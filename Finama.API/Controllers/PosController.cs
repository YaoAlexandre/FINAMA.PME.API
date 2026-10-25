using Finama.API.Services;
using Finama.Commerce.Core.DTOs;
using Finama.Commerce.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finama.API.Controllers;

[Authorize(Policy = Policies.Commercial)]
[ApiController]
[Route("api/commerce/pos")]
public class PosController : ControllerBase
{
    private readonly IPosService _posService;
    private readonly ILiaisonComptableService _liaison;
    private readonly ILogger<PosController> _log;

    public PosController(IPosService posService, ILiaisonComptableService liaison, ILogger<PosController> log)
    {
        _posService = posService;
        _liaison = liaison;
        _log = log;
    }

    /// <summary>Caisses d'un point de vente (crée « Caisse 1 » au premier accès).</summary>
    [HttpGet("caisses")]
    public async Task<IActionResult> Caisses([FromQuery] Guid magasinId, CancellationToken ct)
        => Ok(await _posService.ListerCaissesAsync(magasinId, ct));

    /// <summary>Session ouverte du caissier connecté sur ce magasin (204 si aucune).</summary>
    [HttpGet("session-active")]
    public async Task<IActionResult> SessionActive([FromQuery] Guid magasinId, CancellationToken ct)
    {
        var session = await _posService.SessionActiveAsync(magasinId, ct);
        return session == null ? NoContent() : Ok(session);
    }

    [HttpPost("sessions/ouvrir")]
    public async Task<IActionResult> OuvrirSession([FromBody] OuvrirSessionRequest request, CancellationToken ct)
        => Ok(await _posService.OuvrirSessionAsync(request, ct));

    [HttpPost("ventes")]
    public async Task<IActionResult> CreerVente([FromBody] CreerVenteRequest request, CancellationToken ct)
        => Ok(await _posService.CreerVenteAsync(request, ct));

    // ⚠️ Ne reçoit plus l'entité ClotureCaisse : le client ne déclare que ses montants comptés.
    [HttpPost("sessions/cloturer")]
    public async Task<IActionResult> CloturerSession([FromBody] ClotureAveugleRequest request, CancellationToken ct)
    {
        var rapport = await _posService.CloturerSessionAsync(request, ct);
        try
        {
            var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")!.Value);
            await _liaison.ComptabiliserCaisseAsync(request.SessionCaisseId, userId, ct);
        }
        catch (Exception ex) { _log.LogWarning(ex, "Liaison comptable : caisse {Id}", request.SessionCaisseId); }
        return Ok(rapport);
    }

    [Authorize(Policy = Policies.AdminTenant)] // Rapport Z : gérant / administrateur uniquement
    [HttpGet("sessions/{sessionId:guid}/rapport-z")]
    public async Task<IActionResult> ObtenirRapportZ(Guid sessionId, CancellationToken ct)
        => Ok(await _posService.ObtenirRapportZAsync(sessionId, ct));
}
