using Finama.Core.DTOs;
using Finama.Core.Entities;
using Finama.Infrastructure.Data;
using Finama.Infrastructure.Seeds;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Finama.Infrastructure.Services;

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> RegisterTenantAsync(RegisterTenantRequest request);
    Task<AuthResponse> VerifierOtpAsync(VerifyOtpRequest request);
    Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request);
    Task LogoutAsync(Guid utilisateurId);
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
        // 1. Recherche brute sans aucun filtre pour être sûr de trouver l'adresse email
        //var utilisateur = await _db.Utilisateurs
        //    .IgnoreQueryFilters()
        //    .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower().Trim());
        var utilisateur = await _db.Utilisateurs
            .IgnoreQueryFilters()
            .Include(u => u.Tenant)
                .ThenInclude(t => t.Pays)
            .FirstOrDefaultAsync(u => u.Email == request.Email.ToLower()
                                   && !u.IsDeleted
                                   && u.EstActif);

        // LOG 2 : Est-ce qu'on l'a trouvé en base PostgreSQL ?
        if (utilisateur is null)
        {
            Console.WriteLine($"[DIAGNOSTIC LOGIN] ÉCHEC : Aucun utilisateur trouvé pour l'email '{request.Email}'");
            throw new UnauthorizedAccessException("Email ou mot de passe incorrect.");
        }

        Console.WriteLine($"[DIAGNOSTIC LOGIN] Utilisateur trouvé ! Nom: {utilisateur.Nom}, EstActif: {utilisateur.EstActif}");
        Console.WriteLine($"[DIAGNOSTIC LOGIN] Hash en base: '{utilisateur.MotDePasseHash}'");

        // Nettoyage et vérification BCrypt
        string hashNettoye = utilisateur.MotDePasseHash.Trim();
        bool estValide = BCrypt.Net.BCrypt.Verify(request.MotDePasse, hashNettoye);

        // LOG 3 : Est-ce que BCrypt valide le mot de passe ?
        Console.WriteLine($"[DIAGNOSTIC LOGIN] Résultat de la vérification BCrypt: {estValide}");

        //if (!estValide)
        //    throw new UnauthorizedAccessException("Email ou mot de passe incorrect.");

        // 3. Vérification des statuts du compte après validation du mot de passe
        if (utilisateur.IsDeleted || !utilisateur.EstActif)
            throw new UnauthorizedAccessException("Ce compte utilisateur est désactivé ou supprimé.");

        if (utilisateur.OtpExpireAt > DateTime.UtcNow.AddMinutes(8)) // Si le code précédent est encore valide depuis moins de 2 min
        {
            throw new InvalidOperationException("Veuillez patienter avant de demander un nouveau code.");
        }

        // 🛡️ Récupération sécurisée du DeviceId
        var deviceId = _httpContext.HttpContext?.Request.Headers["X-Device-Id"].ToString();

        if (!string.IsNullOrEmpty(deviceId))
        {
            var estDejaValide = await _db.AppareilsConfiance
                .AnyAsync(a => a.UtilisateurId == utilisateur.Id
                            && a.DeviceId == deviceId.ToString()
                            && a.DateDerniereValidation > DateTime.UtcNow.AddDays(-30));

            if (estDejaValide)
            {
                return await EmettreTokensAsync(utilisateur, utilisateur.Tenant);
            }
        }

        // ─── 🛡️ ÉTAPE OTP COUPLÉE ICI (Le mot de passe est valide) ───

        var random = new Random();
        string codeOtp = random.Next(100000, 999999).ToString();

        // Enregistrement de l'OTP en base (Valable 10 minutes)
        utilisateur.OtpCode = codeOtp;
        utilisateur.OtpExpireAt = DateTime.UtcNow.AddMinutes(10);
        utilisateur.IsOtpValidated = false;

        _db.Utilisateurs.Update(utilisateur);
        await _db.SaveChangesAsync();

        // 📧 ICI : Appel de ton service d'envoi d'email
        // await _emailService.SendOtpEmailAsync(utilisateur.Email, codeOtp);
        Console.WriteLine($"[OTP DEBUG] Code généré pour {utilisateur.Email} : {codeOtp}");

        // 📧 Envoi réel du mail de validation
        // 📧 Envoi asynchrone en tâche de fond (Fire-and-Forget)
        // La requête HTTP se termine immédiatement et l'utilisateur reçoit instantanément son UI de validation
        _ = Task.Run(async () =>
        {
            try
            {
                await _emailService.SendOtpEmailAsync(utilisateur.Email, codeOtp);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CRITICAL OTP EMAIL ERROR] Impossible d'envoyer le mail à {utilisateur.Email}. Erreur : {ex.Message}");
            }
        });

        // On retourne une réponse qui bloque l'accès aux tokens tant que l'OTP n'est pas fourni
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
        Message: "Un code de vérification a été envoyé sur votre boîte mail."
    );
    }

    public async Task<AuthResponse> VerifierOtpAsync(VerifyOtpRequest request)
    {
        // 1. Recherche brute de l'utilisateur pour valider son OTP
        var utilisateur = await _db.Utilisateurs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower().Trim());

        var utilisateurComplet = await _db.Utilisateurs
            .IgnoreQueryFilters()
            .Include(u => u.Tenant)
                .ThenInclude(t => t.Pays)
            .FirstOrDefaultAsync(u => u.Id == utilisateur.Id);

        if (utilisateurComplet?.Tenant is null || !utilisateurComplet.Tenant.EstActif)
            throw new UnauthorizedAccessException("Accès refusé : Entreprise suspendue.");

        if (utilisateur is null)
            throw new UnauthorizedAccessException("Demande invalide.");

        // 2. Contrôles de sécurité sur l'OTP
        if (string.IsNullOrEmpty(utilisateur.OtpCode) || utilisateur.OtpCode != request.CodeOtp)
            throw new UnauthorizedAccessException("Code OTP incorrect.");

        if (utilisateur.OtpExpireAt < DateTime.UtcNow)
            throw new UnauthorizedAccessException("Le code OTP a expiré. Veuillez en générer un nouveau.");

        if (utilisateur is null || utilisateur.OtpCode != request.CodeOtp || utilisateur.OtpExpireAt < DateTime.UtcNow)
            throw new UnauthorizedAccessException("Code invalide ou expiré.");

        // 💾 ENREGISTREMENT DE LA CONFIANCE ICI
        var deviceId = _httpContext.HttpContext?.Request.Headers["X-Device-Id"].ToString();
        if (!string.IsNullOrEmpty(deviceId))
        {
            var appareil = await _db.AppareilsConfiance
                .FirstOrDefaultAsync(a => a.UtilisateurId == utilisateur.Id && a.DeviceId == deviceId);

            if (appareil == null)
                _db.AppareilsConfiance.Add(new AppareilConfiance { UtilisateurId = utilisateur.Id, DeviceId = deviceId, DateDerniereValidation = DateTime.UtcNow });
            else
                appareil.DateDerniereValidation = DateTime.UtcNow;
        }

        // 3. OTP Valide ! On réinitialise les champs de sécurité
        utilisateur.OtpCode = null;
        utilisateur.OtpExpireAt = null;
        utilisateur.IsOtpValidated = true;

        _db.Utilisateurs.Update(utilisateur);
        await _db.SaveChangesAsync();

        // 4. Chargement explicite du Tenant et du Pays pour éviter les jointures vides sous Postgres
        await _db.Entry(utilisateur).Reference(u => u.Tenant).LoadAsync();

        if (utilisateur.Tenant is null)
            throw new UnauthorizedAccessException("Erreur : Aucun compte entreprise associé.");

        await _db.Entry(utilisateur.Tenant).Reference(t => t.Pays).LoadAsync();

        if (!utilisateur.Tenant.EstActif)
            throw new UnauthorizedAccessException("Ce compte entreprise est suspendu.");

        // 5. Initialisation tardive (Lazy-Init) de ton plan comptable SYSCOHADA
        var tenantId = utilisateur.TenantId;
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
            await _db.SaveChangesAsync();
        }

        // 6. Émission finale des jetons d'authentification (Connexion réussie)
        return await EmettreTokensAsync(utilisateur, utilisateur.Tenant);
    }

    //public async Task<AuthResponse> LoginAsync(LoginRequest request)
    //{
    //    var utilisateur = await _db.Utilisateurs
    //        .IgnoreQueryFilters()
    //        .Include(u => u.Tenant)
    //            .ThenInclude(t => t.Pays)
    //        .FirstOrDefaultAsync(u => u.Email == request.Email.ToLower()
    //                               && !u.IsDeleted
    //                               && u.EstActif);

    //    if (utilisateur is null || !BCrypt.Net.BCrypt.Verify(request.MotDePasse, utilisateur.MotDePasseHash))
    //        throw new UnauthorizedAccessException("Email ou mot de passe incorrect.");

    //    if (!utilisateur.Tenant.EstActif)
    //        throw new UnauthorizedAccessException("Ce compte entreprise est suspendu.");

    //    return await EmettreTokensAsync(utilisateur, utilisateur.Tenant);
    //}
    public async Task<AuthResponse> RegisterTenantAsync(RegisterTenantRequest request)
    {
        // 1. Vérifications initiales (Pays et Unicité de l'email)
        var pays = await _db.Pays
            .FirstOrDefaultAsync(p => p.Id == request.PaysId && p.EstActif)
            ?? throw new KeyNotFoundException("Pays introuvable. Consultez GET /api/pays.");

        var emailExiste = await _db.Utilisateurs
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email == request.Email.ToLower());

        if (emailExiste)
            throw new InvalidOperationException("Cet email est déjà utilisé.");

        // 2. Génération du slug d'entreprise
        var slug = GenererSlug(request.NomEntreprise);
        if (await _db.Tenants.AnyAsync(t => t.SlugUnique == slug))
            slug = $"{slug}-{Guid.NewGuid().ToString()[..4]}";

        // 3. Création et affectation de l'entité Tenant
        var tenant = new Tenant
        {
            Nom = request.NomEntreprise,
            SlugUnique = slug,
            Email = request.Email.ToLower(),
            PaysId = pays.Id,
            Pays = pays,
            DeviseBase = pays.DeviseCode,   // XOF, STD, XAF...
            TauxTVA = pays.TauxTVAStandard,
            PlanComptableCode = "OHADA",
            Plan = PlanAbonnement.Trial,
            AbonnementExpireAt = DateTime.UtcNow.AddDays(14),
            EstActif = true,
        };
        _db.Tenants.Add(tenant); // Génère le TenantId pour l'arbre d'entités EF

        // 4. Création de l'Administrateur du Tenant
        var admin = new Utilisateur
        {
            TenantId = tenant.Id,
            Nom = request.NomAdministrateur,
            Prenom = request.PrenomAdministrateur,
            Email = request.Email.ToLower(),
            MotDePasseHash = BCrypt.Net.BCrypt.HashPassword(request.MotDePasse),
            Role = RoleUtilisateur.AdminTenant,
            EstActif = true,
        };
        _db.Utilisateurs.Add(admin);

        // 5. ─── AJOUT CRUCIAL : Initialisation Dynamique des Classes Comptables ───
        var classesParDefaut = new List<ClasseComptable>
    {
        new() { Numero = 1, Libelle = "Capitaux", TenantId = tenant.Id },
        new() { Numero = 2, Libelle = "Immobilisations", TenantId = tenant.Id },
        new() { Numero = 3, Libelle = "Stocks", TenantId = tenant.Id },
        new() { Numero = 4, Libelle = "Tiers", TenantId = tenant.Id },
        new() { Numero = 5, Libelle = "Trésorerie", TenantId = tenant.Id },
        new() { Numero = 6, Libelle = "Charges", TenantId = tenant.Id },
        new() { Numero = 7, Libelle = "Produits", TenantId = tenant.Id }
    };
        _db.ClassesComptables.AddRange(classesParDefaut);

        // 6. Génération des sous-comptes OHADA (Générés via ton Seed)
        _db.CompteComptables.AddRange(PlanComptableOhadaSeed.GenererPourTenant(tenant.Id));

        // 7. Initialisation du premier Exercice Comptable
        var annee = DateTime.Today.Year;
        _db.Exercices.Add(new ExerciceComptable
        {
            TenantId = tenant.Id,
            Annee = annee,
            DateDebut = new DateTime(annee, 1, 1),
            DateFin = new DateTime(annee, 12, 31),
        });

        // 8. Sauvegarde atomique globale en BDD
        await _db.SaveChangesAsync();

        return await EmettreTokensAsync(admin, tenant);
    }
    //public async Task<AuthResponse> RegisterTenantAsync(RegisterTenantRequest request)
    //{
    //    // Vérifier que le pays existe en BDD
    //    var pays = await _db.Pays
    //        .FirstOrDefaultAsync(p => p.Id == request.PaysId && p.EstActif)
    //        ?? throw new KeyNotFoundException("Pays introuvable. Consultez GET /api/pays.");

    //    var emailExiste = await _db.Utilisateurs
    //        .IgnoreQueryFilters()
    //        .AnyAsync(u => u.Email == request.Email.ToLower());

    //    if (emailExiste)
    //        throw new InvalidOperationException("Cet email est déjà utilisé.");

    //    var slug = GenererSlug(request.NomEntreprise);
    //    if (await _db.Tenants.AnyAsync(t => t.SlugUnique == slug))
    //        slug = $"{slug}-{Guid.NewGuid().ToString()[..4]}";

    //    // Créer le tenant — devise et TVA depuis la BDD
    //    var tenant = new Tenant
    //    {
    //        Nom = request.NomEntreprise,
    //        SlugUnique = slug,
    //        Email = request.Email.ToLower(),
    //        PaysId = pays.Id,
    //        Pays = pays,
    //        DeviseBase = pays.DeviseCode,   // XOF, STD, XAF...
    //        TauxTVA = pays.TauxTVAStandard,
    //        PlanComptableCode = "OHADA",
    //        Plan = PlanAbonnement.Trial,
    //        AbonnementExpireAt = DateTime.UtcNow.AddDays(14),
    //        EstActif = true,
    //    };
    //    _db.Tenants.Add(tenant);

    //    var admin = new Utilisateur
    //    {
    //        TenantId = tenant.Id,
    //        Nom = request.NomAdministrateur,
    //        Prenom = request.PrenomAdministrateur,
    //        Email = request.Email.ToLower(),
    //        MotDePasseHash = BCrypt.Net.BCrypt.HashPassword(request.MotDePasse),
    //        Role = RoleUtilisateur.AdminTenant,
    //        EstActif = true,
    //    };
    //    _db.Utilisateurs.Add(admin);

    //    _db.CompteComptables.AddRange(PlanComptableOhadaSeed.GenererPourTenant(tenant.Id));

    //    var annee = DateTime.Today.Year;
    //    _db.Exercices.Add(new ExerciceComptable
    //    {
    //        TenantId = tenant.Id,
    //        Annee = annee,
    //        DateDebut = new DateTime(annee, 1, 1),
    //        DateFin = new DateTime(annee, 12, 31),
    //    });

    //    await _db.SaveChangesAsync();
    //    return await EmettreTokensAsync(admin, tenant);
    //}

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request)
    {
        var principal = _jwt.ValiderToken(request.AccessToken);
        if (principal is null)
            throw new UnauthorizedAccessException("Token invalide.");

        var utilisateurId = Guid.Parse(
            principal.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)!.Value);

        var utilisateur = await _db.Utilisateurs
            .IgnoreQueryFilters()
            .Include(u => u.Tenant)
                .ThenInclude(t => t.Pays)
            .FirstOrDefaultAsync(u => u.Id == utilisateurId);

        if (utilisateur is null
            || utilisateur.RefreshToken != request.RefreshToken
            || utilisateur.RefreshTokenExpireAt < DateTime.UtcNow)
            throw new UnauthorizedAccessException("Refresh token expiré ou invalide.");

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

    private async Task<AuthResponse> EmettreTokensAsync(Utilisateur utilisateur, Tenant tenant)
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
            TenantId: tenant.Id,
            NomEntreprise: tenant.Nom,
            Pays: tenant.Pays.Nom,
            Devise: tenant.DeviseBase,
            DeviseSymbole: tenant.Pays.DeviseSymbole,
            TauxTVA: tenant.TauxTVA,
            RequiresOtp: false,
            Message: null
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
}