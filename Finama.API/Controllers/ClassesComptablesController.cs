using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Finama.Core.Entities;
using Finama.Infrastructure.Services;

namespace Finama.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ClassesComptablesController : ControllerBase
{
    private readonly IClasseComptableService _classeService;

    public ClassesComptablesController(IClasseComptableService classeService)
    {
        _classeService = classeService;
    }

    [HttpGet]
    public async Task<IActionResult> ObtenirClasses()
    {
        var classes = await _classeService.ObtenirToutesAsync();
        return Ok(classes);
    }

    [HttpPost]
    public async Task<IActionResult> Ajouter([FromBody] ClasseComptable nouvelleClasse)
    {
        try
        {
            await _classeService.AjouterAsync(nouvelleClasse);
            return CreatedAtAction(nameof(ObtenirClasses), new { id = nouvelleClasse.Numero }, nouvelleClasse);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{numero:int}")]
    public async Task<IActionResult> Modifier(int numero, [FromBody] string nouveauLibelle)
    {
        try
        {
            await _classeService.ModifierLibelleAsync(numero, nouveauLibelle);
            return Ok(new { message = "Libellé mis à jour avec succès." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}