using Finama.API.Services;
using Finama.Core.DTOs;
using Finama.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Finama.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FacturesController : ControllerBase
{
    private readonly IFactureService _factureService;
    private readonly ILiaisonComptableService _liaison;
    private readonly ILogger<FacturesController> _log;
    public FacturesController(IFactureService factureService, ILiaisonComptableService liaison, ILogger<FacturesController> log)
    {
        _factureService = factureService;
        _liaison = liaison;
        _log = log;
    }

    /// <summary>
    /// Crée une nouvelle facture.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Comptable, AdminTenant")]
    public async Task<IActionResult> Creer([FromBody] CreerFactureRequest request)
    {
        try
        {
            var utilisateurId = ObtenirUtilisateurId();
            var result = await _factureService.CreerAsync(request, utilisateurId);

            // Écriture automatique si la liaison comptable est active (un échec n'annule jamais la facture)
            try { await _liaison.ComptabiliserFactureAsync(result.Id, utilisateurId); }
            catch (Exception ex) { _log.LogWarning(ex, "Liaison comptable : facture {Id}", result.Id); }

            return CreatedAtAction(nameof(Obtenir), new { id = result.Id }, result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Liste paginée des factures.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Lister([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _factureService.ListerAsync(Math.Max(1, page), Math.Clamp(pageSize, 1, 100));
        return Ok(result);
    }

    /// <summary>
    /// Détail d'une facture.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obtenir(Guid id)
    {
        try
        {
            var result = await _factureService.ObtenirAsync(id);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Télécharge la facture en PDF.
    /// </summary>
    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> TelechargerPdf(Guid id)
    {
        try
        {
            var pdf = await _factureService.GenererPdfAsync(id);
            var facture = await _factureService.ObtenirAsync(id);
            var nomFichier = $"Facture_{facture.Numero.Replace("-", "_")}.pdf";
            return File(pdf, "application/pdf", nomFichier);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    private Guid ObtenirUtilisateurId()
    {
        // Cherche d'abord "sub" (brut), puis l'alias NameIdentifier au cas où
        var claim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
                    ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(claim, out var id)
            ? id
            : throw new UnauthorizedAccessException("Utilisateur non identifié.");
    }
}
