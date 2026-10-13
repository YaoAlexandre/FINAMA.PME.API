using Finama.Core.DTOs;
using Finama.Infrastructure.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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
        _validator = validator;
    }

    /// <summary>
    /// Crée une nouvelle écriture comptable en brouillon.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Comptable, AdminTenant")] // Réactivé pour aligner la sécurité de saisie
    public async Task<IActionResult> Creer([FromBody] CreerEcritureRequest request)
    {
        var validation = await _validator.ValidateAsync(request);
        if (!validation.IsValid)
            return BadRequest(new
            {
                message = "Données invalides.",
                erreurs = validation.Errors.Select(e => new
                {
                    champ = e.PropertyName,
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
    /// 🌟 AJOUT : Met à jour une écriture comptable existante en statut Brouillon.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Comptable, AdminTenant")]
    public async Task<IActionResult> Modifier(Guid id, [FromBody] CreerEcritureRequest request)
    {
        // On réutilise le même validateur FluentValidation que la création
        var validation = await _validator.ValidateAsync(request);
        if (!validation.IsValid)
            return BadRequest(new
            {
                message = "Données invalides.",
                erreurs = validation.Errors.Select(e => new
                {
                    champ = e.PropertyName,
                    message = e.ErrorMessage
                })
            });

        try
        {
            var utilisateurId = ObtenirUtilisateurId();

            // 🌟 On appelle une méthode ModifierAsync sur ton service métier 
            // (Assure-toi qu'elle accepte bien l'ID, la request et l'utilisateur)
            var result = await _ecritureService.ModifierAsync(id, request, utilisateurId);

            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // Capturera les blocages si l'utilisateur tente de modifier une écriture déjà "Validée" ou un "Exercice clos"
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
    [Authorize(Roles = "Comptable, AdminTenant")]
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
    [Authorize(Roles = "Comptable, AdminTenant")]
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