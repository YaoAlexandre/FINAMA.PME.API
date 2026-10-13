using Finama.Core.DTOs;
using Finama.Core.Entities;
using Finama.Infrastructure.Data;
using Finama.Infrastructure.Seeds;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Org.BouncyCastle.Asn1.Ocsp;
using System.Security.Cryptography;

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
    private readonly AppDbContext _db;
    private readonly IJwtService _jwt;
    private readonly JwtSettings _settings;
    private readonly IEmailService _emailService;
    private readonly IHttpContextAccessor _httpContext;

    public AuthService(AppDbContext db, IJwtService jwt, IOptions<JwtSettings> options, IEmailService emailService, IHttpContextAccessor httpContext)
    {
        _db = db;
        _jwt = jwt;
        _settings = options.Value;
        _emailService = emailService;
        _httpContext = httpContext;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var emailNormalise = request.Email.Trim().ToLower();

        // 1. Recherche sécurisée de l'utilisateur avec son Tenant et Pays
        var utilisateur = await _db.Utilisateurs
            .IgnoreQueryFilters()
            .Include(u => u.Tenant!)
                .ThenInclude(t => t.Pays)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == emailNormalise
                                   && !u.IsDeleted
                                   && u.EstActif);

        if (utilisateur is null)
        {
            Console.WriteLine($"[DIAGNOSTIC LOGIN] ÉCHEC : Aucun utilisateur trouvé pour '{request.Email}'");
            throw new UnauthorizedAccessException("Email ou mot de passe incorrect.");
        }

        // 2. Nettoyage du hash et vérification BCrypt
        string hashNettoye = utilisateur.MotDePasseHash.Trim();
        bool estValide = BCrypt.Net.BCrypt.Verify(request.MotDePasse, hashNettoye);

        if (!estValide)
        {
            Console.WriteLine($"[DIAGNOSTIC LOGIN] ÉCHEC : Mot de passe invalide pour '{request.Email}'");
            throw new UnauthorizedAccessException("Email ou mot de passe incorrect.");
        }

        // 3. Vérification du statut de l'entreprise (Uniquement si l'utilisateur appartient à un Tenant)
        if (utilisateur.Role != RoleUtilisateur.SuperAdmin)
        {
            var tenant = utilisateur.Tenant;
            if (tenant is null || !tenant.EstActif)
            {
                throw new UnauthorizedAccessException("Le compte de votre entreprise est désactivé ou suspendu.");
            }

            if (tenant.AbonnementExpireAt.HasValue && tenant.AbonnementExpireAt.Value < DateTime.UtcNow)
            {
                throw new InvalidOperationException("Votre abonnement Finama a expiré. Veuillez contacter votre administrateur.");
            }
        }

        // 4. Protection contre le spam OTP (Anti-flood)
        if (utilisateur.OtpExpireAt.HasValue && utilisateur.OtpExpireAt > DateTime.UtcNow.AddMinutes(8))
        {
            throw new InvalidOperationException("Un code a récemment été envoyé. Veuillez patienter avant de demander un nouveau code.");
        }

        // 5. Récupération et vérification du DeviceId (Appareils de confiance)
        var deviceId = _httpContext.HttpContext?.Request.Headers["X-Device-Id"].ToString();

        if (!string.IsNullOrEmpty(deviceId))
        {
            var estDejaValide = await _db.AppareilsConfiance
                .AnyAsync(a => a.UtilisateurId == utilisateur.Id
                            && a.DeviceId == deviceId
                            && a.DateDerniereValidation > DateTime.UtcNow.AddDays(-30));

            if (estDejaValide)
            {
                return await EmettreTokensAsync(utilisateur, utilisateur.Tenant);
            }
        }

        // 6. Génération du code OTP (Si appareil non reconnu)
        var random = new Random();
        string codeOtp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

        utilisateur.OtpCode = codeOtp;
        utilisateur.OtpExpireAt = DateTime.UtcNow.AddMinutes(10);
        utilisateur.IsOtpValidated = false;

        _db.Utilisateurs.Update(utilisateur);
        await _db.SaveChangesAsync();

        // 7. Envoi du mail de manière asynchrone (Fire-and-Forget)
        _ = Task.Run(async () =>
        {
            try
            {
                await _emailService.SendOtpEmailByResendAsync(utilisateur.Email, codeOtp);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CRITICAL OTP EMAIL ERROR] Échec de l'envoi à {utilisateur.Email} : {ex.Message}");
            }
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
            utilisateur.DoitChangerMotDePasse
        );
    }

    public async Task<AuthResponse> VerifierOtpAsync(VerifyOtpRequest request)
    {
        var emailNormalise = request.Email.Trim().ToLower();

        // 1. Recherche de l'utilisateur complet avec son Tenant
        var utilisateur = await _db.Utilisateurs
            .IgnoreQueryFilters()
            .Include(u => u.Tenant!)
                .ThenInclude(t => t.Pays)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == emailNormalise);

        if (utilisateur is null)
            throw new UnauthorizedAccessException("Demande invalide.");

        if (utilisateur.Role != RoleUtilisateur.SuperAdmin)
        {
            var tenant = utilisateur.Tenant;
            if (tenant is null || !tenant.EstActif)
                throw new UnauthorizedAccessException("Accès refusé : Entreprise suspendue.");

            if (tenant.AbonnementExpireAt.HasValue && tenant.AbonnementExpireAt.Value < DateTime.UtcNow)
                throw new InvalidOperationException("Accès refusé : L'abonnement de votre entreprise a expiré.");
        }

        // 2. Contrôles sur le code OTP
        if (string.IsNullOrEmpty(utilisateur.OtpCode) || utilisateur.OtpCode != request.CodeOtp)
            throw new UnauthorizedAccessException("Code OTP incorrect.");

        if (!utilisateur.OtpExpireAt.HasValue || utilisateur.OtpExpireAt < DateTime.UtcNow)
            throw new UnauthorizedAccessException("Le code OTP a expiré. Veuillez en générer un nouveau.");

        // 3. Enregistrement de la confiance du périphérique
        var deviceId = _httpContext.HttpContext?.Request.Headers["X-Device-Id"].ToString();
        if (!string.IsNullOrEmpty(deviceId))
        {
            var appareil = await _db.AppareilsConfiance
                .FirstOrDefaultAsync(a => a.UtilisateurId == utilisateur.Id && a.DeviceId == deviceId);

            if (appareil is null)
            {
                _db.AppareilsConfiance.Add(new AppareilConfiance
                {
                    UtilisateurId = utilisateur.Id,
                    DeviceId = deviceId,
                    DateDerniereValidation = DateTime.UtcNow
                });
            }
            else
            {
                appareil.DateDerniereValidation = DateTime.UtcNow;
            }
        }

        // 4. Réinitialisation des champs OTP
        utilisateur.OtpCode = null;
        utilisateur.OtpExpireAt = null;
        utilisateur.IsOtpValidated = true;

        _db.Utilisateurs.Update(utilisateur);
        await _db.SaveChangesAsync();

        // 5. Initialisation automatique du plan comptable et de l'exercice (si applicatif PME)
        if (utilisateur.Tenant != null)
        {
            var tenantId = utilisateur.Tenant.Id;

            // --- A. Plan comptable ---
            var aDesClasses = await _db.ClassesComptables
                .IgnoreQueryFilters()
                .AnyAsync(c => c.TenantId == tenantId);

            if (!aDesClasses)
            {
                var classesParDefaut = new List<ClasseComptable>
        {
            new() { Numero = 1, Libelle = "Capitaux", TenantId = tenantId },
            new() { Numero = 2, Libelle = "Immobilisations", TenantId = tenantId },
            new() { Numero = 3, Libelle = "Stocks", TenantId = tenantId },
            new() { Numero = 4, Libelle = "Tiers", TenantId = tenantId },
            new() { Numero = 5, Libelle = "Trésorerie", TenantId = tenantId },
            new() { Numero = 6, Libelle = "Charges", TenantId = tenantId },
            new() { Numero = 7, Libelle = "Produits", TenantId = tenantId }
        };

                await _db.ClassesComptables.AddRangeAsync(classesParDefaut);
            }

            // --- B. Initialisation de l'exercice comptable courant ---
            var anneeEnCours = DateTime.Today.Year;
            var aUnExercice = await _db.Exercices
                .IgnoreQueryFilters()
                .AnyAsync(e => e.TenantId == tenantId && e.Annee == anneeEnCours);

            if (!aUnExercice)
            {
                var nouvelExercice = new ExerciceComptable
                {
                    TenantId = tenantId,
                    Annee = anneeEnCours,
                    Libelle = $"Exercice {anneeEnCours}",
                    DateDebut = new DateTime(anneeEnCours, 1, 1),
                    DateFin = new DateTime(anneeEnCours, 12, 31),
                    EstCloture = false
                };

                await _db.Exercices.AddAsync(nouvelExercice);
            }

            await _db.SaveChangesAsync();
        }

        return await EmettreTokensAsync(utilisateur, utilisateur.Tenant);
    }

    public async Task<AuthResponse> RegisterTenantAsync(RegisterTenantRequest request)
    {
        var emailNormalise = request.Email.Trim().ToLower();

        var pays = await _db.Pays
            .FirstOrDefaultAsync(p => p.Id == request.PaysId && p.EstActif)
            ?? throw new KeyNotFoundException("Pays introuvable. Consultez GET /api/pays.");

        var emailExiste = await _db.Utilisateurs
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email.ToLower() == emailNormalise);

        if (emailExiste)
            throw new InvalidOperationException("Cet email est déjà utilisé.");

        var slug = GenererSlug(request.NomEntreprise);
        if (await _db.Tenants.AnyAsync(t => t.SlugUnique == slug))
            slug = $"{slug}-{Guid.NewGuid().ToString()[..4]}";

        // 1. Initialiser explicitement l'ID du Tenant
        var tenantId = Guid.NewGuid();

        using var transaction = await _db.Database.BeginTransactionAsync();

        try
        {
            var tenant = new Tenant
            {
                Id = tenantId,
                Nom = request.NomEntreprise,
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

            // 2. Administrateur
            var admin = new Utilisateur
            {
                TenantId = tenantId,
                Nom = request.NomAdministrateur,
                Prenom = request.PrenomAdministrateur,
                Email = emailNormalise,
                MotDePasseHash = BCrypt.Net.BCrypt.HashPassword(request.MotDePasse),
                Role = RoleUtilisateur.AdminTenant,
                EstActif = true,
            };
            _db.Utilisateurs.Add(admin);

            // 3. Classes SYSCOHADA
            var classesParDefaut = new List<ClasseComptable>
        {
            new() { Numero = 1, Libelle = "Capitaux", TenantId = tenantId },
            new() { Numero = 2, Libelle = "Immobilisations", TenantId = tenantId },
            new() { Numero = 3, Libelle = "Stocks", TenantId = tenantId },
            new() { Numero = 4, Libelle = "Tiers", TenantId = tenantId },
            new() { Numero = 5, Libelle = "Trésorerie", TenantId = tenantId },
            new() { Numero = 6, Libelle = "Charges", TenantId = tenantId },
            new() { Numero = 7, Libelle = "Produits", TenantId = tenantId }
        };
            _db.ClassesComptables.AddRange(classesParDefaut);

            // 4. Plan de comptes
            _db.CompteComptables.AddRange(PlanComptableOhadaSeed.GenererPourTenant(tenantId));

            // 5. Exercice comptable courant
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

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return await EmettreTokensAsync(admin, tenant);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request)
    {
        var principal = _jwt.ValiderToken(request.AccessToken);
        if (principal is null)
            throw new UnauthorizedAccessException("Token invalide.");

        var utilisateurId = Guid.Parse(
            principal.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)!.Value);

        var utilisateur = await _db.Utilisateurs
            .IgnoreQueryFilters()
            .Include(u => u.Tenant!)
                .ThenInclude(t => t.Pays)
            .FirstOrDefaultAsync(u => u.Id == utilisateurId);

        if (utilisateur is null
            || utilisateur.RefreshToken != request.RefreshToken
            || utilisateur.RefreshTokenExpireAt < DateTime.UtcNow)
            throw new UnauthorizedAccessException("Refresh token expiré ou invalide.");

        if (utilisateur.Role != RoleUtilisateur.SuperAdmin)
        {
            if (utilisateur.Tenant == null || !utilisateur.Tenant.EstActif || (utilisateur.Tenant.AbonnementExpireAt.HasValue && utilisateur.Tenant.AbonnementExpireAt.Value < DateTime.UtcNow))
                throw new UnauthorizedAccessException("L'accès de votre entreprise a expiré.");
        }

        return await EmettreTokensAsync(utilisateur, utilisateur.Tenant);
    }

    public async Task LogoutAsync(Guid utilisateurId)
    {
        var utilisateur = await _db.Utilisateurs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == utilisateurId);

        if (utilisateur is not null)
        {
            utilisateur.RefreshToken = null;
            utilisateur.RefreshTokenExpireAt = null;
            await _db.SaveChangesAsync();
        }
    }

    private async Task<AuthResponse> EmettreTokensAsync(Utilisateur utilisateur, Tenant? tenant)
    {
        var accessToken = _jwt.GenererAccessToken(utilisateur, tenant);
        var refreshToken = _jwt.GenererRefreshToken();

        utilisateur.RefreshToken = refreshToken;
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
            utilisateur.DoitChangerMotDePasse
        );
    }

    private static string GenererSlug(string nom)
    {
        var slug = nom.ToLower()
            .Replace("é", "e").Replace("è", "e").Replace("ê", "e")
            .Replace("à", "a").Replace("â", "a").Replace("ç", "c")
            .Replace("ô", "o").Replace("ù", "u").Replace("î", "i");
        return System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9]+", "-").Trim('-');
    }

    public async Task<AuthResult> DefinirNouveauMotDePasseAsync(Guid utilisateurId, ChangerMotDePasseRequest request)
    {
        var utilisateur = await _db.Utilisateurs.FindAsync(utilisateurId);
        if (utilisateur == null)
            return AuthResult.Failure("Utilisateur introuvable.");

        // Vérifier l'ancien mot de passe / code
        if (!BCrypt.Net.BCrypt.Verify(request.AncienMotDePasseOuCode, utilisateur.MotDePasseHash))
            return AuthResult.Failure("Le mot de passe temporaire est incorrect.");

        // Mettre à jour le mot de passe
        utilisateur.MotDePasseHash = BCrypt.Net.BCrypt.HashPassword(request.NouveauMotDePasse);
        utilisateur.DoitChangerMotDePasse = false;

        await _db.SaveChangesAsync();

        return AuthResult.Success("Mot de passe mis à jour avec succès.");
    }
}