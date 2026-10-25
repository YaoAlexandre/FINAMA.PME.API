using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Finama.Core.Entities;
using Finama.Core.Securite;
using Finama.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Finama.Infrastructure.Services;

public interface IPermissionsService
{
    /// <summary>Permissions de l'utilisateur connecté (cache 60 s).</summary>
    Task<IReadOnlySet<string>> DeAsync(ClaimsPrincipal user);

    /// <summary>Permissions d'un utilisateur quelconque (ex. le gérant qui valide une annulation).</summary>
    Task<IReadOnlySet<string>> DeUtilisateurAsync(Guid utilisateurId);

    /// <summary>À appeler après modification d'un profil ou d'un accès : effet immédiat pour toute l'entreprise.</summary>
    void Invalider(Guid? tenantId);
}

/// <summary>
/// Résout les permissions : profil personnalisé s'il existe, sinon profil prédéfini du rôle. SuperAdmin : tout.
/// Lecture en base mise en cache ; une version par entreprise permet d'invalider d'un coup tous ses utilisateurs.
/// </summary>
public class PermissionsService : IPermissionsService
{
    private static readonly ConcurrentDictionary<Guid, int> Versions = new();
    private static readonly TimeSpan Duree = TimeSpan.FromSeconds(60);

    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;

    public PermissionsService(AppDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<IReadOnlySet<string>> DeAsync(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true) return new HashSet<string>();
        if (user.IsInRole("SuperAdmin")) return PermissionsFinama.Codes;

        var id = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(id, out var utilisateurId) ? await DeUtilisateurAsync(utilisateurId) : new HashSet<string>();
    }

    public async Task<IReadOnlySet<string>> DeUtilisateurAsync(Guid utilisateurId)
    {
        var u = await _db.Utilisateurs.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Id == utilisateurId)
            .Select(x => new { x.Role, x.ProfilId, x.TenantId, x.EstActif })
            .FirstOrDefaultAsync();

        if (u is null || !u.EstActif) return new HashSet<string>();
        if (u.Role == RoleUtilisateur.SuperAdmin) return PermissionsFinama.Codes;

        var version = u.TenantId is { } t ? Versions.GetOrAdd(t, 0) : 0;
        var cle = $"perm:{version}:{utilisateurId}:{u.Role}:{u.ProfilId}";

        return await _cache.GetOrCreateAsync(cle, async entree =>
        {
            entree.AbsoluteExpirationRelativeToNow = Duree;
            if (u.ProfilId is { } profilId)
            {
                var stockees = await _db.Set<ProfilAcces>().IgnoreQueryFilters().AsNoTracking()
                    .Where(p => p.Id == profilId && p.TenantId == u.TenantId && !p.IsDeleted)
                    .Select(p => p.Permissions)
                    .FirstOrDefaultAsync();
                if (stockees is not null) return (IReadOnlySet<string>)PermissionsFinama.Lire(stockees);
            }
            return PermissionsFinama.DuRole(u.Role);
        }) ?? new HashSet<string>();
    }

    public void Invalider(Guid? tenantId)
    {
        if (tenantId is { } t) Versions.AddOrUpdate(t, 1, (_, v) => v + 1);
    }
}
