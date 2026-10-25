using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Finama.Core.Entities;
using Finama.Infrastructure.Data;
using Finama.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Finama.API.Controllers;

/// <summary>Abonnement des appareils aux notifications (chaque utilisateur, pour ses propres appareils).</summary>
[ApiController]
[Route("api/push")]
[Authorize]
public class PushController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPushService _push;
    private readonly ITenantContext _tenant;

    public PushController(AppDbContext db, IPushService push, ITenantContext tenant)
    {
        _db = db;
        _push = push;
        _tenant = tenant;
    }

    public record AbonnementRequest(string Endpoint, string P256dh, string Auth, string? Appareil);
    public record RetraitRequest(string Endpoint);

    [HttpGet("config")]
    public async Task<IActionResult> Config(CancellationToken ct)
    {
        var id = UtilisateurId();
        var appareils = id is null ? 0 : await _db.Set<AbonnementPush>().IgnoreQueryFilters()
            .CountAsync(a => a.UtilisateurId == id && !a.IsDeleted, ct);
        return Ok(new { actif = _push.EstActif, clePublique = _push.ClePublique, appareils });
    }

    [HttpPost("abonnements")]
    public async Task<IActionResult> Abonner([FromBody] AbonnementRequest r, CancellationToken ct)
    {
        if (!_push.EstActif) return BadRequest(new { message = "Les notifications ne sont pas encore activées sur le serveur." });
        // SuperAdmin : appareil « plateforme » (sans entreprise) → alertes de pilotage du SaaS
        var estSuperAdmin = User.IsInRole("SuperAdmin");
        Guid? tenantId = estSuperAdmin ? null : _tenant.TenantId;
        if (UtilisateurId() is not { } utilisateurId || (!estSuperAdmin && tenantId is null))
            return BadRequest(new { message = "Les notifications sont réservées aux utilisateurs d'une entreprise." });
        if (string.IsNullOrWhiteSpace(r.Endpoint) || !Uri.TryCreate(r.Endpoint, UriKind.Absolute, out var url) || url.Scheme != "https"
            || r.Endpoint.Length > 1000 || string.IsNullOrWhiteSpace(r.P256dh) || r.P256dh.Length > 200 || string.IsNullOrWhiteSpace(r.Auth) || r.Auth.Length > 100)
            return BadRequest(new { message = "Abonnement invalide." });

        // Un appareil = un abonnement : on le rattache à l'utilisateur connecté (cas d'un téléphone partagé)
        var existant = await _db.Set<AbonnementPush>().IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Endpoint == r.Endpoint, ct);
        if (existant is null)
        {
            _db.Set<AbonnementPush>().Add(new AbonnementPush
            {
                TenantId = tenantId, UtilisateurId = utilisateurId,
                Endpoint = r.Endpoint, P256dh = r.P256dh, Auth = r.Auth, Appareil = Tronquer(r.Appareil, 100)
            });
        }
        else
        {
            existant.TenantId = tenantId;
            existant.UtilisateurId = utilisateurId;
            existant.P256dh = r.P256dh;
            existant.Auth = r.Auth;
            existant.IsDeleted = false;
            existant.DateAbonnement = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "Notifications activées sur cet appareil." });
    }

    [HttpPost("abonnements/retirer")]
    public async Task<IActionResult> Retirer([FromBody] RetraitRequest r, CancellationToken ct)
    {
        var id = UtilisateurId();
        var a = await _db.Set<AbonnementPush>().IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Endpoint == r.Endpoint && x.UtilisateurId == id, ct);
        if (a is not null)
        {
            _db.Remove(a);
            await _db.SaveChangesAsync(ct);
        }
        return Ok(new { message = "Notifications désactivées sur cet appareil." });
    }

    [HttpPost("test")]
    public async Task<IActionResult> Test(CancellationToken ct)
    {
        var estSuperAdmin = User.IsInRole("SuperAdmin");
        Guid? tenantId = estSuperAdmin ? null : _tenant.TenantId;
        if (UtilisateurId() is not { } id || (!estSuperAdmin && tenantId is null)) return BadRequest(new { message = "Utilisateur inconnu." });
        var n = await _push.EnvoyerAsync(tenantId, [id],
            new MessagePush("Finama", "Les notifications fonctionnent sur cet appareil ✅", "/modules", "test"), ct);
        return n > 0
            ? Ok(new { message = $"Notification envoyée à {n} appareil(s)." })
                        : BadRequest(new { message = _push.DerniereErreur ?? "Aucun appareil n'a reçu la notification." });
    }

    private static string? Tronquer(string? texte, int max)
        => string.IsNullOrWhiteSpace(texte) ? null : texte.Trim().Length <= max ? texte.Trim() : texte.Trim()[..max];

    private Guid? UtilisateurId()
        => Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) ? id : null;
}
