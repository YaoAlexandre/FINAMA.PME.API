using Finama.Core.Entities;
using Finama.Core.Securite;
using Finama.Core.Validation;
using Finama.Infrastructure.Data;
using Finama.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Finama.API.Controllers;

/// <summary>
/// Profils &amp; permissions.
///  • GET  api/moi/permissions        : permissions de l'utilisateur connecté (menus de l'application)
///  • GET  api/profils/catalogue      : permissions par module + profils prédéfinis (écran d'administration)
///  • CRUD api/profils                : profils personnalisés de l'entreprise
/// </summary>
[ApiController]
[Authorize]
public class ProfilsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPermissionsService _permissions;
    private readonly ITenantContext _tenant;

    public ProfilsController(AppDbContext db, IPermissionsService permissions, ITenantContext tenant)
    {
        _db = db;
        _permissions = permissions;
        _tenant = tenant;
    }

    [HttpGet("api/moi/permissions")]
    public async Task<IActionResult> MesPermissions()
        => Ok(new { permissions = (await _permissions.DeAsync(User)).Order().ToArray() });

    [HttpGet("api/profils/catalogue")]
    [Authorize(Policy = Policies.Utilisateurs)]
    public async Task<IActionResult> Catalogue()
    {
        // Seuls les modules de l'abonnement sont proposés
        var modules = _tenant.TenantId is { } t
            ? await _db.Tenants.AsNoTracking().Where(x => x.Id == t).Select(x => x.Modules).FirstOrDefaultAsync()
            : ModuleFinama.Tous;

        var groupes = PermissionsFinama.Groupes
            .Where(g => g.ModuleAbonnement is null || modules.HasFlag(g.ModuleAbonnement.Value))
            .Select(g => new
            {
                g.Module, g.Titre, g.Icone,
                Permissions = PermissionsFinama.Toutes.Where(p => p.Module == g.Module)
                    .Select(p => new { p.Code, p.Libelle, p.Description })
            });

        var predefinis = PermissionsFinama.Predefinis.Select(p => new
        {
            Role = (int)p.Role, Code = p.Role.ToString(), p.Libelle, p.Description, p.Permissions
        });

        return Ok(new { groupes, predefinis });
    }

    [HttpGet("api/profils")]
    [Authorize(Policy = Policies.Utilisateurs)]
    public async Task<IActionResult> Lister()
    {
        var profils = await _db.Set<ProfilAcces>().AsNoTracking().OrderBy(p => p.Nom).ToListAsync();
        var ids = profils.Select(p => (Guid?)p.Id).ToList();
        var nbUtilisateurs = await _db.Utilisateurs.AsNoTracking()
            .Where(u => ids.Contains(u.ProfilId))
            .GroupBy(u => u.ProfilId)
            .Select(g => new { g.Key, Nb = g.Count() })
            .ToDictionaryAsync(x => x.Key!.Value, x => x.Nb);

        return Ok(profils.Select(p => new
        {
            p.Id, p.Nom, p.Description,
            Permissions = PermissionsFinama.Lire(p.Permissions).Order().ToArray(),
            NbUtilisateurs = nbUtilisateurs.GetValueOrDefault(p.Id)
        }));
    }

    [HttpPost("api/profils")]
    [Authorize(Policy = Policies.Utilisateurs)]
    public async Task<IActionResult> Creer([FromBody] ProfilRequest request)
    {
        if (_tenant.TenantId is not { } tenantId) return BadRequest(new { message = "Aucune entreprise associée à cette session." });
        var (nom, erreur) = await ValiderAsync(request, null);
        if (erreur is not null) return BadRequest(new { message = erreur });

        var profil = new ProfilAcces
        {
            TenantId = tenantId,
            Nom = nom!,
            Description = ValidationContact.Texte(request.Description, 250, "Description"),
            Permissions = PermissionsFinama.Serialiser(request.Permissions)
        };
        _db.Set<ProfilAcces>().Add(profil);
        await _db.SaveChangesAsync();
        return Ok(new { profil.Id, message = $"Profil « {profil.Nom} » créé." });
    }

    [HttpPut("api/profils/{id:guid}")]
    [Authorize(Policy = Policies.Utilisateurs)]
    public async Task<IActionResult> Modifier(Guid id, [FromBody] ProfilRequest request)
    {
        var profil = await _db.Set<ProfilAcces>().FirstOrDefaultAsync(p => p.Id == id);
        if (profil is null) return NotFound(new { message = "Profil introuvable." });

        var (nom, erreur) = await ValiderAsync(request, id);
        if (erreur is not null) return BadRequest(new { message = erreur });

        profil.Nom = nom!;
        profil.Description = ValidationContact.Texte(request.Description, 250, "Description");
        profil.Permissions = PermissionsFinama.Serialiser(request.Permissions);
        await _db.SaveChangesAsync();
        _permissions.Invalider(profil.TenantId);   // effet immédiat pour les utilisateurs de ce profil

        return Ok(new { message = $"Profil « {profil.Nom} » mis à jour : les droits s'appliquent immédiatement." });
    }

    [HttpDelete("api/profils/{id:guid}")]
    [Authorize(Policy = Policies.Utilisateurs)]
    public async Task<IActionResult> Supprimer(Guid id)
    {
        var profil = await _db.Set<ProfilAcces>().FirstOrDefaultAsync(p => p.Id == id);
        if (profil is null) return NotFound(new { message = "Profil introuvable." });

        var utilise = await _db.Utilisateurs.CountAsync(u => u.ProfilId == id);
        if (utilise > 0)
            return BadRequest(new { message = $"Ce profil est attribué à {utilise} utilisateur(s) : changez d'abord leur profil." });

        profil.IsDeleted = true;
        await _db.SaveChangesAsync();
        return Ok(new { message = "Profil supprimé." });
    }

    private async Task<(string? Nom, string? Erreur)> ValiderAsync(ProfilRequest r, Guid? id)
    {
        string nom;
        try { nom = ValidationContact.Nom(r.Nom, "Nom du profil"); }
        catch (ArgumentException ex) { return (null, ex.Message); }
        if (nom.Length > 80) return (null, "Nom du profil : 80 caractères au maximum.");

        if (r.Permissions is not { Count: > 0 } || !r.Permissions.Any(PermissionsFinama.Codes.Contains))
            return (null, "Cochez au moins une permission.");

        if (PermissionsFinama.Predefinis.Any(p => string.Equals(p.Libelle, nom, StringComparison.OrdinalIgnoreCase)))
            return (null, "Ce nom est celui d'un profil prédéfini : choisissez-en un autre.");

        var doublon = await _db.Set<ProfilAcces>().AnyAsync(p => p.Nom == nom && p.Id != id);
        return doublon ? (null, "Un profil porte déjà ce nom.") : (nom, null);
    }

    public record ProfilRequest(string Nom, string? Description, List<string> Permissions);
}
