using Finama.Core.DTOs;
using Finama.Core.Entities;
using Finama.Infrastructure.Data;
using Finama.Infrastructure.Seeds;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Finama.Infrastructure.Services;

public interface ISuperAdminService
{
    // Demandes d'accès
    Task<List<DemandeAccesResponseDto>> ObtenirDemandesAccesAsync();
    Task ApprouverDemandeAccesAsync(Guid demandeId);
    Task RefuserDemandeAccesAsync(Guid demandeId);

    // Entreprises
    Task<List<TenantAdminDto>> ObtenirTenantsAsync();
    Task ChangerStatutTenantAsync(Guid tenantId, bool actif);
    Task ActiverAbonnementAsync(Guid tenantId, ActiverAbonnementRequest request);

    // Utilisateurs
    Task<List<UtilisateurAdminDto>> ObtenirUtilisateursTenantAsync(Guid tenantId);
    Task ChangerStatutUtilisateurAsync(Guid utilisateurId, bool actif);
    Task ReinitialiserMotDePasseAsync(Guid utilisateurId);
    Task SupprimerUtilisateurAsync(Guid utilisateurId);
    // Rattrapage
    Task<int> InitialiserPlanComptableTenantAsync(Guid tenantId);
    Task<int> InitialiserPlanComptableTousTenantsAsync();
}

public class SuperAdminService : ISuperAdminService
{
    private readonly AppDbContext _db;
    private readonly IEmailService _emailService;

    public SuperAdminService(AppDbContext db, IEmailService emailService)
    {
        _db = db;
        _emailService = emailService;
    }

    public async Task<List<DemandeAccesResponseDto>> ObtenirDemandesAccesAsync()
    {
        return await _db.Set<DemandeAcces>()
            .OrderByDescending(d => d.DateDemande)
            .Select(d => new DemandeAccesResponseDto(
                d.Id,
                d.NomEntreprise,
                d.NomContact,
                d.PrenomContact,
                d.Email,
                d.Telephone,
                d.PaysId,
                d.Message,
                (int)d.Statut,
                d.DateDemande
            ))
            .ToListAsync();
    }


    public async Task ApprouverDemandeAccesAsync(Guid demandeId)
    {
        var demande = await _db.Set<DemandeAcces>().FindAsync(demandeId)
            ?? throw new KeyNotFoundException("Demande d'accès introuvable.");

        if (demande.Statut == StatutDemandeAcces.Approuvee)
            throw new InvalidOperationException("Cette demande d'accès a déjà été approuvée.");

        var emailNormalise = demande.Email.Trim().ToLower();

        bool emailExiste = await _db.Utilisateurs
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email.ToLower() == emailNormalise);
        if (emailExiste)
            throw new InvalidOperationException($"Un compte utilisateur existe déjà avec l'adresse {demande.Email}.");

        var pays = await _db.Pays.FirstOrDefaultAsync(p => p.Id == demande.PaysId && p.EstActif)
            ?? throw new KeyNotFoundException("Pays associé introuvable.");

