using Finama.Commerce.Core.DTOs;
using Finama.Commerce.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finama.API.Controllers;

[Authorize]
[ApiController]
[Route("api/commerce/parametres")]
public class ParametresController : ControllerBase
{
    private readonly IParametresCommerceService _parametres;

    public ParametresController(IParametresCommerceService parametres) => _parametres = parametres;

    /// <summary>Lisible par tous les utilisateurs : les écrans s'adaptent aux options activées.</summary>
    [HttpGet]
    public async Task<IActionResult> Obtenir(CancellationToken ct)
        => Ok(await _parametres.ObtenirAsync(ct));

    [HttpPut]
    [Authorize(Policy = "AdminTenant")]
    public async Task<IActionResult> Sauvegarder([FromBody] ParametresCommerceDto dto, CancellationToken ct)
        => Ok(await _parametres.SauvegarderAsync(dto, ct));

    [HttpGet("profils")]
    public IActionResult Profils() => Ok(_parametres.ListerProfils());

    [HttpPost("appliquer-profil")]
    [Authorize(Policy = "AdminTenant")]
    public async Task<IActionResult> AppliquerProfil([FromBody] AppliquerProfilRequest request, CancellationToken ct)
        => Ok(await _parametres.AppliquerProfilAsync(request.Code, ct));
}
