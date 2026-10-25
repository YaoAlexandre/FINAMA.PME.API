using Finama.Core.Validation;
using Finama.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Finama.API.Controllers;

/// <summary>Correction de la raison sociale d'une entreprise cliente par le SuperAdmin.</summary>
[ApiController]
[Route("api/superadmin/tenants")]
[Authorize(Roles = "SuperAdmin")]
public class SuperAdminEntrepriseController : ControllerBase
{
    private readonly AppDbContext _db;
    public SuperAdminEntrepriseController(AppDbContext db) => _db = db;

    public record RenommerRequest(string Nom);

    [HttpPut("{id:guid}/nom")]
    public async Task<IActionResult> Renommer(Guid id, [FromBody] RenommerRequest r)
    {
        string nom;
        try { nom = ValidationContact.Nom(r.Nom, "Nom de l'entreprise"); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }

        var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
        if (tenant is null) return NotFound(new { message = "Entreprise introuvable." });

        var doublon = await _db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id != id && !t.IsDeleted && t.Nom == nom);
        if (doublon) return BadRequest(new { message = "Une autre entreprise porte déjà ce nom." });

        var ancien = tenant.Nom;
        tenant.Nom = nom;
        await _db.SaveChangesAsync();
        return Ok(new { message = $"« {ancien} » s'appelle désormais « {nom} ». Ses utilisateurs verront le nouveau nom à leur prochaine connexion." });
    }
}
