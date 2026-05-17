using Finama.Core.Entities;
using Finama.Infrastructure.Data;
using Finama.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Finama.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "AdminTenant,Comptable,SuperAdmin")] // Sécurisé par tes rôles natifs !
public class EntrepriseController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;

    public EntrepriseController(AppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    [HttpGet("profil")]
    public async Task<IActionResult> GetProfil()
    {
        // 1. Récupérer l'ID de l'entreprise depuis le jeton de sécurité
        var tenantId = _tenantContext.TenantId;
        if (tenantId == null)
        {
            return Unauthorized("Contexte d'entreprise introuvable.");
        }

        // 2. Chercher les informations de l'entreprise dans ta table Tenants
        // Utilise IgnoreQueryFilters au besoin si ton entité Tenant elle-même subit son propre filtre.
        var entreprise = await _db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId);

        if (entreprise == null)
        {
            return NotFound("Entreprise non enregistrée sur la plateforme.");
        }

        // 3. Mapper vers les propriétés attendues par l'UI de ton application
        // Note : Adapte les fallbacks (comme "Togo", "XOF", 18) selon les vraies colonnes de ta table Tenant
        var model = new
        {
            Id = entreprise.Id,
            Nom = entreprise.Nom,
            SlugUnique = entreprise.SlugUnique,
            Email = entreprise.Email ?? "contact@" + entreprise.SlugUnique + ".com",
            PaysNom = "Togo", // Ou entreprise.Pays si la propriété existe
            DeviseBase = "XOF", // Ou entreprise.Devise (CFA)
            TauxTVA = 18.0m, // Taux standard UEMOA par défaut
            PlanComptableCode = "SYSCOHADA"
        };

        return Ok(model);
    }
}