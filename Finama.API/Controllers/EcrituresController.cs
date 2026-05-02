using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using FluentValidation;
using Finama.Core.DTOs;
using Finama.Infrastructure.Services;

namespace Finama.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EcrituresController : ControllerBase
{
    private readonly IEcritureService _ecritureService;
    private readonly IValidator<CreerEcritureRequest> _validator;

    public EcrituresController(
        IEcritureService ecritureService,
        IValidator<CreerEcritureRequest> validator)
    {
        _ecritureService = ecritureService;
        _validator       = validator;
    }

    /// <summary>
    /// Crée une nouvelle écriture comptable en brouillon.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "Comptable")]
    public async Task<IActionResult> Creer([FromBody] CreerEcritureRequest request)
    {
        // Validation FluentValidation
        var validation = await _validator.ValidateAsync(request);
        if (!validation.IsValid)
            return BadRequest(new {
                message = "Données invalides.",
                erreurs = validation.Errors.Select(e => new {
                    champ   = e.PropertyName,
                    message = e.ErrorMessage
                })
            });

        try
        {
            var utilisateurId = ObtenirUtilisateurId();
            var result = await _ecritureService.CreerAsync(request, utilisateurId);
            return CreatedAtAction(nameof(Obtenir), new { id = result.Id }, result);
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
    /// Liste paginée des écritures avec filtres optionnels.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Lister([FromQuery] FiltreEcritureQuery filtre)
    {
        if (filtre.PageSize > 100)
            return BadRequest(new { message = "La taille de page maximale est 100." });

        var result = await _ecritureService.ListerAsync(filtre);
        return Ok(result);
    }

    /// <summary>
    /// Détail complet d'une écriture avec ses lignes.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obtenir(Guid id)
    {
        try
        {
            var result = await _ecritureService.ObtenirAsync(id);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Valide une écriture en brouillon — irréversible.
    /// </summary>
    [HttpPut("{id:guid}/valider")]
    [Authorize(Policy = "Comptable")]
    public async Task<IActionResult> Valider(Guid id)
    {
        try
        {
            var utilisateurId = ObtenirUtilisateurId();
            var result = await _ecritureService.ValiderAsync(id, utilisateurId);
            return Ok(result);
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
    /// Supprime un brouillon (soft delete).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Comptable")]
    public async Task<IActionResult> Supprimer(Guid id)
    {
        try
        {
            var utilisateurId = ObtenirUtilisateurId();
            await _ecritureService.SupprimerBrouillonAsync(id, utilisateurId);
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

    // ─── Helper ───────────────────────────────────────────────────────────────
    private Guid ObtenirUtilisateurId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                 ?? User.FindFirst("sub")?.Value;

        return Guid.TryParse(claim, out var id)
            ? id
            : throw new UnauthorizedAccessException("Utilisateur non identifié.");
    }
}
