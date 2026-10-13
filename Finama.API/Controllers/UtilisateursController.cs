using System.Security.Claims;
using System.Security.Cryptography;
using Finama.Core.DTOs;
using Finama.Core.Entities;
using Finama.Infrastructure.Data;
using Finama.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Finama.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "AdminTenant")] // Seul l'admin de l'entreprise gère ses utilisateurs
public class UtilisateursController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IEmailService _emailService;

    public UtilisateursController(AppDbContext db, ITenantContext tenantContext, IEmailService emailService)
    {
        _db = db;
        _tenantContext = tenantContext;
        _emailService = emailService;
    }

    [HttpPost]
    public async Task<IActionResult> CreerCollaborateur([FromBody] CreerCollaborateurDto model)
    {
        var email = model.Email.Trim().ToLower();

        var existe = await _db.Utilisateurs.IgnoreQueryFilters().AnyAsync(u => u.Email.ToLower() == email);
        if (existe) return BadRequest(new { message = "Cette adresse email est déjà associée à un compte." });

        _db.Utilisateurs.Add(new Utilisateur
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId.Value,
            Nom = model.Nom,
            Prenom = model.Prenom,
            Email = email,
            MotDePasseHash = BCrypt.Net.BCrypt.HashPassword(model.MotDePasse),
            Role = (RoleUtilisateur)model.Role,
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
        if (!actif)
        {
            // Coupe les sessions ouvertes : l'utilisateur est déconnecté au prochain rafraîchissement
            user.RefreshToken = null;
            user.RefreshTokenExpireAt = null;
        }
        await _db.SaveChangesAsync();

        return Ok(new { message = actif ? "Accès réactivé." : "Accès suspendu." });
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
