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
    string GenererAccessToken(Utilisateur utilisateur, Tenant tenant);
    string GenererRefreshToken();
    ClaimsPrincipal? ValiderToken(string token);
}

public class JwtService : IJwtService
{
    private readonly JwtSettings _settings;

    public JwtService(IOptions<JwtSettings> options)
    {
        _settings = options.Value;
    }

    public string GenererAccessToken(Utilisateur utilisateur, Tenant tenant)
    {
        var cle = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
        var credentials = new SigningCredentials(cle, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   utilisateur.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, utilisateur.Email),
            new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
            new Claim("tenant_id",   tenant.Id.ToString()),
            new Claim("tenant_slug", tenant.SlugUnique),
            new Claim("tenant_nom",  tenant.Nom),
            new Claim("role",        ((RoleUtilisateur)utilisateur.Role).ToString()),
            new Claim("nom",         $"{utilisateur.Prenom} {utilisateur.Nom}"),
        };

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_settings.AccessTokenDureeMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenererRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }

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
                ValidateLifetime = false, // on valide nous-mêmes pour le refresh
                ClockSkew = TimeSpan.Zero,
            }, out _);

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
