using Microsoft.EntityFrameworkCore;

namespace Finama.Infrastructure.Data;

public static class SequenceExtensions
{
    /// <summary>
    /// Renvoie le prochain numéro de la séquence (tenant, clé, année) sous verrou SQL Server.
    /// À appeler DANS une transaction : le verrou tient jusqu'au commit, aucun doublon possible.
    /// Clés utilisées : "ECR-{journal}" (écritures), "FAC-FA" / "FAC-AC" (factures).
    /// </summary>
    public static async Task<int> ProchainNumeroAsync(this AppDbContext db, Guid tenantId, string cle, int annee)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("ProchainNumeroAsync doit être appelé dans une transaction.");

        return (await db.Database.SqlQueryRaw<int>(@"
SET NOCOUNT ON;
IF NOT EXISTS (SELECT 1 FROM SequencesComptables WITH (UPDLOCK, HOLDLOCK) WHERE TenantId = {0} AND Cle = {1} AND Annee = {2})
    INSERT INTO SequencesComptables (Id, TenantId, Cle, Annee, DernierNumero) VALUES (NEWID(), {0}, {1}, {2}, 0);
UPDATE SequencesComptables SET DernierNumero = DernierNumero + 1
OUTPUT inserted.DernierNumero AS [Value]
WHERE TenantId = {0} AND Cle = {1} AND Annee = {2};", tenantId, cle, annee).ToListAsync()).Single();
    }
}
