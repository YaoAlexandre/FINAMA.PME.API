using Finama.API.Middleware;
using Finama.Core.Entities;
using Finama.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Finama.API.Controllers;

/// <summary>
/// Modules par entreprise.
///  • GET  api/entreprise/modules                      : modules de l'entreprise connectée (menus de l'application)
///  • GET  api/superadmin/tenants/{id}/modules         : SuperAdmin
///  • PUT  api/superadmin/tenants/{id}/modules         : SuperAdmin, effet immédiat (cache vidé)
/// </summary>
[ApiController]
[Authorize]
public class ModulesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;

    public ModulesController(AppDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    [HttpGet("api/entreprise/modules")]
    public async Task<IActionResult> MesModules(CancellationToken ct)
    {
        // Compte plateforme (SuperAdmin hors mode support) : aucune donnée d'entreprise
        if (!Guid.TryParse(User.FindFirst("tenant_id")?.Value, out var tenantId))
            return Ok(Dto(ModuleFinama.Aucun, sansEntreprise: true));

        var modules = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => t.Modules)
            .FirstOrDefaultAsync(ct);

        return Ok(Dto(modules));
    }

    [HttpGet("api/superadmin/tenants/{tenantId:guid}/modules")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Lire(Guid tenantId, CancellationToken ct)
    {
        var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        return tenant is null ? NotFound(new { message = "Entreprise introuvable." }) : Ok(Dto(tenant.Modules));
    }

    [HttpPut("api/superadmin/tenants/{tenantId:guid}/modules")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Modifier(Guid tenantId, [FromBody] ModifierModulesRequest request, CancellationToken ct)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null) return NotFound(new { message = "Entreprise introuvable." });

        var modules = (ModuleFinama)request.Modules & ModuleFinama.Tous;   // ignore les bits inconnus
        if (modules == ModuleFinama.Aucun)
            return BadRequest(new { message = "Activez au moins un module. Pour couper l'accès, suspendez l'entreprise." });

        tenant.Modules = modules;
        await _db.SaveChangesAsync(ct);
        _cache.Remove(ModulesMiddleware.CleCache(tenantId));   // effet immédiat sur l'API

        return Ok(new { message = $"Modules de « {tenant.Nom} » mis à jour.", modules = (int)modules });
    }

    private static object Dto(ModuleFinama m, bool sansEntreprise = false) => new
    {
        modules = (int)m,
        sansEntreprise,
        comptabilite = m.HasFlag(ModuleFinama.Comptabilite),
        gestionCommerciale = m.HasFlag(ModuleFinama.GestionCommerciale),
        commerce = m.HasFlag(ModuleFinama.Commerce),
    };

    public record ModifierModulesRequest(int Modules);
}
