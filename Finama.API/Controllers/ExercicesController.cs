using Finama.Infrastructure.Data;
using Finama.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Finama.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ExercicesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IClotureService _clotureService;

    public ExercicesController(AppDbContext db, IClotureService clotureService)
    {
        _db = db;
        _clotureService = clotureService;
    }

    /// <summary>
    /// Liste tous les exercices du tenant.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Lister()
    {
        var exercices = await _db.Exercices
            .OrderByDescending(e => e.Annee)
            .Select(e => new {
                e.Id, e.Annee, e.DateDebut, e.DateFin, e.EstCloture
            })
            .ToListAsync();

        return Ok(new { Items = exercices, TotalItems = exercices.Count });
    }

    /// <summary>
    /// Exercice de l'année en cours — utilisé par le frontend au chargement.
    /// </summary>
    [HttpGet("courant")]
    public async Task<IActionResult> Courant()
    {
        var exercice = await _db.Exercices
            .Where(e => e.Annee == DateTime.Today.Year && !e.EstCloture)
            .Select(e => new {
                e.Id, e.Annee, e.DateDebut, e.DateFin, e.EstCloture
            })
            .FirstOrDefaultAsync();

        if (exercice is null)
            return NotFound(new { message = "Aucun exercice actif pour cette année." });

        return Ok(exercice);
    }

    /// <summary>
    /// Clôture définitivement un exercice comptable ouvert, solde la gestion et génère le RAN.
    /// </summary>
    [HttpPost("{id:guid}/cloturer")]
    [Authorize(Roles = "Comptable")]
    public async Task<IActionResult> Cloturer(Guid id)
    {
        try
        {
            var utilisateurId = ObtenirUtilisateurId();
            var tenantId = ObtenirTenantId();

            await _clotureService.CloturerExerciceAsync(tenantId, id, utilisateurId);

            return Ok(new { message = "L'exercice comptable a été clôturé avec succès. Le nouvel exercice a été ouvert avec ses écritures de Report à Nouveau." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ─── Extraction Sécurisée du Contexte Utilisateur ────────────────────────
    private Guid ObtenirUtilisateurId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                 ?? User.FindFirst("sub")?.Value;

        return Guid.TryParse(claim, out var id) ? id : throw new UnauthorizedAccessException("Utilisateur non authentifié.");
    }

    private Guid ObtenirTenantId()
    {
        var claim = User.FindFirst("TenantId")?.Value
                 ?? User.FindFirst("tenant_id")?.Value;

        return Guid.TryParse(claim, out var id) ? id : throw new UnauthorizedAccessException("Contexte entreprise (Tenant) manquant.");
    }
}
