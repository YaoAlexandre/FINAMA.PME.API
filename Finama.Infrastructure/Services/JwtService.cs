using Finama.Core.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Finama.Infrastructure.Services;

public interface IJwtService
{
    string GenererAccessToken(Utilisateur utilisateur, Tenant? tenant);
    string GenererRefreshToken();
    string HacherRefreshToken(string refreshToken);
    ClaimsPrincipal? ValiderToken(string token);
}

public class JwtService : IJwtService
{
    private readonly JwtSettings _settings;

    public JwtService(IOptions<JwtSettings> options)
    {
        _settings = options.Value;
    }

    public string GenererAccessToken(Utilisateur utilisateur, Tenant? tenant)
    {
        var cle = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
        var credentials = new SigningCredentials(cle, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,   utilisateur.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, utilisateur.Email),
            new(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
            new(ClaimTypes.Role,               utilisateur.Role.ToString()), // sérialisé en "role"
            new("nom",                         $"{utilisateur.Prenom} {utilisateur.Nom}"),
        };

        // Mot de passe temporaire : l'API refusera tout sauf /api/auth/* (MotDePasseObligatoireMiddleware)
        if (utilisateur.DoitChangerMotDePasse)
            claims.Add(new Claim("pwd_change", "1"));

        if (tenant is not null)
        {
            claims.Add(new Claim("tenant_id", tenant.Id.ToString()));
            claims.Add(new Claim("tenant_slug", tenant.SlugUnique));
            claims.Add(new Claim("tenant_nom", tenant.Nom));
        }

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_settings.AccessTokenDureeMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenererRefreshToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    /// <summary>Seul le hash est stocké en base : une fuite de la table Utilisateurs ne permet pas de voler une session.</summary>
    public string HacherRefreshToken(string refreshToken)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

    public ClaimsPrincipal? ValiderToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var cle = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));

        try
        {
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = cle,
                ValidateIssuer = true,
                ValidIssuer = _settings.Issuer,
                ValidateAudience = true,
                ValidAudience = _settings.Audience,
                ValidateLifetime = false, // le refresh accepte un access token expiré (signature vérifiée)
                ClockSkew = TimeSpan.Zero,
            }, out var jeton);

            // Refuse tout algorithme autre que HS256 (protection "alg confusion")
            if (jeton is not JwtSecurityToken jwt ||
                !jwt.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.OrdinalIgnoreCase))
                return null;

            return principal;
        }
        catch
        {
            return null;
        }
    }
}

public class JwtSettings
{
    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "Finama";
    public string Audience { get; set; } = "Finama.Clients";
    public int AccessTokenDureeMinutes { get; set; } = 60;
    public int RefreshTokenDureeJours { get; set; } = 30;
}
