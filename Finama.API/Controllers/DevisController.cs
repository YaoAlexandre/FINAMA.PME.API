using Finama.Core.DTOs;
using Finama.Core.Entities;
using Finama.Core.Securite;
using Finama.Infrastructure.Services;
using Finama.Infrastructure.Services.Commercials;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Finama.API.Controllers;

/// <summary>
/// Module Ventes &amp; Devis. Sans « Voir tous les devis », un utilisateur ne voit que les siens.
/// Erreurs métier (statut, validation, introuvable) traduites par ExceptionMiddleware (400 / 404 / 422).
/// </summary>
[ApiController]
[Route("api/devis")]
[Authorize(Policy = Policies.Ventes)]
public class DevisController : ControllerBase
{
    private readonly IDevisService _devis;
    private readonly IDevisPdfService _pdf;
    private readonly IPermissionsService _permissions;

    public DevisController(IDevisService devis, IDevisPdfService pdf, IPermissionsService permissions)
    {
        _devis = devis;
        _pdf = pdf;
        _permissions = permissions;
    }

    [HttpGet]
    public async Task<IActionResult> Lister([FromQuery] StatutDevis? statut, [FromQuery] string? recherche)
        => Ok(await _devis.ListerAsync(await ContexteAsync(), statut, recherche));

    [HttpGet("tableau-bord")]
    public async Task<IActionResult> TableauBord()
        => Ok(await _devis.TableauBordAsync(await ContexteAsync()));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obtenir(Guid id)
        => Ok(await _devis.ObtenirAsync(id, await ContexteAsync()));

    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> Pdf(Guid id, CancellationToken ct)
    {
        var devis = await _devis.ObtenirAsync(id, await ContexteAsync());
        var octets = await _pdf.GenererAsync(devis, ct);
        return File(octets, "application/pdf", $"Devis_{devis.Numero}.pdf");
    }

    [HttpPost]
    [Authorize(Policy = "perm:ventes.devis")]
    public async Task<IActionResult> Creer([FromBody] CreerDevisRequest request)
    {
        var devis = await _devis.CreerAsync(request, await ContexteAsync());
        return CreatedAtAction(nameof(Obtenir), new { id = devis.Id }, devis);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:ventes.devis")]
    public async Task<IActionResult> Modifier(Guid id, [FromBody] CreerDevisRequest request)
        => Ok(await _devis.ModifierAsync(id, request, await ContexteAsync()));

    [HttpPost("{id:guid}/envoyer")]
    [Authorize(Policy = "perm:ventes.devis")]
    public async Task<IActionResult> Envoyer(Guid id) => Ok(await _devis.EnvoyerAsync(id, await ContexteAsync()));

    [HttpPost("{id:guid}/accepter")]
    [Authorize(Policy = "perm:ventes.devis")]
    public async Task<IActionResult> Accepter(Guid id) => Ok(await _devis.AccepterAsync(id, await ContexteAsync()));

    [HttpPost("{id:guid}/refuser")]
    [Authorize(Policy = "perm:ventes.devis")]
    public async Task<IActionResult> Refuser(Guid id, [FromBody] RefuserDevisRequest? request)
        => Ok(await _devis.RefuserAsync(id, request?.Motif, await ContexteAsync()));

    [HttpPost("{id:guid}/brouillon")]
    [Authorize(Policy = "perm:ventes.devis")]
    public async Task<IActionResult> RemettreEnBrouillon(Guid id) => Ok(await _devis.RemettreEnBrouillonAsync(id, await ContexteAsync()));

    [HttpPost("{id:guid}/dupliquer")]
    [Authorize(Policy = "perm:ventes.devis")]
    public async Task<IActionResult> Dupliquer(Guid id) => Ok(await _devis.DupliquerAsync(id, await ContexteAsync()));

    /// <summary>Crée la facture de vente (brouillon) d'un devis accepté. Une seule fois par devis.</summary>
    [HttpPost("{id:guid}/convertir")]
    [Authorize(Policy = "perm:ventes.facturer")]
    public async Task<IActionResult> Convertir(Guid id) => Ok(await _devis.ConvertirEnFactureAsync(id, await ContexteAsync()));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "perm:ventes.devis")]
    public async Task<IActionResult> Supprimer(Guid id)
    {
        await _devis.SupprimerAsync(id, await ContexteAsync());
        return NoContent();
    }

    // Permission « Voir tous les devis » : toute l'entreprise ; sinon seulement ses propres devis
    private async Task<ContexteVendeur> ContexteAsync()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (!Guid.TryParse(claim, out var id))
            throw new UnauthorizedAccessException("Session invalide.");

        var voitTout = (await _permissions.DeAsync(User)).Contains(Perm.VentesTousDevis);
        return new ContexteVendeur(id, voitTout);
    }
}
