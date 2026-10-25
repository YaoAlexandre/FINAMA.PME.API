using Finama.Core.Entities;
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

    /// <summary>Liste tous les exercices du tenant (le plus récent en premier).</summary>
    [HttpGet]
    public async Task<IActionResult> Lister()
    {
        var exercices = await _db.Exercices
            .AsNoTracking()
            .OrderByDescending(e => e.Annee)
            .Select(e => new { e.Id, e.Annee, e.Libelle, e.DateDebut, e.DateFin, e.EstCloture, e.ClotureLe })
            .ToListAsync();

        return Ok(new { Items = exercices, TotalItems = exercices.Count });
    }

    /// <summary>
    /// Exercice de travail : l'exercice ouvert de l'année en cours,
    /// sinon le plus récent encore ouvert (ex. janvier, avant clôture de N-1).
    /// </summary>
    [HttpGet("courant")]
    public async Task<IActionResult> Courant()
    {
        if (!AUnTenant())
            return NotFound(new { message = "Ce compte n'est rattaché à aucune entreprise." });

        var exercice = await ExerciceDeTravail(_db)
            .Select(e => new { e.Id, e.Annee, e.Libelle, e.DateDebut, e.DateFin, e.EstCloture })
            .FirstOrDefaultAsync();

        return exercice is null
            ? NotFound(new { message = "Aucun exercice ouvert. Ouvrez un exercice depuis l'administration." })
            : Ok(exercice);
    }

    /// <summary>
    /// Ouvre manuellement un exercice (ex. N+1 avant la clôture de N, ou reprise d'un exercice antérieur).
    /// L'index unique (TenantId, Annee) empêche tout doublon.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Policies.AdminTenant)]
    public async Task<IActionResult> Ouvrir([FromBody] OuvrirExerciceRequest request)
    {
        var tenantId = ObtenirTenantId();
        var anneeCourante = DateTime.Today.Year;

        if (request.Annee < anneeCourante - 5 || request.Annee > anneeCourante + 1)
            return BadRequest(new { message = $"L'année doit être comprise entre {anneeCourante - 5} et {anneeCourante + 1}." });

        if (await _db.Exercices.AnyAsync(e => e.Annee == request.Annee))
            return Conflict(new { message = $"L'exercice {request.Annee} existe déjà." });

        var exercice = new ExerciceComptable
        {
            TenantId = tenantId,
            Annee = request.Annee,
            Libelle = $"Exercice {request.Annee}",
            DateDebut = new DateTime(request.Annee, 1, 1),
            DateFin = new DateTime(request.Annee, 12, 31),
            EstCloture = false
        };

        _db.Exercices.Add(exercice);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(Courant), null,
            new { exercice.Id, exercice.Annee, exercice.Libelle, exercice.DateDebut, exercice.DateFin, exercice.EstCloture });
    }

    /// <summary>Clôture définitivement un exercice ouvert : solde la gestion et génère le RAN dans N+1.</summary>
    [HttpPost("{id:guid}/cloturer")]
    [Authorize(Policy = Policies.Comptable)] // AdminTenant + Comptable
    public async Task<IActionResult> Cloturer(Guid id)
    {
        // Erreurs métier (introuvable, brouillons restants, balance déséquilibrée…) → ExceptionMiddleware (404 / 422)
        await _clotureService.CloturerExerciceAsync(ObtenirTenantId(), id, ObtenirUtilisateurId());

        return Ok(new { message = "Exercice clôturé. L'exercice suivant est ouvert avec ses écritures de report à nouveau." });
    }

    // ─── Partagé avec TableauBordController ───────────────────────────────────

    /// <summary>Exercices ouverts triés : année en cours d'abord, puis le plus récent.</summary>
    internal static IQueryable<ExerciceComptable> ExerciceDeTravail(AppDbContext db)
    {
        var annee = DateTime.Today.Year;
        return db.Exercices
            .AsNoTracking()
            .Where(e => !e.EstCloture)
            .OrderByDescending(e => e.Annee == annee)
            .ThenByDescending(e => e.Annee);
    }

    // ─── Contexte utilisateur ─────────────────────────────────────────────────

    private bool AUnTenant() => Guid.TryParse(User.FindFirst("tenant_id")?.Value, out _);

    private Guid ObtenirUtilisateurId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(claim, out var id) ? id : throw new UnauthorizedAccessException("Utilisateur non authentifié.");
    }

    private Guid ObtenirTenantId()
    {
        var claim = User.FindFirst("tenant_id")?.Value ?? User.FindFirst("TenantId")?.Value;
        return Guid.TryParse(claim, out var id) ? id : throw new UnauthorizedAccessException("Contexte entreprise (tenant) manquant.");
    }
}

public record OuvrirExerciceRequest(int Annee);