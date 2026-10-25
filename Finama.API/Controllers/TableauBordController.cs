using Finama.Infrastructure.Data;
using Finama.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Finama.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TableauBordController : ControllerBase
{
    private readonly ITableauBordService _tableauBordService;
    private readonly AppDbContext _db;

    public TableauBordController(ITableauBordService tableauBordService, AppDbContext db)
    {
        _tableauBordService = tableauBordService;
        _db = db;
    }

    /// <summary>
    /// KPIs du tableau de bord.
    /// Sans exerciceId : exercice ouvert de l'année en cours, sinon le plus récent encore ouvert.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] Guid? exerciceId)
    {
        if (!Guid.TryParse(User.FindFirst("tenant_id")?.Value, out _))
            return NotFound(new { code = "SANS_TENANT", message = "Ce compte n'est rattaché à aucune entreprise : pas de tableau de bord comptable." });

        Guid idExercice;

        if (exerciceId is { } id && id != Guid.Empty)
        {
            // Le filtre multi-tenant garantit que l'exercice appartient bien à l'entreprise connectée
            if (!await _db.Exercices.AnyAsync(e => e.Id == id))
                return NotFound(new { code = "EXERCICE_INTROUVABLE", message = "Exercice comptable introuvable." });

            idExercice = id;
        }
        else
        {
            var courant = await ExercicesController.ExerciceDeTravail(_db).Select(e => (Guid?)e.Id).FirstOrDefaultAsync();
            if (courant is null)
                return NotFound(new { code = "AUCUN_EXERCICE", message = "Aucun exercice ouvert. Ouvrez un exercice depuis l'administration." });

            idExercice = courant.Value;
        }

        // KeyNotFoundException éventuelle → 404 via ExceptionMiddleware
        return Ok(await _tableauBordService.GetAsync(idExercice));
    }
}