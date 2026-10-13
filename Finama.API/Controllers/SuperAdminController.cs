using Finama.Core.DTOs;
using Finama.Core.Entities;
using Finama.Infrastructure.Data;
using Finama.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Finama.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin")]
public class SuperAdminController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ISuperAdminService _superAdminService;

    public SuperAdminController(AppDbContext db, ISuperAdminService superAdminService)
    {
        _db = db;
        _superAdminService = superAdminService;
    }

    // ===== Entreprises =====

    [HttpGet("tenants")]
    public async Task<IActionResult> ObtenirTousLesTenants()
        => Ok(await _superAdminService.ObtenirTenantsAsync());

    [HttpPut("tenants/{tenantId:guid}/statut")]
    public async Task<IActionResult> ChangerStatutTenant(Guid tenantId, [FromBody] ChangerStatutTenantRequest request)
    {
        try
        {
            await _superAdminService.ChangerStatutTenantAsync(tenantId, request.EstActif);
            return Ok(new { message = "Statut de l'entreprise mis à jour.", estActif = request.EstActif });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("tenants/{tenantId:guid}/abonnement")]
    public async Task<IActionResult> ActiverAbonnement(Guid tenantId, [FromBody] ActiverAbonnementRequest request)
    {
        try
        {
            await _superAdminService.ActiverAbonnementAsync(tenantId, request);
            return Ok(new { message = "Abonnement activé avec succès." });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("tenants/{tenantId:guid}/utilisateurs")]
    public async Task<IActionResult> ObtenirUtilisateursTenant(Guid tenantId)
        => Ok(await _superAdminService.ObtenirUtilisateursTenantAsync(tenantId));

    // ===== Utilisateurs =====

    [HttpPut("utilisateurs/{utilisateurId:guid}/statut")]
    public async Task<IActionResult> ChangerStatutUtilisateur(Guid utilisateurId, [FromQuery] bool actif)
    {
        try
        {
            await _superAdminService.ChangerStatutUtilisateurAsync(utilisateurId, actif);
            return Ok(new { message = "Statut de l'utilisateur mis à jour." });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("utilisateurs/{utilisateurId:guid}/reinitialiser-mot-de-passe")]
    public async Task<IActionResult> ReinitialiserMotDePasse(Guid utilisateurId)
    {
        try
        {
            await _superAdminService.ReinitialiserMotDePasseAsync(utilisateurId);
            return Ok(new { message = "Mot de passe réinitialisé et envoyé par email." });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("utilisateurs/{utilisateurId:guid}")]
    public async Task<IActionResult> SupprimerUtilisateur(Guid utilisateurId)
    {
        try
        {
            await _superAdminService.SupprimerUtilisateurAsync(utilisateurId);
            return Ok(new { message = "Utilisateur supprimé." });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    // ===== Statistiques =====

    [HttpGet("stats")]
    public async Task<IActionResult> ObtenirStatistiquesGlobales()
    {
        var totalTenants = await _db.Set<Tenant>().IgnoreQueryFilters().CountAsync(t => !t.IsDeleted);
        var tenantsActifs = await _db.Set<Tenant>().IgnoreQueryFilters().CountAsync(t => !t.IsDeleted && t.EstActif);
        var totalUtilisateurs = await _db.Set<Utilisateur>().IgnoreQueryFilters().CountAsync(u => !u.IsDeleted);

        return Ok(new
        {
            TotalTenants = totalTenants,
            TenantsActifs = tenantsActifs,
            TenantsSuspendus = totalTenants - tenantsActifs,
            TotalUtilisateurs = totalUtilisateurs
        });
    }

    // ===== Demandes d'accès =====

    [HttpGet("demandes-acces")]
    public async Task<IActionResult> GetDemandesAcces()
        => Ok(await _superAdminService.ObtenirDemandesAccesAsync());

    [HttpPost("demandes-acces/{id:guid}/approuver")]
    public async Task<IActionResult> ApprouverDemande(Guid id)
    {
        try
        {
            await _superAdminService.ApprouverDemandeAccesAsync(id);
            return Ok(new { Message = "Demande d'accès approuvée et entreprise créée avec succès." });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("demandes-acces/{id:guid}/refuser")]
    public async Task<IActionResult> RefuserDemande(Guid id)
    {
        try
        {
            await _superAdminService.RefuserDemandeAccesAsync(id);
            return Ok(new { Message = "Demande d'accès refusée." });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("tenants/{tenantId:guid}/initialiser-comptabilite")]
    public async Task<IActionResult> InitialiserComptabilite(Guid tenantId)
    {
        try
        {
            var repare = await _superAdminService.InitialiserPlanComptableTenantAsync(tenantId);
            return Ok(new { message = repare == 1 ? "Plan comptable initialisé." : "Rien à faire : tout existe déjà." });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost("tenants/initialiser-comptabilite")]
    public async Task<IActionResult> InitialiserComptabiliteTous()
    {
        var nb = await _superAdminService.InitialiserPlanComptableTousTenantsAsync();
        return Ok(new { message = $"{nb} entreprise(s) complétée(s)." });
    }
}