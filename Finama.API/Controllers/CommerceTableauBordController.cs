using Finama.Commerce.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finama.API.Controllers;

/// <summary>
/// Synthèse du module Commerce (accueil des modules).
/// Distinct de TableauBordController (Finance, route api/TableauBord) : nom de classe et route explicites pour éviter tout conflit.
/// </summary>
[Authorize]
[ApiController]
[Route("api/commerce/tableau-bord")]
public class CommerceTableauBordController : ControllerBase
{
    private readonly ITableauBordCommerceService _tableauBord;

    public CommerceTableauBordController(ITableauBordCommerceService tableauBord) => _tableauBord = tableauBord;

    [HttpGet]
    public async Task<IActionResult> Obtenir(CancellationToken ct)
        => Ok(await _tableauBord.ObtenirAsync(ct));
}
