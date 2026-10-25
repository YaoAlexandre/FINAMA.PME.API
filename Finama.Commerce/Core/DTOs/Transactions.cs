using Finama.Commerce.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Finama.Commerce.Application.Common;

public static class Transactions
{
    /// <summary>
    /// Unité de travail atomique : stratégie de reprise SQL Server + transaction explicite + nouvelle tentative
    /// quand deux caisses modifient le même stock au même instant (concurrence optimiste via RowVersion).
    /// Le travail est rejoué depuis zéro : il doit donc tout relire (jamais d'entité chargée avant l'appel).
    /// </summary>
    public static Task<T> ExecuterAsync<T>(this ICommerceDbContext db, Func<Task<T>> travail, CancellationToken ct, int tentativesMax = 3)
    {
        var strategie = db.Database.CreateExecutionStrategy();
        return strategie.ExecuteAsync(async () =>
        {
            for (var tentative = 1; ; tentative++)
            {
                db.ChangeTracker.Clear();
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                try
                {
                    var resultat = await travail();
                    await tx.CommitAsync(ct);
                    return resultat;
                }
                catch (DbUpdateConcurrencyException) when (tentative < tentativesMax)
                {
                    await tx.RollbackAsync(ct);
                }
            }
        });
    }

    public static async Task<ParametresCommerce> ParametresOuDefautAsync(this ICommerceDbContext db, CancellationToken ct)
        => await db.Parametres.AsNoTracking().FirstOrDefaultAsync(ct) ?? new ParametresCommerce();
}
