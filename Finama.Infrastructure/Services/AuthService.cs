using Microsoft.EntityFrameworkCore;
using Finama.Core.DTOs;
using Finama.Core.Entities;
using Finama.Infrastructure.Data;
using Finama.Infrastructure.Data.Seeds;
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

    // ─── Login ────────────────────────────────────────────────────────────────
    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        // Cherche l'utilisateur sans filtre tenant (on ne connaît pas encore le tenant)
        var utilisateur = await _db.Utilisateurs
            .IgnoreQueryFilters()
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Email == request.Email.ToLower()
                                   && !u.IsDeleted
                                   && u.EstActif);

        if (utilisateur is null || !BCrypt.Net.BCrypt.Verify(request.MotDePasse, utilisateur.MotDePasseHash))
            throw new UnauthorizedAccessException("Email ou mot de passe incorrect.");

        if (!utilisateur.Tenant.EstActif)
            throw new UnauthorizedAccessException("Ce compte entreprise est suspendu.");

        return await EmettreTokensAsync(utilisateur, utilisateur.Tenant);
    }

    // ─── Inscription nouveau tenant ───────────────────────────────────────────
    public async Task<AuthResponse> RegisterTenantAsync(RegisterTenantRequest request)
    {
        var emailExiste = await _db.Utilisateurs
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email == request.Email.ToLower());

        if (emailExiste)
            throw new InvalidOperationException("Cet email est déjà utilisé.");

        var slug = GenererSlug(request.NomEntreprise);
        if (await _db.Tenants.AnyAsync(t => t.SlugUnique == slug))
            slug = $"{slug}-{Guid.NewGuid().ToString()[..4]}";

        // Créer le tenant
        var tenant = new Tenant
        {
            Nom              = request.NomEntreprise,
            SlugUnique       = slug,
            Email            = request.Email.ToLower(),
            DeviseBase       = request.DeviseBase,
            PlanComptableCode= request.PlanComptableCode,
            Plan             = PlanAbonnement.Trial,
            AbonnementExpireAt = DateTime.UtcNow.AddDays(14), // 14 jours d'essai
            EstActif         = true,
        };
        _db.Tenants.Add(tenant);

        // Créer l'administrateur
        var admin = new Utilisateur
        {
            TenantId        = tenant.Id,
            Nom             = request.NomAdministrateur,
            Prenom          = request.PrenomAdministrateur,
            Email           = request.Email.ToLower(),
            MotDePasseHash  = BCrypt.Net.BCrypt.HashPassword(request.MotDePasse),
            Role            = RoleUtilisateur.AdminTenant,
            EstActif        = true,
        };
        _db.Utilisateurs.Add(admin);

        // Injecter le plan comptable OHADA
        var comptes = PlanComptableOhadaSeed.GenererPourTenant(tenant.Id);
        _db.CompteComptables.AddRange(comptes);

        // Créer l'exercice comptable de l'année en cours
        var annee = DateTime.Today.Year;
        var exercice = new ExerciceComptable
        {
            TenantId  = tenant.Id,
            Annee     = annee,
            DateDebut = new DateTime(annee, 1, 1),
            DateFin   = new DateTime(annee, 12, 31),
        };
        _db.Exercices.Add(exercice);

        await _db.SaveChangesAsync();

        return await EmettreTokensAsync(admin, tenant);
    }

    // ─── Refresh token ────────────────────────────────────────────────────────
    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request)
    {
        var principal = _jwt.ValiderToken(request.AccessToken);
        if (principal is null)
            throw new UnauthorizedAccessException("Token invalide.");

        var utilisateurId = Guid.Parse(principal.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)!.Value);

        var utilisateur = await _db.Utilisateurs
            .IgnoreQueryFilters()
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Id == utilisateurId);

        if (utilisateur is null
            || utilisateur.RefreshToken != request.RefreshToken
            || utilisateur.RefreshTokenExpireAt < DateTime.UtcNow)
            throw new UnauthorizedAccessException("Refresh token expiré ou invalide.");

        return await EmettreTokensAsync(utilisateur, utilisateur.Tenant);
    }

    // ─── Logout ───────────────────────────────────────────────────────────────
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

    // ─── Helpers ──────────────────────────────────────────────────────────────
    private async Task<AuthResponse> EmettreTokensAsync(Utilisateur utilisateur, Tenant tenant)
    {
        var accessToken  = _jwt.GenererAccessToken(utilisateur, tenant);
        var refreshToken = _jwt.GenererRefreshToken();

        utilisateur.RefreshToken          = refreshToken;
        utilisateur.RefreshTokenExpireAt  = DateTime.UtcNow.AddDays(_settings.RefreshTokenDureeJours);
        utilisateur.DerniereConnexionAt   = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return new AuthResponse(
            AccessToken:  accessToken,
            RefreshToken: refreshToken,
            ExpireAt:     DateTime.UtcNow.AddMinutes(_settings.AccessTokenDureeMinutes),
            NomUtilisateur: $"{utilisateur.Prenom} {utilisateur.Nom}",
            Email:        utilisateur.Email,
            Role:         utilisateur.Role.ToString(),
            TenantId:     tenant.Id,
            NomEntreprise: tenant.Nom
        );
    }

    private static string GenererSlug(string nom)
    {
        var slug = nom.ToLower()
            .Replace("é", "e").Replace("è", "e").Replace("ê", "e")
            .Replace("à", "a").Replace("â", "a")
            .Replace("ç", "c").Replace("ô", "o").Replace("ù", "u");
        return System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9]+", "-").Trim('-');
    }
}
