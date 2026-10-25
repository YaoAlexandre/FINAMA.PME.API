using Finama.API.Middleware;
using Finama.Core.Entities;
using Finama.Infrastructure.Data;
using Finama.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Finama.API.Controllers;

/// <summary>
/// Mode support : le SuperAdmin voit l'application exactement comme une entreprise cliente.
///  • jeton dédié, courte durée (15 min à 4 h), sans refresh : à l'expiration on revient à la session SuperAdmin ;
///  • lecture seule par défaut (SupportMiddleware bloque toute écriture) ;
///  • chaque accès est journalisé et visible par l'administrateur de l'entreprise.
/// </summary>
[ApiController]
[Authorize]
public class SupportController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtSettings _jwt;

    public SupportController(AppDbContext db, IOptions<JwtSettings> jwt)
    {
        _db = db;
        _jwt = jwt.Value;
    }

    /// <summary>Ouvre une session support sur une entreprise. Réservé au SuperAdmin hors session support.</summary>
    [HttpPost("api/superadmin/support/{tenantId:guid}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Demarrer(Guid tenantId, [FromBody] DemarrerSupportRequest request, CancellationToken ct)
    {
        if (User.HasClaim(c => c.Type == SupportClaims.SessionId))
            return BadRequest(new { message = "Quittez d'abord la session support en cours." });

        if (string.IsNullOrWhiteSpace(request.Motif) || request.Motif.Trim().Length < 5)
            return BadRequest(new { message = "Indiquez le motif de l'accès (ex. « Demande du client : balance déséquilibrée »)." });

        var duree = Math.Clamp(request.DureeMinutes, 15, 240);

        if (!Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var superAdminId))
            return Unauthorized();

        var superAdmin = await _db.Utilisateurs.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == superAdminId && u.Role == RoleUtilisateur.SuperAdmin && u.EstActif, ct);
        if (superAdmin is null) return Unauthorized();

        var tenant = await _db.Tenants.Include(t => t.Pays).AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null) return NotFound(new { message = "Entreprise introuvable." });

        var maintenant = DateTime.UtcNow;
        var acces = new AccesSupport
        {
            TenantId = tenant.Id,
            SuperAdminId = superAdmin.Id,
            SuperAdminNom = $"{superAdmin.Prenom} {superAdmin.Nom}".Trim(),
            Motif = request.Motif.Trim(),
            LectureSeule = request.LectureSeule,
            DateDebut = maintenant,
            DateExpiration = maintenant.AddMinutes(duree),
            AdresseIp = HttpContext.Connection.RemoteIpAddress?.ToString()
        };
        _db.AccesSupports.Add(acces);
        await _db.SaveChangesAsync(ct);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, superAdmin.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, superAdmin.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, RoleUtilisateur.SuperAdmin.ToString()),
            new("nom", acces.SuperAdminNom),
            new("tenant_id", tenant.Id.ToString()),
            new("tenant_slug", tenant.SlugUnique),
            new("tenant_nom", tenant.Nom),
            new(SupportClaims.SessionId, acces.Id.ToString()),
            new(SupportClaims.LectureSeule, request.LectureSeule ? "1" : "0"),
        };

        var jeton = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: acces.DateExpiration,
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SecretKey)), SecurityAlgorithms.HmacSha256));

        return Ok(new
        {
            accessToken = new JwtSecurityTokenHandler().WriteToken(jeton),
            expireAt = acces.DateExpiration,
            tenantNom = tenant.Nom,
            devise = tenant.DeviseBase,
            deviseSymbole = tenant.Pays?.DeviseSymbole ?? "FCFA",
            lectureSeule = acces.LectureSeule
        });
    }

    /// <summary>Termine la session support en cours (appelé avec le jeton support).</summary>
    [HttpPost("api/superadmin/support/terminer")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Terminer(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst(SupportClaims.SessionId)?.Value, out var sessionId))
            return Ok(new { message = "Aucune session support en cours." });

        var acces = await _db.AccesSupports.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Id == sessionId, ct);
        if (acces is { DateFin: null })
        {
            acces.DateFin = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        return Ok(new { message = "Session support terminée." });
    }

    /// <summary>Journal des accès support de l'entreprise connectée (administrateur de l'entreprise).</summary>
    [HttpGet("api/entreprise/acces-support")]
    [Authorize(Policy = Policies.AdminTenant)]
    public async Task<IActionResult> Journal(CancellationToken ct)
    {
        var lignes = await _db.AccesSupports.AsNoTracking()
            .OrderByDescending(a => a.DateDebut)
            .Take(100)
            .Select(a => new
            {
                a.Id, a.SuperAdminNom, a.Motif, a.LectureSeule, a.DateDebut, a.DateExpiration, a.DateFin,
                enCours = a.DateFin == null && a.DateExpiration > DateTime.UtcNow
            })
            .ToListAsync(ct);

        return Ok(lignes);
    }

    public record DemarrerSupportRequest(string Motif, int DureeMinutes = 60, bool LectureSeule = true);
}