        var slug = GenererSlug(demande.NomEntreprise);
        if (await _db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.SlugUnique == slug))
            slug = $"{slug}-{Guid.NewGuid().ToString()[..4]}";

        var tenantId = Guid.NewGuid();
        string motDePasseTemp = "Finama@" + RandomNumberGenerator.GetInt32(100000, 999999);

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var tenant = new Tenant
            {
                Id = tenantId,
                Nom = demande.NomEntreprise,
                SlugUnique = slug,
                Email = emailNormalise,
                PaysId = pays.Id,
                Pays = pays,
                DeviseBase = pays.DeviseCode,
                TauxTVA = pays.TauxTVAStandard,
                PlanComptableCode = "OHADA",
                Plan = PlanAbonnement.Trial,
                AbonnementExpireAt = DateTime.UtcNow.AddDays(AbonnementConstants.DureeEssaiJours),
                EstActif = true
            };
            _db.Tenants.Add(tenant);

            _db.Utilisateurs.Add(new Utilisateur
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Nom = demande.NomContact,
                Prenom = demande.PrenomContact,
                Email = emailNormalise,
                MotDePasseHash = BCrypt.Net.BCrypt.HashPassword(motDePasseTemp),
                Role = RoleUtilisateur.AdminTenant,
                EstActif = true,
                DoitChangerMotDePasse = true
            });

            _db.ClassesComptables.AddRange(new List<ClasseComptable>
        {
            new() { Numero = 1, Libelle = "Capitaux",        TenantId = tenantId },
            new() { Numero = 2, Libelle = "Immobilisations", TenantId = tenantId },
            new() { Numero = 3, Libelle = "Stocks",          TenantId = tenantId },
            new() { Numero = 4, Libelle = "Tiers",           TenantId = tenantId },
            new() { Numero = 5, Libelle = "Trésorerie",      TenantId = tenantId },
            new() { Numero = 6, Libelle = "Charges",         TenantId = tenantId },
            new() { Numero = 7, Libelle = "Produits",        TenantId = tenantId }
        });

            _db.CompteComptables.AddRange(PlanComptableOhadaSeed.GenererPourTenant(tenantId));

            var annee = DateTime.Today.Year;
            _db.Exercices.Add(new ExerciceComptable
            {
                TenantId = tenantId,
                Annee = annee,
                Libelle = $"Exercice {annee}",
                DateDebut = new DateTime(annee, 1, 1),
                DateFin = new DateTime(annee, 12, 31),
                EstCloture = false
            });

            demande.Statut = StatutDemandeAcces.Approuvee;

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        try
        {
            await _emailService.SendEmailAsync(
                demande.Email,
                "Votre compte Finama SaaS est activé",
                $"<p>Bonjour {demande.PrenomContact},</p><p>Votre demande d'accès pour <b>{demande.NomEntreprise}</b> a été approuvée.</p><p>Voici vos identifiants temporaires :</p><ul><li>Email : {demande.Email}</li><li>Mot de passe : <b>{motDePasseTemp}</b></li></ul><p>Vous devrez le modifier à votre première connexion.</p>");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EMAIL WARNING] : {ex.Message}");
        }
    }

    public async Task RefuserDemandeAccesAsync(Guid demandeId)
    {
        var demande = await _db.Set<DemandeAcces>().FindAsync(demandeId)
            ?? throw new KeyNotFoundException("Demande d'accès introuvable.");

        if (demande.Statut == StatutDemandeAcces.Approuvee)
            throw new InvalidOperationException("Une demande approuvée ne peut pas être refusée.");

        demande.Statut = StatutDemandeAcces.Rejetee; // adapte au nom réel de ton enum
        await _db.SaveChangesAsync();
    }

    public async Task<List<TenantAdminDto>> ObtenirTenantsAsync()
    {
        return await _db.Tenants
            .IgnoreQueryFilters()
            .Where(t => !t.IsDeleted)
            .OrderByDescending(t => t.AbonnementExpireAt)
            .Select(t => new TenantAdminDto(
                t.Id,
                t.Nom,
                t.Email,
                t.Pays != null ? t.Pays.Nom : "",
                t.Plan.ToString(),
                t.AbonnementExpireAt,
                t.EstActif,
                _db.Utilisateurs.IgnoreQueryFilters()
                    .Count(u => u.TenantId == t.Id && !u.IsDeleted)))
            .ToListAsync();
    }

    public async Task ChangerStatutTenantAsync(Guid tenantId, bool actif)
    {
        var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == tenantId)
            ?? throw new KeyNotFoundException("Entreprise introuvable.");

        tenant.EstActif = actif;

        // Suspension : on coupe les sessions ouvertes
        if (!actif)
        {
            var users = await _db.Utilisateurs.IgnoreQueryFilters()
                .Where(u => u.TenantId == tenantId).ToListAsync();
            foreach (var u in users)
            {
                u.RefreshToken = null;
                u.RefreshTokenExpireAt = null;
            }
        }

        await _db.SaveChangesAsync();
    }

    public async Task ActiverAbonnementAsync(Guid tenantId, ActiverAbonnementRequest request)
    {
        if (request.DureeJours <= 0)
            throw new InvalidOperationException("La durée doit être supérieure à 0.");

        var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == tenantId)
            ?? throw new KeyNotFoundException("Entreprise introuvable.");

        // Prolonge à partir de l'échéance actuelle si elle est encore dans le futur
        var depart = tenant.AbonnementExpireAt.HasValue && tenant.AbonnementExpireAt.Value > DateTime.UtcNow
            ? tenant.AbonnementExpireAt.Value
            : DateTime.UtcNow;

        tenant.Plan = request.Plan;
        tenant.AbonnementExpireAt = depart.AddDays(request.DureeJours);
        tenant.EstActif = true;

        await _db.SaveChangesAsync();
    }

    public async Task<List<UtilisateurAdminDto>> ObtenirUtilisateursTenantAsync(Guid tenantId)
    {
        return await _db.Utilisateurs
            .IgnoreQueryFilters()
            .Where(u => u.TenantId == tenantId && !u.IsDeleted)
            .OrderBy(u => u.Nom)
            .Select(u => new UtilisateurAdminDto(
                u.Id, u.TenantId, u.Nom, u.Prenom, u.Email,
                u.Role.ToString(), u.EstActif, u.DerniereConnexionAt))
            .ToListAsync();
    }

    public async Task ChangerStatutUtilisateurAsync(Guid utilisateurId, bool actif)
    {
        var user = await ObtenirUtilisateurModifiableAsync(utilisateurId);

        user.EstActif = actif;
        if (!actif)
        {
            user.RefreshToken = null;
            user.RefreshTokenExpireAt = null;
        }

        await _db.SaveChangesAsync();
    }

    public async Task ReinitialiserMotDePasseAsync(Guid utilisateurId)
    {
        var user = await ObtenirUtilisateurModifiableAsync(utilisateurId);

        string motDePasseTemp = "Finama@" + RandomNumberGenerator.GetInt32(100000, 999999);
        user.MotDePasseHash = BCrypt.Net.BCrypt.HashPassword(motDePasseTemp);
        user.DoitChangerMotDePasse = true;
        user.RefreshToken = null;
        user.RefreshTokenExpireAt = null;
        user.OtpCode = null;
        user.OtpExpireAt = null;

        await _db.SaveChangesAsync();

        try
        {
            await _emailService.SendEmailAsync(
                user.Email,
                "Réinitialisation de votre mot de passe Finama",
                $"<p>Bonjour {user.Prenom},</p><p>Votre mot de passe a été réinitialisé.</p><p>Mot de passe temporaire : <b>{motDePasseTemp}</b></p><p>Vous devrez le modifier à votre prochaine connexion.</p>");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EMAIL WARNING] : {ex.Message}");
        }
    }

    public async Task SupprimerUtilisateurAsync(Guid utilisateurId)
    {
        var user = await ObtenirUtilisateurModifiableAsync(utilisateurId);

        // Si c'est le dernier admin actif du tenant, on refuse
        if (user.Role == RoleUtilisateur.AdminTenant)
        {
            bool autreAdmin = await _db.Utilisateurs.IgnoreQueryFilters()
                .AnyAsync(u => u.TenantId == user.TenantId
                            && u.Id != user.Id
                            && u.Role == RoleUtilisateur.AdminTenant
                            && u.EstActif && !u.IsDeleted);
            if (!autreAdmin)
                throw new InvalidOperationException("Impossible de supprimer le dernier administrateur de l'entreprise.");
        }

        user.IsDeleted = true;
        user.EstActif = false;
        user.RefreshToken = null;
        user.RefreshTokenExpireAt = null;

        await _db.SaveChangesAsync();
    }

    private async Task<Utilisateur> ObtenirUtilisateurModifiableAsync(Guid utilisateurId)
    {
        var user = await _db.Utilisateurs.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == utilisateurId && !u.IsDeleted)
            ?? throw new KeyNotFoundException("Utilisateur introuvable.");

        if (user.Role == RoleUtilisateur.SuperAdmin)
            throw new InvalidOperationException("Un compte SuperAdmin ne peut pas être modifié ici.");

        return user;
    }

    public async Task<int> InitialiserPlanComptableTenantAsync(Guid tenantId)
    {
        var existe = await _db.Tenants.IgnoreQueryFilters()
            .AnyAsync(t => t.Id == tenantId && !t.IsDeleted);
        if (!existe)
            throw new KeyNotFoundException("Entreprise introuvable.");

        bool modifie = false;

        // 1. Classes comptables
        bool aDesClasses = await _db.ClassesComptables.IgnoreQueryFilters()
            .AnyAsync(c => c.TenantId == tenantId);
        if (!aDesClasses)
        {
            _db.ClassesComptables.AddRange(new List<ClasseComptable>
        {
            new() { Numero = 1, Libelle = "Capitaux",        TenantId = tenantId },
            new() { Numero = 2, Libelle = "Immobilisations", TenantId = tenantId },
            new() { Numero = 3, Libelle = "Stocks",          TenantId = tenantId },
            new() { Numero = 4, Libelle = "Tiers",           TenantId = tenantId },
            new() { Numero = 5, Libelle = "Trésorerie",      TenantId = tenantId },
            new() { Numero = 6, Libelle = "Charges",         TenantId = tenantId },
            new() { Numero = 7, Libelle = "Produits",        TenantId = tenantId }
        });
            modifie = true;
        }

        // 2. Plan de comptes
        bool aDesComptes = await _db.CompteComptables.IgnoreQueryFilters()
            .AnyAsync(c => c.TenantId == tenantId);
        if (!aDesComptes)
        {
            _db.CompteComptables.AddRange(PlanComptableOhadaSeed.GenererPourTenant(tenantId));
            modifie = true;
        }

        // 3. Exercice de l'année en cours
        var annee = DateTime.Today.Year;
        bool aUnExercice = await _db.Exercices.IgnoreQueryFilters()
            .AnyAsync(e => e.TenantId == tenantId && e.Annee == annee);
        if (!aUnExercice)
        {
            _db.Exercices.Add(new ExerciceComptable
            {
                TenantId = tenantId,
                Annee = annee,
                Libelle = $"Exercice {annee}",
                DateDebut = new DateTime(annee, 1, 1),
                DateFin = new DateTime(annee, 12, 31),
                EstCloture = false
            });
            modifie = true;
        }

        if (modifie)
            await _db.SaveChangesAsync();

        return modifie ? 1 : 0;
    }

    public async Task<int> InitialiserPlanComptableTousTenantsAsync()
    {
        var tenantIds = await _db.Tenants.IgnoreQueryFilters()
            .Where(t => !t.IsDeleted)
            .Select(t => t.Id)
            .ToListAsync();

        int repares = 0;
        foreach (var id in tenantIds)
            repares += await InitialiserPlanComptableTenantAsync(id);

        return repares; // nombre d'entreprises effectivement complétées
    }

    private static string GenererSlug(string nom)
    {
        var slug = nom.ToLower()
            .Replace("é", "e").Replace("è", "e").Replace("ê", "e")
            .Replace("à", "a").Replace("â", "a").Replace("ç", "c")
            .Replace("ô", "o").Replace("ù", "u").Replace("î", "i");
        return Regex.Replace(slug, @"[^a-z0-9]+", "-").Trim('-');
    }
}