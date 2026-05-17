using Finama.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace Finama.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // Accessible par tout utilisateur authentifié sur Finama
public class DevisesController : ControllerBase
{
    private readonly AppDbContext _db;

    public DevisesController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Récupère la liste de toutes les devises actives sur la plateforme.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var devises = await _db.Devises
            .Where(d => d.EstActive)
            .Select(d => new
            {
                d.Code,
                d.Symbole,
                d.Libelle,
                d.TauxBaseDollar
            })
            .ToListAsync();

        return Ok(devises);
    }
}