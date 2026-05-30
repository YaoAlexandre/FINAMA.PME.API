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
            Adresse = entreprise.Adresse,
            NumeroFiscal = entreprise.NumeroFiscal,
            Telephone = entreprise.Telephone,
            BanqueBIC = entreprise.BanqueBIC,
            BanqueNom = entreprise.BanqueNom,
            BanqueRIB = entreprise.BanqueRIB,
            SlugUnique = entreprise.SlugUnique,
            Email = entreprise.Email ?? "contact@" + entreprise.SlugUnique + ".com",
            PaysNom = entreprise.Pays, // Ou entreprise.Pays si la propriété existe
            DeviseBase = entreprise.DeviseBase, // Ou entreprise.Devise (CFA)
            TauxTVA = entreprise.TauxTVA, // 18.0m Taux standard UEMOA par défaut
            PlanComptableCode = "SYSCOHADA",
        };

        return Ok(model);
    }

    [HttpPut("profil")]
    public async Task<IActionResult> UpdateProfil([FromBody] EntrepriseUpdateRequest request)
    {
        var tenantId = _tenantContext.TenantId;
        if (tenantId == null) return Unauthorized();

        var entreprise = await _db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId);

        if (entreprise == null) return NotFound();

        // Mise à jour des champs
        entreprise.Nom = request.Nom;
        entreprise.Adresse = request.Adresse;
        entreprise.Telephone = request.Telephone;
        entreprise.NumeroFiscal = request.NumeroFiscal;
        entreprise.BanqueNom = request.BanqueNom;
        entreprise.BanqueBIC = request.BanqueBIC;
        entreprise.TauxTVA = request.TauxTVA;

        await _db.SaveChangesAsync();

        return Ok(new { message = "Informations mises à jour avec succès." });
    }
}