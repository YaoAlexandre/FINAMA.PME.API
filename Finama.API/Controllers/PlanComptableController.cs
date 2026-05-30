using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Finama.Core.DTOs;
using Finama.Infrastructure.Services;

namespace Finama.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PlanComptableController : ControllerBase
{
    private readonly IPlanComptableService _planService;

    public PlanComptableController(IPlanComptableService planService)
    {
        _planService = planService;
    }

    /// <summary>
    /// Liste paginée du plan comptable avec filtres.
    /// Filtrable par classe (1-7), recherche (numéro ou libellé), statut actif.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Lister([FromQuery] FiltreCompteQuery filtre)
    {
        if (filtre.PageSize > 200)
            return BadRequest(new { message = "Taille de page maximale : 200." });

        var result = await _planService.ListerAsync(filtre);
        return Ok(result);
    }

    /// <summary>
    /// Détail d'un compte — avec compte parent et nombre de sous-comptes.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obtenir(Guid id)
    {
        try
        {
            var result = await _planService.ObtenirAsync(id);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Calcule et suggère le prochain numéro de sous-compte disponible pour un parent donné.
    /// Utilisé par l'assistant magique sur l'UI Blazor.
    /// </summary>
    [HttpGet("parent/{parentId:guid}/prochain-numero")]
    public async Task<IActionResult> ObtenirProchainNumeroSousCompte(Guid parentId)
    {
        try
        {
            var prochainNumero = await _planService.GenererProchainNumeroSousCompteAsync(parentId);
            // On retourne un Content brut (string) comme attendu par GetStringAsync() dans votre ApiService Blazor
            return Content(prochainNumero, "text/plain");
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Récupère la liste des sous-comptes directs rattachés à un compte parent.
    /// Utile pour le chargement à la demande (Lazy Loading) ou l'affichage en cascade.
    /// </summary>
    [HttpGet("parent/{parentId:guid}/sous-comptes")]
    public async Task<IActionResult> ListerSousComptes(Guid parentId)
    {
        try
        {
            var sousComptes = await _planService.ListerSousComptesAsync(parentId);
            return Ok(sousComptes);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Liste simplifiée pour les selects du frontend.
    /// Optionnel : filtrer par classe (ex: ?classe=4 pour comptes tiers).
    /// </summary>
    [HttpGet("select")]
    public async Task<IActionResult> ListerPourSelect([FromQuery] string? classe = null)
    {
        var result = await _planService.ListerPourSelectAsync(classe);
        return Ok(result.Select(c => new
        {
            c.Id,
            c.Numero,
            c.Libelle,
            Affichage = $"{c.Numero} — {c.Libelle}",
            c.Classe,
            c.Type,
        }));
    }

    /// <summary>
    /// Crée un nouveau compte personnalisé ou un sous-compte.
    /// Validation OHADA : le premier chiffre du numéro doit correspondre à la classe.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "AdminTenant")]
    public async Task<IActionResult> Creer([FromBody] CreerCompteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Numero))
            return BadRequest(new { message = "Le numéro de compte est obligatoire." });

        if (string.IsNullOrWhiteSpace(request.Libelle))
            return BadRequest(new { message = "Le libellé du compte est obligatoire." });

        if (request.Classe < 1 || request.Classe > 7)
            return BadRequest(new { message = "La classe doit être entre 1 et 7 (plan OHADA)." });

        try
        {
            var result = await _planService.CreerAsync(request);
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
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Modifier le libellé ou le compte parent.
    /// Le numéro et la classe ne sont pas modifiables (règle OHADA).
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "AdminTenant")]
    public async Task<IActionResult> Modifier(Guid id, [FromBody] ModifierCompteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Libelle))
            return BadRequest(new { message = "Le libellé est obligatoire." });

        try
        {
            var result = await _planService.ModifierAsync(id, request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Activer ou désactiver un compte.
    /// Bloqué si le compte a des mouvements enregistrés.
    /// </summary>
    [HttpPatch("{id:guid}/statut")]
    [Authorize(Roles = "AdminTenant")]
    public async Task<IActionResult> ChangerStatut(Guid id, [FromBody] ChangerStatutRequest request)
    {
        try
        {
            await _planService.ActiverDesactiverAsync(id, request.EstActif);
            var action = request.EstActif ? "activé" : "désactivé";
            return Ok(new { message = $"Compte {action} avec succès." });
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
}

public record ChangerStatutRequest(bool EstActif);