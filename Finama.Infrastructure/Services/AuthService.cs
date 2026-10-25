using Finama.Core.DTOs;
using Finama.Core.Entities;
using Finama.Infrastructure.Data;
using Finama.Infrastructure.Seeds;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Finama.Infrastructure.Services;

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> RegisterTenantAsync(RegisterTenantRequest request);
    Task<AuthResponse> VerifierOtpAsync(VerifyOtpRequest request);
    Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request);
    Task LogoutAsync(Guid utilisateurId);
    Task<AuthResult> DefinirNouveauMotDePasseAsync(Guid utilisateurId, ChangerMotDePasseRequest request);
}

public class AuthService : IAuthService
{
    private const int OtpTentativesMax = 5;
    private const int OtpDureeMinutes = 10;
    private const int OtpDelaiRenvoiMinutes = 2;
    private const int AppareilConfianceJours = 30;

    // Hash calculé une fois : sert à égaliser le temps de réponse quand le compte n'existe pas.
    private static readonly string HashFactice = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString());

    private readonly AppDbContext _db;
    private readonly IJwtService _jwt;
    private readonly JwtSettings _settings;
    private readonly IEmailService _emailService;
    private readonly IHttpContextAccessor _httpContext;
    private readonly ILogger<AuthService> _logger;

    public AuthService(AppDbContext db, IJwtService jwt, IOptions<JwtSettings> options,
        IEmailService emailService, IHttpContextAccessor httpContext, ILogger<AuthService> logger)
    {
        _db = db;
        _jwt = jwt;
        _settings = options.Value;
        _emailService = emailService;
        _httpContext = httpContext;
        _logger = logger;
    }

    // ─── Connexion (étape 1 : mot de passe) ───────────────────────────────────
    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.MotDePasse))
            throw new UnauthorizedAccessException("Email ou mot de passe incorrect.");

        var emailNormalise = request.Email.Trim().ToLowerInvariant();

        var utilisateur = await _db.Utilisateurs
            .IgnoreQueryFilters()
            .Include(u => u.Tenant!).ThenInclude(t => t.Pays)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == emailNormalise && !u.IsDeleted && u.EstActif);

        // Même message et vérification BCrypt factice : pas d'énumération des comptes par le temps de réponse.
        if (utilisateur is null)
        {
            BCrypt.Net.BCrypt.Verify(request.MotDePasse, HashFactice);
            _logger.LogInformation("Échec de connexion : compte inconnu ou inactif.");
            throw new UnauthorizedAccessException("Email ou mot de passe incorrect.");
        }

        if (!BCrypt.Net.BCrypt.Verify(request.MotDePasse, utilisateur.MotDePasseHash.Trim()))
        {
            _logger.LogInformation("Échec de connexion : mot de passe invalide pour l'utilisateur {UserId}.", utilisateur.Id);
            throw new UnauthorizedAccessException("Email ou mot de passe incorrect.");
        }

        VerifierStatutTenant(utilisateur);

        // Appareil de confiance (validé par OTP il y a moins de 30 jours) → connexion directe
        var deviceId = LireDeviceId();
        if (deviceId is not null)
        {
            var appareilValide = await _db.AppareilsConfiance.AnyAsync(a =>
                a.UtilisateurId == utilisateur.Id &&
                a.DeviceId == deviceId &&
                a.DateDerniereValidation > DateTime.UtcNow.AddDays(-AppareilConfianceJours));

            if (appareilValide)
            {
                if (utilisateur.Tenant is not null)
                {
                    await AssurerReferentielTenantAsync(utilisateur.Tenant.Id);
                    await _db.SaveChangesAsync();
                }
                return await EmettreTokensAsync(utilisateur, utilisateur.Tenant);
            }
        }

        // Anti-flood : un seul code toutes les 2 minutes
        if (utilisateur.OtpExpireAt.HasValue &&
            utilisateur.OtpExpireAt > DateTime.UtcNow.AddMinutes(OtpDureeMinutes - OtpDelaiRenvoiMinutes))
            throw new InvalidOperationException("Un code vient d'être envoyé. Patientez deux minutes avant d'en demander un nouveau.");

        var codeOtp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        utilisateur.OtpCode = codeOtp;
        utilisateur.OtpExpireAt = DateTime.UtcNow.AddMinutes(OtpDureeMinutes);
        utilisateur.OtpTentatives = 0;
        utilisateur.IsOtpValidated = false;
        await _db.SaveChangesAsync();

        // Envoi asynchrone : EmailService ne dépend que d'IConfiguration (pas de DbContext capturé).
        var destinataire = utilisateur.Email;
        var userId = utilisateur.Id;
        _ = Task.Run(async () =>
        {
            try { await _emailService.SendOtpEmailByResendAsync(destinataire, codeOtp); }
            catch (Exception ex) { _logger.LogError(ex, "Échec d'envoi de l'OTP pour l'utilisateur {UserId}.", userId); }
        });

        return new AuthResponse(
            AccessToken: string.Empty,
            RefreshToken: string.Empty,
            ExpireAt: DateTime.UtcNow,
            NomUtilisateur: $"{utilisateur.Prenom} {utilisateur.Nom}",
            Email: utilisateur.Email,
            Role: utilisateur.Role.ToString(),
            TenantId: Guid.Empty,
            NomEntreprise: string.Empty,
            Pays: string.Empty,
            Devise: string.Empty,
            DeviseSymbole: string.Empty,
            TauxTVA: 0,
            RequiresOtp: true,
            Message: "Un code de vérification a été envoyé sur votre boîte mail.",
            DoitChangerMotDePasse: utilisateur.DoitChangerMotDePasse);
    }

    // ─── Connexion (étape 2 : OTP) ────────────────────────────────────────────
    public async Task<AuthResponse> VerifierOtpAsync(VerifyOtpRequest request)
    {
        var emailNormalise = request.Email.Trim().ToLowerInvariant();

        var utilisateur = await _db.Utilisateurs
            .IgnoreQueryFilters()
            .Include(u => u.Tenant!).ThenInclude(t => t.Pays)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == emailNormalise && !u.IsDeleted && u.EstActif)
            ?? throw new UnauthorizedAccessException("Code incorrect ou expiré.");

        VerifierStatutTenant(utilisateur);

        if (string.IsNullOrEmpty(utilisateur.OtpCode) ||
            !utilisateur.OtpExpireAt.HasValue || utilisateur.OtpExpireAt < DateTime.UtcNow)
            throw new UnauthorizedAccessException("Le code a expiré. Demandez-en un nouveau.");

        if (utilisateur.OtpTentatives >= OtpTentativesMax)
            throw new UnauthorizedAccessException("Trop de tentatives. Demandez un nouveau code.");

        var codeOk = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(utilisateur.OtpCode),
            Encoding.UTF8.GetBytes((request.CodeOtp ?? string.Empty).Trim()));

        if (!codeOk)
        {
            utilisateur.OtpTentatives++;
            var restantes = OtpTentativesMax - utilisateur.OtpTentatives;
            if (restantes <= 0)
            {
                utilisateur.OtpCode = null;
                utilisateur.OtpExpireAt = null;
            }
            await _db.SaveChangesAsync();

            throw new UnauthorizedAccessException(restantes > 0
                ? $"Code incorrect. {restantes} tentative(s) restante(s)."
                : "Trop de tentatives. Demandez un nouveau code.");
        }

        // Mémorisation de l'appareil de confiance
        var deviceId = LireDeviceId();
        if (deviceId is not null)
        {
            var appareil = await _db.AppareilsConfiance
                .FirstOrDefaultAsync(a => a.UtilisateurId == utilisateur.Id && a.DeviceId == deviceId);

            if (appareil is null)
                _db.AppareilsConfiance.Add(new AppareilConfiance
                {
                    UtilisateurId = utilisateur.Id,
                    DeviceId = deviceId,
                    DateDerniereValidation = DateTime.UtcNow
                });
            else
                appareil.DateDerniereValidation = DateTime.UtcNow;
        }

        utilisateur.OtpCode = null;
        utilisateur.OtpExpireAt = null;
        utilisateur.OtpTentatives = 0;
        utilisateur.IsOtpValidated = true;

        if (utilisateur.Tenant is not null)
            await AssurerReferentielTenantAsync(utilisateur.Tenant.Id);

        await _db.SaveChangesAsync();

        return await EmettreTokensAsync(utilisateur, utilisateur.Tenant);
    }

    // ─── Inscription d'un tenant (réservée au SuperAdmin côté contrôleur) ─────
    public async Task<AuthResponse> RegisterTenantAsync(RegisterTenantRequest request)
    {
        var emailNormalise = request.Email.Trim().ToLowerInvariant();
        ValiderRobustesseMotDePasse(request.MotDePasse);

        var pays = await _db.Pays.FirstOrDefaultAsync(p => p.Id == request.PaysId && p.EstActif)
            ?? throw new KeyNotFoundException("Pays introuvable. Consultez GET /api/pays.");

        if (await _db.Utilisateurs.IgnoreQueryFilters().AnyAsync(u => u.Email.ToLower() == emailNormalise))
            throw new InvalidOperationException("Cet email est déjà utilisé.");

        var slug = GenererSlug(request.NomEntreprise);
        if (await _db.Tenants.AnyAsync(t => t.SlugUnique == slug))
            slug = $"{slug}-{Guid.NewGuid().ToString()[..4]}";

        var tenantId = Guid.NewGuid();

        await using var transaction = await _db.Database.BeginTransactionAsync();

        var tenant = new Tenant
        {
            Id = tenantId,
            Nom = request.NomEntreprise.Trim(),
            SlugUnique = slug,
            Email = emailNormalise,
            PaysId = pays.Id,
            Pays = pays,
            DeviseBase = pays.DeviseCode,
            TauxTVA = pays.TauxTVAStandard,
            PlanComptableCode = "OHADA",
            Plan = PlanAbonnement.Trial,
            AbonnementExpireAt = DateTime.UtcNow.AddDays(AbonnementConstants.DureeEssaiJours),
            EstActif = true,
        };
        _db.Tenants.Add(tenant);

        var admin = new Utilisateur
        {
            TenantId = tenantId,
            Nom = request.NomAdministrateur.Trim(),
            Prenom = request.PrenomAdministrateur.Trim(),
            Email = emailNormalise,
            MotDePasseHash = BCrypt.Net.BCrypt.HashPassword(request.MotDePasse),
            Role = RoleUtilisateur.AdminTenant,
            EstActif = true,
            DoitChangerMotDePasse = true, // créé par le SuperAdmin : mot de passe temporaire
        };
        _db.Utilisateurs.Add(admin);

        _db.ClassesComptables.AddRange(ClassesOhadaParDefaut(tenantId));
        _db.CompteComptables.AddRange(PlanComptableOhadaSeed.GenererPourTenant(tenantId));
        _db.Exercices.Add(NouvelExercice(tenantId, DateTime.Today.Year));

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return await EmettreTokensAsync(admin, tenant);
    }

    // ─── Refresh (rotation : chaque refresh token n'est utilisable qu'une fois) ───
    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request)
    {
        const string sessionExpiree = "Session expirée. Reconnectez-vous.";

        if (string.IsNullOrWhiteSpace(request.AccessToken) || string.IsNullOrWhiteSpace(request.RefreshToken))
            throw new UnauthorizedAccessException("Session invalide.");

        var principal = _jwt.ValiderToken(request.AccessToken);
        if (principal is null)
        {
            _logger.LogWarning("Refresh refusé : access token illisible ou signature invalide (clé JWT changée ?).");
            throw new UnauthorizedAccessException("Session invalide.");
        }

        if (!Guid.TryParse(principal.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value, out var utilisateurId))
            throw new UnauthorizedAccessException("Session invalide.");

        var utilisateur = await _db.Utilisateurs
            .IgnoreQueryFilters()
            .Include(u => u.Tenant!).ThenInclude(t => t.Pays)
            .FirstOrDefaultAsync(u => u.Id == utilisateurId);

        if (utilisateur is null)
        {
            _logger.LogWarning("Refresh refusé : utilisateur {UserId} absent de cette base (jeton émis par un autre environnement ?).", utilisateurId);
            throw new UnauthorizedAccessException(sessionExpiree);
        }

        if (utilisateur.IsDeleted || !utilisateur.EstActif)
        {
            _logger.LogWarning("Refresh refusé : utilisateur {UserId} supprimé ou désactivé.", utilisateurId);
            throw new UnauthorizedAccessException(sessionExpiree);
        }

        var hashRecu = _jwt.HacherRefreshToken(request.RefreshToken);
        var hashOk = !string.IsNullOrEmpty(utilisateur.RefreshToken)
            && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(utilisateur.RefreshToken), Encoding.UTF8.GetBytes(hashRecu));

        if (!hashOk)
        {
            _logger.LogWarning("Refresh refusé : refresh token inconnu pour {UserId} (déjà utilisé, ancien format non haché, ou session ouverte ailleurs).", utilisateurId);
            throw new UnauthorizedAccessException(sessionExpiree);
        }

        if (utilisateur.RefreshTokenExpireAt < DateTime.UtcNow)
        {
            _logger.LogInformation("Refresh refusé : refresh token expiré pour {UserId}.", utilisateurId);
            throw new UnauthorizedAccessException(sessionExpiree);
        }

        VerifierStatutTenant(utilisateur, pourRefresh: true);

        return await EmettreTokensAsync(utilisateur, utilisateur.Tenant);
    }
    public async Task LogoutAsync(Guid utilisateurId)
    {
        var utilisateur = await _db.Utilisateurs.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == utilisateurId);
        if (utilisateur is null) return;

        utilisateur.RefreshToken = null;
        utilisateur.RefreshTokenExpireAt = null;
        await _db.SaveChangesAsync();
    }

    // ─── Changement de mot de passe : renvoie une nouvelle session sans pwd_change ───
    public async Task<AuthResult> DefinirNouveauMotDePasseAsync(Guid utilisateurId, ChangerMotDePasseRequest request)
    {
        var utilisateur = await _db.Utilisateurs
            .IgnoreQueryFilters()
            .Include(u => u.Tenant!).ThenInclude(t => t.Pays)
            .FirstOrDefaultAsync(u => u.Id == utilisateurId && !u.IsDeleted && u.EstActif);

        if (utilisateur is null)
            return AuthResult.Failure("Utilisateur introuvable.");

        if (!BCrypt.Net.BCrypt.Verify(request.AncienMotDePasseOuCode, utilisateur.MotDePasseHash))
            return AuthResult.Failure("Le mot de passe actuel est incorrect.");

        if (request.NouveauMotDePasse == request.AncienMotDePasseOuCode)
            return AuthResult.Failure("Le nouveau mot de passe doit être différent de l'ancien.");

        try { ValiderRobustesseMotDePasse(request.NouveauMotDePasse); }
        catch (ArgumentException ex) { return AuthResult.Failure(ex.Message); }

        utilisateur.MotDePasseHash = BCrypt.Net.BCrypt.HashPassword(request.NouveauMotDePasse);
        utilisateur.DoitChangerMotDePasse = false;
        await _db.SaveChangesAsync();

        var resultat = AuthResult.Success("Mot de passe mis à jour avec succès.");
        resultat.Session = await EmettreTokensAsync(utilisateur, utilisateur.Tenant); // remplace l'ancienne session
        return resultat;
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private async Task<AuthResponse> EmettreTokensAsync(Utilisateur utilisateur, Tenant? tenant)
    {
        var accessToken = _jwt.GenererAccessToken(utilisateur, tenant);
        var refreshToken = _jwt.GenererRefreshToken();

        utilisateur.RefreshToken = _jwt.HacherRefreshToken(refreshToken); // seul le hash est persisté
        utilisateur.RefreshTokenExpireAt = DateTime.UtcNow.AddDays(_settings.RefreshTokenDureeJours);
        utilisateur.DerniereConnexionAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return new AuthResponse(
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            ExpireAt: DateTime.UtcNow.AddMinutes(_settings.AccessTokenDureeMinutes),
            NomUtilisateur: $"{utilisateur.Prenom} {utilisateur.Nom}",
            Email: utilisateur.Email,
            Role: utilisateur.Role.ToString(),
            TenantId: tenant?.Id ?? Guid.Empty,
            NomEntreprise: tenant?.Nom ?? "SaaS Admin",
            Pays: tenant?.Pays?.Nom ?? "Global",
            Devise: tenant?.DeviseBase ?? "XOF",
            DeviseSymbole: tenant?.Pays?.DeviseSymbole ?? "FCFA",
            TauxTVA: tenant?.TauxTVA ?? 0,
            RequiresOtp: false,
            Message: null,
            DoitChangerMotDePasse: utilisateur.DoitChangerMotDePasse);
    }

    private static void VerifierStatutTenant(Utilisateur utilisateur, bool pourRefresh = false)
    {
        if (utilisateur.Role == RoleUtilisateur.SuperAdmin) return;

        var tenant = utilisateur.Tenant;
        if (tenant is null || !tenant.EstActif)
            throw new UnauthorizedAccessException("Le compte de votre entreprise est désactivé ou suspendu.");

        if (tenant.AbonnementExpireAt.HasValue && tenant.AbonnementExpireAt.Value < DateTime.UtcNow)
        {
            const string msg = "Votre abonnement Finama a expiré. Contactez votre administrateur.";
            if (pourRefresh) throw new UnauthorizedAccessException(msg);
            throw new InvalidOperationException(msg);
        }
    }

    private string? LireDeviceId()
    {
        var valeur = _httpContext.HttpContext?.Request.Headers["X-Device-Id"].ToString();
        // Un DeviceId valide est un GUID généré par le navigateur ; on ignore toute autre valeur.
        return Guid.TryParse(valeur, out var id) ? id.ToString() : null;
    }

    /// <summary>Crée classes et exercice courant s'ils manquent (anciens tenants). Ne sauvegarde pas.</summary>
    private async Task AssurerReferentielTenantAsync(Guid tenantId)
    {
        if (!await _db.ClassesComptables.IgnoreQueryFilters().AnyAsync(c => c.TenantId == tenantId))
            _db.ClassesComptables.AddRange(ClassesOhadaParDefaut(tenantId));

        var annee = DateTime.Today.Year;
        if (!await _db.Exercices.IgnoreQueryFilters().AnyAsync(e => e.TenantId == tenantId && e.Annee == annee && !e.IsDeleted))
            _db.Exercices.Add(NouvelExercice(tenantId, annee));
    }

    private static List<ClasseComptable> ClassesOhadaParDefaut(Guid tenantId) =>
    [
        new() { Numero = 1, Libelle = "Capitaux", TenantId = tenantId },
        new() { Numero = 2, Libelle = "Immobilisations", TenantId = tenantId },
        new() { Numero = 3, Libelle = "Stocks", TenantId = tenantId },
        new() { Numero = 4, Libelle = "Tiers", TenantId = tenantId },
        new() { Numero = 5, Libelle = "Trésorerie", TenantId = tenantId },
        new() { Numero = 6, Libelle = "Charges", TenantId = tenantId },
        new() { Numero = 7, Libelle = "Produits", TenantId = tenantId },
    ];

    private static ExerciceComptable NouvelExercice(Guid tenantId, int annee) => new()
    {
        TenantId = tenantId,
        Annee = annee,
        Libelle = $"Exercice {annee}",
        DateDebut = new DateTime(annee, 1, 1),
        DateFin = new DateTime(annee, 12, 31),
        EstCloture = false
    };

    private static void ValiderRobustesseMotDePasse(string motDePasse)
    {
        if (string.IsNullOrWhiteSpace(motDePasse) || motDePasse.Length < 10
            || !Regex.IsMatch(motDePasse, "[A-Za-z]") || !Regex.IsMatch(motDePasse, "[0-9]"))
            throw new ArgumentException("Le mot de passe doit contenir au moins 10 caractères, dont des lettres et des chiffres.");
    }

    private static string GenererSlug(string nom)
    {
        var normalise = nom.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sansAccents = new string(normalise
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray());
        return Regex.Replace(sansAccents, "[^a-z0-9]+", "-").Trim('-');
    }
}
