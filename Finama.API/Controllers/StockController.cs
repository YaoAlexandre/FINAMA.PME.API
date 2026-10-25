using Finama.Commerce.Core.DTOs;
using Finama.Commerce.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finama.API.Controllers;

/// <summary>
/// Stock et inventaires. Lecture : tout utilisateur connecté de l'entreprise.
/// Mouvements (entrée, ajustement, comptage) : Commercial / AdminTenant. Clôture d'inventaire : AdminTenant.
/// Les erreurs métier (BusinessRuleException, NotFoundException) sont traduites par ExceptionMiddleware (422 / 404).
/// </summary>
[Authorize]
[ApiController]
[Route("api/commerce/stock")]
public class StockController : ControllerBase
{
    private readonly IStockService _stockService;

    public StockController(IStockService stockService)
    {
        _stockService = stockService;
    }

    /// <summary>Niveaux de stock d'un magasin (par défaut le principal) : lots actifs + articles sans lot.</summary>
    [HttpGet("lots")]
    public async Task<IActionResult> GetStocksLots([FromQuery] Guid? magasinId, CancellationToken ct)
        => Ok(await _stockService.GetStocksLotsAsync(magasinId, ct));

    [HttpPost("ajuster")]
    [Authorize(Policy = Policies.Commercial)]
    public async Task<IActionResult> AjusterStock([FromBody] AjusterStockRequest request, CancellationToken ct)
        => Ok(await _stockService.AjusterStockAsync(request, ct));

    [HttpPost("entree-initiale")]
    [Authorize(Policy = Policies.Commercial)]
    public async Task<IActionResult> EntreeInitiale([FromBody] EntreeInitialeRequest request, CancellationToken ct)
    {
        await _stockService.EntreeInitialeAsync(request, ct);
        return Ok(new { message = "Entrée de stock enregistrée." });
    }

    [HttpGet("inventaires")]
    public async Task<IActionResult> GetInventaires(CancellationToken ct)
        => Ok(await _stockService.GetInventairesAsync(ct));

    [HttpGet("inventaires/{inventaireId:guid}")]
    public async Task<IActionResult> GetInventaireDetails(Guid inventaireId, CancellationToken ct)
    {
        var inventaire = await _stockService.GetInventaireDetailsAsync(inventaireId, ct);
        return inventaire is null
            ? NotFound(new { message = "Feuille d'inventaire introuvable." })
            : Ok(inventaire);
    }

    [HttpPost("inventaires/ouvrir")]
    [Authorize(Policy = Policies.Commercial)]
    public async Task<IActionResult> OuvrirInventaire([FromBody] OuvrirInventaireRequest request, CancellationToken ct)
        => Ok(await _stockService.OuvrirInventaireAsync(request, ct));

    [HttpPost("inventaires/{inventaireId:guid}/compter")]
    [Authorize(Policy = Policies.Commercial)]
    public async Task<IActionResult> CompterInventaire(Guid inventaireId, [FromBody] ComptageRequest request, CancellationToken ct)
    {
        await _stockService.CompterInventaireAsync(inventaireId, request, ct);
        return Ok(new { message = "Comptage enregistré." });
    }

    /// <summary>Annule un inventaire en cours sans toucher au stock. Réservé à l'administrateur.</summary>
    [HttpPost("inventaires/{inventaireId:guid}/annuler")]
    [Authorize(Policy = Policies.AdminTenant)]
    public async Task<IActionResult> AnnulerInventaire(Guid inventaireId, CancellationToken ct)
    {
        await _stockService.AnnulerInventaireAsync(inventaireId, ct);
        return Ok(new { message = "Inventaire annulé. Aucun stock n'a été modifié." });
    }

    /// <summary>Clôture : applique les écarts au stock et aux lots. Réservé à l'administrateur.</summary>
    [HttpPost("inventaires/{inventaireId:guid}/cloturer")]
    [Authorize(Policy = Policies.AdminTenant)]
    public async Task<IActionResult> CloturerInventaire(Guid inventaireId, [FromBody] ClotureInventaireRequest request, CancellationToken ct)
    {
        var resultat = await _stockService.CloturerInventaireAsync(inventaireId, request, ct);
        return Ok(new
        {
            message = resultat.NbEcarts == 0
                ? "Inventaire clôturé : aucun écart."
                : $"Inventaire clôturé : {resultat.NbEcarts} écart(s) régularisé(s), valeur {resultat.ValeurEcartAuCout:N0} FCFA au coût moyen.",
            resultat.NbLignes,
            resultat.NbEcarts,
            resultat.ValeurEcartAuCout
        });
    }
}