using Finama.Core.DTOs;
using Finama.Core.Entities;
using Finama.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Finama.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "AdminTenant")] // 🌟 Seul l'admin de l'entreprise peut créer des utilisateurs !
public class UtilisateursController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext; // Ton service d'extraction du tenant depuis le JWT

    public UtilisateursController(AppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    [HttpPost]
    public async Task<IActionResult> CreerCollaborateur([FromBody] CreerCollaborateurDto model)
    {
        // 1. Vérifier si l'email existe déjà globalement
        var existe = await _db.Utilisateurs.IgnoreQueryFilters()
            .AnyAsync(u => u.Email == model.Email.ToLower());

        if (existe) return BadRequest(new { message = "Cette adresse email est déjà associée à un compte." });

        // 2. Créer l'entité
        var nouvelUtilisateur = new Utilisateur
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId.Value, // 🌟 Sécurité : On lie de force le collaborateur au même domaine que l'admin
            Nom = model.Nom,
            Prenom = model.Prenom,
            Email = model.Email.ToLower(),
            MotDePasseHash = BCrypt.Net.BCrypt.HashPassword(model.MotDePasse),
            Role = (RoleUtilisateur)model.Role, // Cast l'entier du formulaire vers ton Enum (0=Admin, 1=Comptable)
            EstActif = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Utilisateurs.Add(nouvelUtilisateur);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Créé avec succès" });
    }

    [HttpGet]
    public async Task<IActionResult> GetUtilisateurs()
    {
        // Grâce au Query Filter global, cela ne listera que les utilisateurs du Tenant de l'admin connecté
        var users = await _db.Utilisateurs
            .Select(u => new { u.Id, u.Nom, u.Prenom, u.Email, Role = u.Role.ToString(), u.EstActif })
            .ToListAsync();

        return Ok(users);
    }
}
