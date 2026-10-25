using Finama.Commerce.Application.Common;
using Finama.Commerce.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Finama.Commerce.Infrastructure.Services;

public static class MagasinContexte
{
    /// <summary>Magasin demandé s'il est renseigné, sinon le magasin principal actif. Lève une erreur claire si aucun n'existe.</summary>
    public static async Task<Guid> ResoudreMagasinAsync(this ICommerceDbContext db, Guid? demande, CancellationToken ct = default)
    {
        if (demande is { } id && id != Guid.Empty)
        {
            var existe = await db.Magasins.AnyAsync(m => m.Id == id && m.EstActif, ct);
            if (!existe) throw new NotFoundException("Magasin introuvable ou inactif.");
            return id;
        }

        var principal = await db.Magasins
            .Where(m => m.EstPrincipal && m.EstActif)
            .Select(m => (Guid?)m.Id)
            .FirstOrDefaultAsync(ct);

        return principal ?? throw new BusinessRuleException("Aucun magasin principal défini. Créez d'abord un magasin.");
    }

    /// <summary>Variante tolérante pour les lectures (catalogue) : null si aucun magasin n'existe encore.</summary>
    public static async Task<Guid?> MagasinParDefautAsync(this ICommerceDbContext db, Guid? demande, CancellationToken ct = default)
    {
        if (demande is { } id && id != Guid.Empty) return id;

        return await db.Magasins
            .Where(m => m.EstPrincipal && m.EstActif)
            .Select(m => (Guid?)m.Id)
            .FirstOrDefaultAsync(ct);
    }
}
