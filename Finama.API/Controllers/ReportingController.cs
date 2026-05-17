using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Finama.Core.DTOs;
using Finama.Infrastructure.Services;

namespace Finama.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportingController : ControllerBase
{
    private readonly IReportingService _reportingService;

    public ReportingController(IReportingService reportingService)
    {
        _reportingService = reportingService;
    }

    /// <summary>
    /// Balance des comptes OHADA.
    /// Colonnes : Solde ouverture | Mouvements | Solde final — débit et crédit.
    /// </summary>
    [HttpGet("balance")]
    public async Task<IActionResult> Balance([FromQuery] FiltreBalanceQuery filtre)
    {
        if (filtre.ExerciceId == Guid.Empty)
            return BadRequest(new { message = "L'exerciceId est obligatoire." });

        try
        {
            var result = await _reportingService.GetBalanceAsync(filtre);

            // Avertissement si balance non équilibrée (anomalie comptable)
            var estEquilibree =
                result.Totaux.TotalMouvementsDebit == result.Totaux.TotalMouvementsCredit;

            return Ok(new
            {
                rapport        = result,
                estEquilibree,
                avertissement  = estEquilibree
                    ? null
                    : "⚠️ La balance n'est pas équilibrée — vérifiez vos écritures."
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Grand livre — détail de tous les mouvements par compte.
    /// </summary>
    [HttpGet("grand-livre")]
    public async Task<IActionResult> GrandLivre([FromQuery] FiltreGrandLivreQuery filtre)
    {
        if (filtre.ExerciceId == Guid.Empty)
            return BadRequest(new { message = "L'exerciceId est obligatoire." });

        try
        {
            var result = await _reportingService.GetGrandLivreAsync(filtre);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Résumé rapide — totaux par classe de comptes (pour le tableau de bord).
    /// </summary>
    [HttpGet("resume/{exerciceId:guid}")]
    public async Task<IActionResult> Resume(Guid exerciceId)
    {
        try
        {
            // Balance complète puis on agrège par classe
            var balance = await _reportingService.GetBalanceAsync(new FiltreBalanceQuery(exerciceId));

            var parClasse = balance.Lignes
                .GroupBy(l => l.Classe)
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    Classe         = g.Key,
                    LibelleClasse  = LibelleClasse(g.Key),
                    TotalDebit     = g.Sum(l => l.MouvementsDebit),
                    TotalCredit    = g.Sum(l => l.MouvementsCredit),
                    Solde          = g.Sum(l => l.SoldeFinalDebit) - g.Sum(l => l.SoldeFinalCredit),
                })
                .ToList();

            // Indicateurs clés
            var chiffresAffaires = balance.Lignes
                .Where(l => l.Numero.StartsWith("70"))
                .Sum(l => l.MouvementsCredit);

            var totalCharges = balance.Lignes
                .Where(l => l.Classe == 6)
                .Sum(l => l.MouvementsDebit);

            var resultatNet = chiffresAffaires - totalCharges;

            var tresorerie = balance.Lignes
                .Where(l => l.Classe == 5)
                .Sum(l => l.SoldeFinalDebit - l.SoldeFinalCredit);

            return Ok(new
            {
                exerciceId,
                annee            = balance.Annee,
                chiffresAffaires,
                totalCharges,
                resultatNet,
                tresorerie,
                parClasse,
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    private static string LibelleClasse(int classe) => classe switch
    {
        1 => "Capitaux",
        2 => "Immobilisations",
        3 => "Stocks",
        4 => "Tiers",
        5 => "Trésorerie",
        6 => "Charges",
        7 => "Produits",
        _ => $"Classe {classe}"
    };
}
