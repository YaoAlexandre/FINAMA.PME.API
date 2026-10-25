using System.Security.Claims;
using System.Security.Cryptography;
using Finama.Core.DTOs;
using Finama.Core.Entities;
using Finama.Core.Validation;
using Finama.Infrastructure.Data;
using Finama.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Finama.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = Policies.Utilisateurs)] // permission « Gérer les utilisateurs »
public class UtilisateursController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IEmailService _emailService;
    private readonly IPermissionsService _permissions;

    public UtilisateursController(AppDbContext db, ITenantContext tenantContext, IEmailService emailService, IPermissionsService permissions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _emailService = emailService;
        _permissions = permissions;
    }

    /// <summary>Rôles qu'un administrateur d'entreprise peut attribuer (jamais SuperAdmin).</summary>
    private static readonly RoleUtilisateur[] RolesAttribuables =
    [
        RoleUtilisateur.AdminTenant, RoleUtilisateur.Comptable, RoleUtilisateur.Collaborateur, RoleUtilisateur.Lecture,
        RoleUtilisateur.Commercial, RoleUtilisateur.Caissier, RoleUtilisateur.Magasinier
    ];

    private const int MotDePasseLongueurMin = 10;

    [HttpPost]
    public async Task<IActionResult> CreerCollaborateur([FromBody] CreerCollaborateurDto model)
    {
        if (_tenantContext.TenantId is not { } tenantId)
            return BadRequest(new { message = "Aucune entreprise associée à cette session." });

        // 🔒 Profil prédéfini : liste fermée (une valeur forgée comme 99 = SuperAdmin est refusée) ; ou profil personnalisé
        var (role, profilId, erreurAcces) = await ResoudreAccesAsync(model.Role, model.ProfilId);
        if (erreurAcces is not null) return BadRequest(new { message = erreurAcces });

        string nom, prenom, email;
        try
        {
            nom = ValidationContact.Nom(model.Nom, "Nom");
            prenom = ValidationContact.Nom(model.Prenom, "Prénom");
            email = ValidationContact.Email(model.Email) ?? throw new ArgumentException("L'adresse e-mail est obligatoire.");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        if (string.IsNullOrWhiteSpace(model.MotDePasse) || model.MotDePasse.Length < MotDePasseLongueurMin)
            return BadRequest(new { message = $"Le mot de passe temporaire doit contenir au moins {MotDePasseLongueurMin} caractères." });

        var existe = await _db.Utilisateurs.IgnoreQueryFilters().AnyAsync(u => u.Email.ToLower() == email);
        if (existe) return BadRequest(new { message = "Cette adresse email est déjà associée à un compte." });

        _db.Utilisateurs.Add(new Utilisateur
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Nom = nom,
            Prenom = prenom,
            Email = email,
            MotDePasseHash = BCrypt.Net.BCrypt.HashPassword(model.MotDePasse),
            Role = role,
            ProfilId = profilId,
            EstActif = true,
            DoitChangerMotDePasse = true, // le mot de passe saisi par l'admin est temporaire
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        return Ok(new { message = "Créé avec succès" });
    }

    [HttpGet]
    public async Task<IActionResult> GetUtilisateurs()
    {
        // Le filtre global limite la liste aux utilisateurs du tenant de l'admin connecté
        var users = await _db.Utilisateurs
            .Select(u => new { u.Id, u.Nom, u.Prenom, u.Email, Role = u.Role.ToString(), u.EstActif })
            .ToListAsync();

        return Ok(users);
    }

    // PUT api/Utilisateurs/{id}/statut   (corps JSON : true = actif, false = suspendu)
    [HttpPut("{id:guid}/statut")]
    public async Task<IActionResult> ChangerStatut(Guid id, [FromBody] bool actif)
    {
        var (user, erreur) = await ObtenirCibleAsync(id);
        if (erreur is not null) return erreur;

        if (!actif && user!.Role == RoleUtilisateur.AdminTenant)
        {
            var autreAdmin = await _db.Utilisateurs.AnyAsync(u =>
                u.Id != user.Id && u.TenantId == user.TenantId &&
                u.Role == RoleUtilisateur.AdminTenant && u.EstActif);
            if (!autreAdmin)
                return BadRequest(new { message = "Impossible de suspendre le dernier administrateur de l'entreprise." });
        }

        user!.EstActif = actif;
        _permissions.Invalider(user.TenantId);
        if (!actif)
        {
            // Coupe les sessions ouvertes : l'utilisateur est déconnecté au prochain rafraîchissement
            user.RefreshToken = null;
            user.RefreshTokenExpireAt = null;
        }
        await _db.SaveChangesAsync();

        return Ok(new { message = actif ? "Accès réactivé." : "Accès suspendu." });
    }

    // GET api/Utilisateurs : liste avec le profil (prédéfini ou personnalisé)
    [HttpGet("acces")]
    public async Task<IActionResult> ListeAcces()
    {
        var profils = await _db.Set<ProfilAcces>().AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Nom);
        var users = await _db.Utilisateurs.AsNoTracking()
            .Select(u => new { u.Id, u.Nom, u.Prenom, u.Email, Role = u.Role.ToString(), RoleValeur = (int)u.Role, u.ProfilId, u.EstActif })
            .ToListAsync();
        return Ok(users.Select(u => new
        {
            u.Id, u.Nom, u.Prenom, u.Email, u.Role, u.RoleValeur, u.ProfilId, u.EstActif,
            ProfilNom = u.ProfilId is { } p ? profils.GetValueOrDefault(p) : null
        }));
    }

    // PUT api/Utilisateurs/{id}/acces   { role, profilId }
    /// <summary>Attribue un profil prédéfini (role) ou personnalisé (profilId). Effet immédiat sur les droits ; reconnexion pour les menus.</summary>
    [HttpPut("{id:guid}/acces")]
    public async Task<IActionResult> ChangerAcces(Guid id, [FromBody] ModifierAccesDto model)
    {
        var (role, profilId, erreurAcces) = await ResoudreAccesAsync(model.Role, model.ProfilId);
        if (erreurAcces is not null) return BadRequest(new { message = erreurAcces });

        var (user, erreur) = await ObtenirCibleAsync(id);
        if (erreur is not null) return erreur;

        // On ne retire pas le rôle du dernier administrateur actif
        if (user!.Role == RoleUtilisateur.AdminTenant && role != RoleUtilisateur.AdminTenant)
        {
            var autreAdmin = await _db.Utilisateurs.AnyAsync(u =>
                u.Id != user.Id && u.TenantId == user.TenantId && u.Role == RoleUtilisateur.AdminTenant && u.EstActif);
            if (!autreAdmin)
                return BadRequest(new { message = "Impossible : c'est le dernier administrateur de l'entreprise." });
        }

        user.Role = role;
        user.ProfilId = profilId;
        user.RefreshToken = null;          // reconnexion : jeton et menus à jour
        user.RefreshTokenExpireAt = null;
        await _db.SaveChangesAsync();
        _permissions.Invalider(user.TenantId);

        return Ok(new { message = $"Accès modifié. {user.Prenom} devra se reconnecter pour voir ses nouveaux menus." });
    }

    /// <summary>
    /// Profil personnalisé : rôle technique « Collaborateur » (neutre), les droits viennent du profil.
    /// Profil prédéfini : le rôle lui-même, dans la liste autorisée.
    /// </summary>
    private async Task<(RoleUtilisateur Role, Guid? ProfilId, string? Erreur)> ResoudreAccesAsync(int role, Guid? profilId)
    {
        if (profilId is { } pid)
        {
            var existe = await _db.Set<ProfilAcces>().AnyAsync(p => p.Id == pid);   // filtre entreprise automatique
            return existe ? (RoleUtilisateur.Collaborateur, pid, null) : (default, null, "Profil introuvable.");
        }

        if (!Enum.IsDefined(typeof(RoleUtilisateur), role) || !RolesAttribuables.Contains((RoleUtilisateur)role))
            return (default, null, "Profil non autorisé.");

        return ((RoleUtilisateur)role, null, null);
    }

    public record IdentiteRequest(string Nom, string Prenom, string Email);

    // PUT api/Utilisateurs/{id}/identite  — corriger le nom, le prénom ou l'e-mail d'un utilisateur (y compris soi-même)
    [HttpPut("{id:guid}/identite")]
    public async Task<IActionResult> ModifierIdentite(Guid id, [FromBody] IdentiteRequest r)
    {
        var user = await _db.Utilisateurs.FirstOrDefaultAsync(u => u.Id == id && u.TenantId == _tenantContext.TenantId);
        if (user is null) return NotFound(new { message = "Utilisateur introuvable." });
        if (user.Role == RoleUtilisateur.SuperAdmin) return BadRequest(new { message = "Ce compte ne se modifie pas ici." });

        string nom, prenom, email;
        try
        {
            nom = ValidationContact.Nom(r.Nom, "Nom");
            prenom = ValidationContact.Nom(r.Prenom, "Prénom");
            email = ValidationContact.Email(r.Email) ?? throw new ArgumentException("L'adresse e-mail est obligatoire.");
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }

        var emailChange = !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase);
        if (emailChange && await _db.Utilisateurs.IgnoreQueryFilters().AnyAsync(u => u.Id != id && u.Email.ToLower() == email))
            return BadRequest(new { message = "Cette adresse e-mail est déjà utilisée par un autre compte." });

        user.Nom = nom;
        user.Prenom = prenom;
        user.Email = email;
        if (emailChange)
        {
            // Nouvelle adresse = nouvel identifiant de connexion : sessions coupées, appareils à revalider par code
            user.RefreshToken = null;
            user.RefreshTokenExpireAt = null;
            var appareils = await _db.AppareilsConfiance.Where(a => a.UtilisateurId == user.Id).ToListAsync();
            _db.AppareilsConfiance.RemoveRange(appareils);
        }
        await _db.SaveChangesAsync();

        return Ok(new { message = emailChange
            ? $"Informations mises à jour. {prenom} se connectera désormais avec {email}."
            : "Informations mises à jour." });
    }

    // POST api/Utilisateurs/{id}/reinitialiser-mot-de-passe
    [HttpPost("{id:guid}/reinitialiser-mot-de-passe")]
    public async Task<IActionResult> ReinitialiserMotDePasse(Guid id)
    {
        var (user, erreur) = await ObtenirCibleAsync(id);
        if (erreur is not null) return erreur;

        var motDePasseTemp = "Finama@" + RandomNumberGenerator.GetInt32(100000, 1000000);

        user!.MotDePasseHash = BCrypt.Net.BCrypt.HashPassword(motDePasseTemp);
        user.DoitChangerMotDePasse = true;
        user.RefreshToken = null;
        user.RefreshTokenExpireAt = null;
        user.OtpCode = null;
        user.OtpExpireAt = null;
        await _db.SaveChangesAsync();

        var emailEnvoye = true;
        try
        {
            await _emailService.SendEmailAsync(
                user.Email,
                "Réinitialisation de votre mot de passe Finama",
                $"<p>Bonjour {user.Prenom},</p><p>Votre administrateur a réinitialisé votre mot de passe.</p><p>Mot de passe temporaire : <b>{motDePasseTemp}</b></p><p>Vous devrez le modifier à votre prochaine connexion.</p>");
        }
        catch (Exception ex)
        {
            emailEnvoye = false;
            System.Diagnostics.Debug.WriteLine($"[EMAIL WARNING] : {ex.Message}");
        }

        // Si l'email échoue, l'admin reçoit le mot de passe une seule fois pour le transmettre lui-même
        return Ok(new
        {
            message = emailEnvoye
                ? $"Un mot de passe temporaire a été envoyé à {user.Email}."
                : "Mot de passe réinitialisé, mais l'email n'a pas pu être envoyé.",
            emailEnvoye,
            motDePasseTemporaire = emailEnvoye ? null : motDePasseTemp
        });
    }

    // Utilisateur ciblé : doit appartenir au tenant de l'admin, et ne peut pas être l'admin lui-même
    private async Task<(Utilisateur? User, IActionResult? Erreur)> ObtenirCibleAsync(Guid id)
    {
        var user = await _db.Utilisateurs.FirstOrDefaultAsync(u => u.Id == id && u.TenantId == _tenantContext.TenantId);
        if (user is null)
            return (null, NotFound(new { message = "Utilisateur introuvable." }));

        var courant = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (Guid.TryParse(courant, out var courantId) && courantId == user.Id)
            return (null, BadRequest(new { message = "Action impossible sur votre propre compte. Utilisez « Changer le mot de passe » pour le vôtre." }));

        return (user, null);
    }
}
