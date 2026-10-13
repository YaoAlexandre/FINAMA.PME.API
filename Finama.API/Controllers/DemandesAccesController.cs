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
public class DemandesAccesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuthService _authService;
    private readonly IEmailService _emailService;

    public DemandesAccesController(AppDbContext db, IAuthService authService, IEmailService emailService)
    {
        _db = db;
        _authService = authService;
        _emailService = emailService;
    }

    /// <summary>
    /// Endpoint public appelé par le formulaire de la landing page.
    /// </summary>
    [HttpPost("soumettre")]
    [AllowAnonymous]
    public async Task<IActionResult> SoumettreDemande([FromBody] CreerDemandeAccesRequest request)
    {
        var emailNormalise = request.Email.Trim().ToLower();

        // 1. Vérification si une demande ou un utilisateur existe déjà
        var existeDeja = await _db.DemandesAcces
            .AnyAsync(d => d.Email.ToLower() == emailNormalise && d.Statut == StatutDemandeAcces.EnAttente);

        if (existeDeja)
            return BadRequest(new { message = "Une demande d'accès avec cet e-mail est déjà en cours de traitement." });

        var pays = await _db.Pays.FindAsync(request.PaysId);
        if (pays is null)
            return BadRequest(new { message = "Le pays sélectionné est invalide." });

        // 2. Enregistrement de la demande
        var demande = new DemandeAcces
        {
            NomEntreprise = request.NomEntreprise.Trim(),
            NomContact = request.NomContact.Trim(),
            PrenomContact = request.PrenomContact.Trim(),
            Email = emailNormalise,
            Telephone = request.Telephone.Trim(),
            PaysId = request.PaysId,
            Message = request.Message,
            Statut = StatutDemandeAcces.EnAttente,
            DateDemande = DateTime.UtcNow
        };

        _db.DemandesAcces.Add(demande);
        await _db.SaveChangesAsync();

        // 3. Notification e-mail optionnelle à l'administrateur Finama (Fire-and-forget)
        _ = Task.Run(async () =>
        {
            try
            {
                // Mail automatique de confirmation au prospect
                await _emailService.SendOtpEmailByResendAsync(
                    demande.Email,
                    $"Bonjour {demande.PrenomContact}, nous avons bien reçu votre demande d'accès pour {demande.NomEntreprise}. Notre équipe l'examine."
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EMAIL DEMANDE ACCES ERROR] : {ex.Message}");
            }
        });

        return Ok(new { message = "Votre demande d'accès a été soumise avec succès." });
    }

    /// <summary>
    /// Liste des demandes reçues (Espace SuperAdmin Finama)
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> ObtenirDemandes([FromQuery] StatutDemandeAcces? statut)
    {
        var query = _db.DemandesAcces.Include(d => d.Pays).AsQueryable();

        if (statut.HasValue)
            query = query.Where(d => d.Statut == statut.Value);

        var resultats = await query
            .OrderByDescending(d => d.DateDemande)
            .Select(d => new DemandeAccesResponse(
                d.Id,
                d.NomEntreprise,
                d.NomContact,
                d.PrenomContact,
                d.Email,
                d.Telephone,
                d.Pays.Nom,
                d.Message,
                d.Statut.ToString(),
                d.DateDemande
            ))
            .ToListAsync();

        return Ok(resultats);
    }

    /// <summary>
    /// Traitement/Validation d'une demande par le SuperAdmin
    /// </summary>
    [HttpPost("{id:guid}/traiter")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> TraiterDemande(Guid id, [FromBody] TraiterDemandeAccesRequest request)
    {
        var demande = await _db.DemandesAcces.Include(d => d.Pays).FirstOrDefaultAsync(d => d.Id == id);

        if (demande is null)
            return NotFound(new { message = "Demande introuvable." });

        if (demande.Statut != StatutDemandeAcces.EnAttente)
            return BadRequest(new { message = "Cette demande a déjà été traitée." });

        if (request.Approuver)
        {
            demande.Statut = StatutDemandeAcces.Approuvee;
            demande.DateTraitement = DateTime.UtcNow;

            // Mot de passe temporaire généré
            string motDePasseTemporaire = $"Finama@{new Random().Next(10000, 99999)}";

            // Création automatique du Tenant et du compte Administrateur
            var registerDto = new RegisterTenantRequest(
                NomEntreprise: demande.NomEntreprise,
                NomAdministrateur: demande.NomContact,
                PrenomAdministrateur: demande.PrenomContact,
                Email: demande.Email,
                MotDePasse: motDePasseTemporaire,
                PaysId: demande.PaysId
            );

            await _authService.RegisterTenantAsync(registerDto);
            await _db.SaveChangesAsync();

            // Envoi des identifiants au client par mail
            _ = Task.Run(async () =>
            {
                await _emailService.SendOtpEmailByResendAsync(
                    demande.Email,
                    $"Votre compte Finama pour {demande.NomEntreprise} a été validé ! Votre mot de passe temporaire est : {motDePasseTemporaire}"
                );
            });

            return Ok(new { message = "Demande approuvée. Le compte de l'entreprise a été créé et les identifiants envoyés." });
        }
        else
        {
            demande.Statut = StatutDemandeAcces.Rejetee;
            demande.MotifRefus = request.MotifRefus;
            demande.DateTraitement = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return Ok(new { message = "La demande d'accès a été rejetée." });
        }
    }
}