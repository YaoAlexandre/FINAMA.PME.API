using Finama.Commerce.Core.DTOs;
using Finama.Commerce.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finama.API.Controllers;

[Authorize]
[ApiController]
[Route("api/commerce/categories")]
public class CategoriesController : ControllerBase
{
    private readonly IReferentielCommerceService _referentiel;

    public CategoriesController(IReferentielCommerceService referentiel) => _referentiel = referentiel;

    [HttpGet]
    public async Task<IActionResult> Lister(CancellationToken ct)
        => Ok(await _referentiel.ListerCategoriesAsync(ct));

    [HttpPost]
    [Authorize(Policy = "Commercial")]
    public async Task<IActionResult> Creer([FromBody] SaveCategorieRequest request, CancellationToken ct)
        => Ok(await _referentiel.CreerCategorieAsync(request, ct));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Commercial")]
    public async Task<IActionResult> Modifier(Guid id, [FromBody] SaveCategorieRequest request, CancellationToken ct)
        => Ok(await _referentiel.ModifierCategorieAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Commercial")]
    public async Task<IActionResult> Supprimer(Guid id, CancellationToken ct)
    {
        await _referentiel.SupprimerCategorieAsync(id, ct);
        return Ok(new { message = "Catégorie supprimée." });
    }
}

[Authorize]
[ApiController]
[Route("api/commerce/editeurs")]
public class EditeursController : ControllerBase
{
    private readonly IReferentielCommerceService _referentiel;

    public EditeursController(IReferentielCommerceService referentiel) => _referentiel = referentiel;

    [HttpGet]
    public async Task<IActionResult> Lister(CancellationToken ct)
        => Ok(await _referentiel.ListerEditeursAsync(ct));

    [HttpPost]
    [Authorize(Policy = "Commercial")]
    public async Task<IActionResult> Creer([FromBody] SaveEditeurRequest request, CancellationToken ct)
        => Ok(await _referentiel.CreerEditeurAsync(request, ct));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Commercial")]
    public async Task<IActionResult> Modifier(Guid id, [FromBody] SaveEditeurRequest request, CancellationToken ct)
        => Ok(await _referentiel.ModifierEditeurAsync(id, request, ct));

    [HttpPost("{id:guid}/actif")]
    [Authorize(Policy = "Commercial")]
    public async Task<IActionResult> DefinirActif(Guid id, [FromQuery] bool actif, CancellationToken ct)
        => Ok(await _referentiel.DefinirEditeurActifAsync(id, actif, ct));
}
