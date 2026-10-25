using Finama.Commerce.Core.DTOs;
using Finama.Commerce.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finama.API.Controllers;

[Authorize]
[ApiController]
[Route("api/commerce/clients")]
public class ClientController : ControllerBase
{
    private readonly IClientService _clients;

    public ClientController(IClientService clients) => _clients = clients;

    [HttpGet]
    public async Task<IActionResult> Rechercher([FromQuery] string? recherche, CancellationToken ct)
        => Ok(await _clients.RechercherAsync(recherche, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obtenir(Guid id, CancellationToken ct)
    {
        var detail = await _clients.ObtenirAsync(id, ct);
        return detail == null ? NotFound(new { message = "Client introuvable." }) : Ok(detail);
    }

    [HttpPost]
    public async Task<IActionResult> Creer([FromBody] SaveClientRequest request, CancellationToken ct)
        => Ok(await _clients.CreerAsync(request, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Modifier(Guid id, [FromBody] SaveClientRequest request, CancellationToken ct)
        => Ok(await _clients.ModifierAsync(id, request, ct));

    [HttpPost("{id:guid}/actif")]
    public async Task<IActionResult> DefinirActif(Guid id, [FromQuery] bool actif, CancellationToken ct)
        => Ok(await _clients.DefinirActifAsync(id, actif, ct));
}
