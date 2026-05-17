using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Finama.Infrastructure.Data;
using Finama.Infrastructure.Services;

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
        _db                 = db;
    }

    /// <summary>
    /// KPIs complets pour le tableau de bord.
    /// Si exerciceId non fourni, prend l'exercice en cours automatiquement.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] Guid? exerciceId)
    {
        try
        {
            Guid idExercice;

            if (exerciceId.HasValue)
            {
                idExercice = exerciceId.Value;
            }
            else
            {
                // Prendre l'exercice de l'année en cours automatiquement
                var exerciceCourant = await _db.Exercices
                    .Where(e => e.Annee == DateTime.Today.Year && !e.EstCloture)
                    .FirstOrDefaultAsync();

                if (exerciceCourant is null)
                    return NotFound(new { message = "Aucun exercice actif trouvé pour cette année." });

                idExercice = exerciceCourant.Id;
            }

            var result = await _tableauBordService.GetAsync(idExercice);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
