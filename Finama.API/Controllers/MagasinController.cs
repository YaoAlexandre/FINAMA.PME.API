using Finama.Commerce.Core.Domain;
using Finama.Commerce.Core.DTOs;
using Finama.Commerce.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finama.API.Controllers;

[Authorize]
[ApiController]
[Route("api/commerce/magasins")]
public class MagasinController : ControllerBase
{
    private readonly IMagasinService _magasinService;

    public MagasinController(IMagasinService magasinService)
    {
        _magasinService = magasinService;
    }

    [HttpGet]
    public async Task<IActionResult> GetMagasins(CancellationToken ct)
    {
        var magasins = await _magasinService.GetMagasinsAsync(ct);
        return Ok(magasins);
    }

    [HttpPost]
    public async Task<IActionResult> CreerMagasin([FromBody] CreerMagasinRequest request, CancellationToken ct)
    {
        var magasin = await _magasinService.CreerMagasinAsync(request, ct);
        return Ok(magasin);
    }

    [HttpPost("{id:guid}/principal")]
    public async Task<IActionResult> DefinirPrincipal(Guid id, CancellationToken ct)
    {
        await _magasinService.DefinirCommePrincipalAsync(id, ct);
        return Ok(new { Message = "Magasin défini comme principal avec succès." });
    }
}