using Finama.Core.DTOs;
using Finama.Core.Entities;
using Finama.Infrastructure.Data;
using Finama.Infrastructure.Seeds;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Finama.Infrastructure.Services;

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> RegisterTenantAsync(RegisterTenantRequest request);
    Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request);
    Task LogoutAsync(Guid utilisateurId);
}

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IJwtService _jwt;
    private readonly JwtSettings _settings;

    public AuthService(AppDbContext db, IJwtService jwt, IOptions<JwtSettings> options)
    {
        _db = db;
        _jwt = jwt;
        _settings = options.Value;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        // 1. On force les DEUX côtés de l'égalité en minuscules (.ToLower())
        var utilisateur = await _db.Utilisateurs
            .IgnoreQueryFilters()
            .Include(u => u.Tenant)
                .ThenInclude(t => t.Pays)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower()
                                   && !u.IsDeleted
                                   && u.EstActif);

        // 2. On applique un .Trim() sur le hash par sécurité pour Postgres
        if (utilisateur is null || !BCrypt.Net.BCrypt.Verify(request.MotDePasse, utilisateur.MotDePasseHash.Trim()))
            throw new UnauthorizedAccessException("Email ou mot de passe incorrect.");

        if (!utilisateur.Tenant.EstActif)
            throw new UnauthorizedAccessException("Ce compte entreprise est suspendu.");

        // ─── AUTO-CORRECTION : Le reste de ton code est parfait et ne change pas ───
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
            TauxTVA: tenant.TauxTVA
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