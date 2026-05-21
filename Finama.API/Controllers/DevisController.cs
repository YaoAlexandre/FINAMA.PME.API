using Finama.Core.DTOs;
using Finama.Infrastructure.Services.Commercials;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;

namespace Finama.API.Controllers;

[ApiController]
[Route("api/devis")]
[Authorize(Policy = "Commercial")]
public class DevisController : ControllerBase
{
    private readonly IDevisService _service;

    public DevisController(IDevisService service) => _service = service;

    private Guid UserId => Guid.Parse(
        User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

    [HttpGet]
    public async Task<IActionResult> GetMesDevis()
        => Ok(await _service.GetMesDevisAsync(UserId));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id, UserId));

    [HttpPost]
    public async Task<IActionResult> Creer(CreerDevisRequest request)
    {
        var devis = await _service.CreerAsync(request, UserId);
        return CreatedAtAction(nameof(GetById), new { id = devis.Id }, devis);
    }

    [HttpPatch("{id}/statut")]
    public async Task<IActionResult> MettreAJourStatut(Guid id, MettreAJourStatutDevisRequest request)
        => Ok(await _service.MettreAJourStatutAsync(id, request, UserId));

    [HttpPost("{id}/convertir")]
    public async Task<IActionResult> ConvertirEnFacture(Guid id)
    {
        var factureId = await _service.ConvertirEnFactureAsync(id, UserId);
        return Ok(new { factureId });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Supprimer(Guid id)
    {
        await _service.SupprimerAsync(id, UserId);
        return NoContent();
    }
}
