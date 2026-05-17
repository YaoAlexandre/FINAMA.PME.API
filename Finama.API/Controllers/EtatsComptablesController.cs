using Finama.Core.DTOs;
using Finama.Core.Entities;
using Finama.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Finama.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EtatsComptablesController : ControllerBase
{
    private readonly AppDbContext _db;

    public EtatsComptablesController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Génère la balance des comptes pour l'exercice actif du locataire (Tenant).
    /// </summary>
    [HttpGet("balance")]
    public async Task<IActionResult> GetBalanceComptable()
    {
        try
        {
            var tenantId = ObtenirTenantId();
            int anneeCourante = DateTime.Today.Year;

            // 1. Récupérer l'exercice actif correspondant pour le Tenant
            var exercice = await _db.Exercices
                .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Annee == anneeCourante);

            if (exercice == null)
            {
                return NotFound(new { message = $"Aucun exercice comptable initialisé pour l'année {anneeCourante}." });
            }

            // 2. Extraire et cumuler les lignes d'écritures Validées de cet exercice
            var donneesComptes = await _db.LignesEcriture
                .Where(l => l.TenantId == tenantId && l.Ecriture.ExerciceId == exercice.Id && l.Ecriture.Statut == StatutEcriture.Validee)
                .GroupBy(l => new { l.CompteId, l.Compte.Numero, l.Compte.Libelle, l.Compte.Classe })
                .Select(g => new
                {
                    g.Key.Numero,
                    g.Key.Libelle,
                    Classe = (int)g.Key.Classe,
                    MouvementsDebit = g.Sum(l => l.Debit),
                    MouvementsCredit = g.Sum(l => l.Credit)
                })
                .ToListAsync();

            // 3. Construire les lignes de la balance locales pour l'API
            var lignesBalance = new List<ApiBalanceLigneDto>();

            foreach (var item in donneesComptes.OrderBy(c => c.Numero))
            {
                decimal soldeFinalDebit = item.MouvementsDebit > item.MouvementsCredit ? item.MouvementsDebit - item.MouvementsCredit : 0;
                decimal soldeFinalCredit = item.MouvementsCredit > item.MouvementsDebit ? item.MouvementsCredit - item.MouvementsDebit : 0;

                lignesBalance.Add(new ApiBalanceLigneDto(
                    Numero: item.Numero,
                    Libelle: item.Libelle,
                    Classe: item.Classe,
                    SoldeOuvertureDebit: 0,
                    SoldeOuvertureCredit: 0,
                    MouvementsDebit: item.MouvementsDebit,
                    MouvementsCredit: item.MouvementsCredit,
                    SoldeFinalDebit: soldeFinalDebit,
                    SoldeFinalCredit: soldeFinalCredit
                ));
            }

            // 4. Calculer les totaux globaux de la balance
            decimal totalMouvementsDebit = lignesBalance.Sum(l => l.MouvementsDebit);
            decimal totalMouvementsCredit = lignesBalance.Sum(l => l.MouvementsCredit);
            decimal totalSoldeFinalDebit = lignesBalance.Sum(l => l.SoldeFinalDebit);
            decimal totalSoldeFinalCredit = lignesBalance.Sum(l => l.SoldeFinalCredit);

            var totaux = new ApiBalanceTotauxDto(
                TotalOuvertureDebit: 0,
                TotalOuvertureCredit: 0,
                TotalMouvementsDebit: totalMouvementsDebit,
                TotalMouvementsCredit: totalMouvementsCredit,
                TotalSoldeFinalDebit: totalSoldeFinalDebit,
                TotalSoldeFinalCredit: totalSoldeFinalCredit
            );

            // 5. Analyse de l'équilibre comptable (Partie double)
            bool estEquilibree = totalMouvementsDebit == totalMouvementsCredit;
            string? avertissement = estEquilibree
                ? null
                : $"Écart de déséquilibre détecté : {Math.Abs(totalMouvementsDebit - totalMouvementsCredit):N0} FCFA entre les débits et les crédits.";

            var rapportBalance = new ApiBalanceDto(
                Annee: exercice.Annee,
                Lignes: lignesBalance,
                Totaux: totaux
            );

            // 6. Construction du Wrapper final attendu par le Front-End via le JSON
            var wrapper = new ApiBalanceWrapper(
                ExerciceId: exercice.Id,
                Rapport: rapportBalance,
                EstEquilibree: estEquilibree,
                Avertissement: avertissement
            );

            return Ok(wrapper);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Erreur interne lors de la compilation de la balance.", details = ex.Message });
        }
    }

    private Guid ObtenirTenantId()
    {
        var claim = User.FindFirst("TenantId")?.Value
                 ?? User.FindFirst("tenant_id")?.Value;

        return Guid.TryParse(claim, out var id)
            ? id
            : throw new UnauthorizedAccessException("Contexte entreprise (Tenant ID) introuvable.");
    }
}

