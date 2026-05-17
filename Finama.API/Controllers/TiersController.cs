using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Finama.Core.DTOs;
using Finama.Infrastructure.Services;

namespace Finama.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TiersController : ControllerBase
{
    private readonly ITiersService _tiersService;

    public TiersController(ITiersService tiersService)
    {
        _tiersService = tiersService;
    }

    /// <summary>
    /// Crée un client ou fournisseur.
    /// Code généré automatiquement : CLI-001, FRN-001, TRS-001.
    /// </summary>
    [HttpPost]
    //[Authorize(Policy = "Comptable")]
    public async Task<IActionResult> Creer([FromBody] CreerTiersRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nom))
            return BadRequest(new { message = "Le nom du tiers est obligatoire." });

        try
        {
            var result = await _tiersService.CreerAsync(request);
            return CreatedAtAction(nameof(Obtenir), new { id = result.Id }, result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Liste paginée avec filtres — type, recherche, statut actif.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Lister([FromQuery] FiltreTiersQuery filtre)
    {
        if (filtre.PageSize > 100)
            return BadRequest(new { message = "Taille de page maximale : 100." });

        var result = await _tiersService.ListerAsync(filtre);
        return Ok(result);
    }

    /// <summary>
    /// Détail d'un tiers.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obtenir(Guid id)
    {
        try
        {
            var result = await _tiersService.ObtenirAsync(id);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Modifier un tiers existant.
    /// </summary>
    [HttpPut("{id:guid}")]
    //[Authorize(Policy = "Comptable")]
    public async Task<IActionResult> Modifier(Guid id, [FromBody] ModifierTiersRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nom))
            return BadRequest(new { message = "Le nom du tiers est obligatoire." });

        try
        {
            var result = await _tiersService.ModifierAsync(id, request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Supprime un tiers (soft delete).
    /// Bloqué si le tiers a des factures liées — désactivez-le plutôt.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "AdminTenant")]
    public async Task<IActionResult> Supprimer(Guid id)
    {
        try
        {
            await _tiersService.SupprimerAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Recherche rapide — pour les selects du frontend.
    /// Retourne id, code, nom uniquement.
    /// </summary>
    [HttpGet("recherche")]
    public async Task<IActionResult> Recherche([FromQuery] string? q, [FromQuery] string? type)
    {
        var filtre = new FiltreTiersQuery(
            Type:      type,
            Recherche: q,
            EstActif:  true,
            Page:      1,
            PageSize:  10
        );

        var result = await _tiersService.ListerAsync(filtre);
        return Ok(result.Items.Select(t => new
        {
            t.Id,
            t.Code,
            t.Nom,
            t.Type,
            t.Email,
            t.Telephone,
        }));
    }
}
