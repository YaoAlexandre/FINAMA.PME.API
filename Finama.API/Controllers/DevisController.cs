using Finama.Core.DTOs;
using Finama.Infrastructure.Services;
using Finama.Infrastructure.Services.Commercials;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Finama.API.Controllers;

[ApiController]
[Route("api/devis")]
[Authorize(Policy = "Commercial, AdminTenant")]
public class DevisController : ControllerBase
{
    private readonly IDevisService _service;
    private Guid UserId ;
    public DevisController(IDevisService service)
    {
        _service = service;
        UserId = ObtenirUtilisateurId();
    }

    

    [HttpGet]
    public async Task<IActionResult> GetMesDevis()
    {
        var devis = await _service.GetMesDevisAsync(UserId);
        return Ok(devis);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var devis = await _service.GetByIdAsync(id, UserId);
            return Ok(devis);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Creer(CreerDevisRequest request)
    {
        try
        {
            var devis = await _service.CreerAsync(request, UserId);
            return CreatedAtAction(nameof(GetById), new { id = devis.Id }, devis);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Modifier(Guid id, CreerDevisRequest request)
    {
        try
        {
            var devis = await _service.ModifierAsync(id, request, UserId);
            return Ok(devis);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("{id:guid}/statut")]
    public async Task<IActionResult> MettreAJourStatut(Guid id, MettreAJourStatutDevisRequest request)
    {
        try
        {
            var devis = await _service.MettreAJourStatutAsync(id, request, UserId);
            return Ok(devis);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/convertir")]
    public async Task<IActionResult> ConvertirEnFacture(Guid id)
    {
        try
        {
            var factureId = await _service.ConvertirEnFactureAsync(id, UserId);
            return Ok(new { factureId });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Supprimer(Guid id)
    {
        try
        {
            await _service.SupprimerAsync(id, UserId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("debug-claims")]
    [Authorize]
    public IActionResult DebugClaims()
    {
        var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();
        return Ok(claims);
    }

    private Guid ObtenirUtilisateurId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                 ?? User.FindFirst("sub")?.Value;

        return Guid.TryParse(claim, out var id)
            ? id
            : throw new UnauthorizedAccessException("Utilisateur non identifié.");
    }
}