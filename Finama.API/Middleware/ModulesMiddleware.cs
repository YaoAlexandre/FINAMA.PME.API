using Finama.Core.Entities;
using Finama.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;

namespace Finama.API.Middleware;

/// <summary>
/// Bloque l'accès aux API d'un module non inclus dans l'abonnement de l'entreprise (403 MODULE_INACTIF).
/// Contrôle centralisé par préfixe de route : aucun contrôleur à modifier.
/// Les modules sont mis en cache 60 s par entreprise ; une modification par le SuperAdmin vide le cache immédiatement.
/// </summary>
public class ModulesMiddleware
{
    private static readonly (string Prefixe, ModuleFinama Module)[] Routes =
    [
        // Commerce & Ventes
        ("/api/commerce", ModuleFinama.Commerce),
        ("/api/exports/fin-journee", ModuleFinama.Commerce),
        ("/api/exports/stocks", ModuleFinama.Commerce),
        ("/api/exports/inventaires", ModuleFinama.Commerce),
        ("/api/exports/clients", ModuleFinama.Commerce),

        // Ventes & Devis (module « Gestion commerciale »)
        ("/api/devis", ModuleFinama.GestionCommerciale),

        // Partagés : clients/fournisseurs et factures servent aux deux modules (l'un OU l'autre suffit)
        ("/api/tiers", ModuleFinama.Comptabilite | ModuleFinama.GestionCommerciale),
        ("/api/factures", ModuleFinama.Comptabilite | ModuleFinama.GestionCommerciale),

        // Finance & Compta
        ("/api/ecritures", ModuleFinama.Comptabilite),
        ("/api/paiement", ModuleFinama.Comptabilite),
        ("/api/reporting", ModuleFinama.Comptabilite),
        ("/api/etatscomptables", ModuleFinama.Comptabilite),
        ("/api/plancomptable", ModuleFinama.Comptabilite),
        ("/api/classescomptables", ModuleFinama.Comptabilite),
        ("/api/tableaubord", ModuleFinama.Comptabilite),
        ("/api/exports/balance", ModuleFinama.Comptabilite),
        ("/api/exports/grand-livre", ModuleFinama.Comptabilite),
        ("/api/exports/journal", ModuleFinama.Comptabilite),
        ("/api/exports/etats-financiers", ModuleFinama.Comptabilite),
    ];

    private readonly RequestDelegate _next;

    public ModulesMiddleware(RequestDelegate next) => _next = next;

    public static string CleCache(Guid tenantId) => $"modules:{tenantId}";

    public async Task InvokeAsync(HttpContext context, AppDbContext db, IMemoryCache cache)
    {
        var module = ModuleRequis(context.Request.Path);

        // Pas d'entreprise dans le jeton (SuperAdmin hors support) : rien à contrôler ici.
        // En mode support, le SuperAdmin porte l'entreprise du client : mêmes modules que le client.
        if (module is null
            || context.User.Identity?.IsAuthenticated != true
            || !Guid.TryParse(context.User.FindFirst("tenant_id")?.Value, out var tenantId))
        {
            await _next(context);
            return;
        }

        var actifs = await cache.GetOrCreateAsync(CleCache(tenantId), async entree =>
        {
            entree.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
            return await db.Tenants.AsNoTracking()
                .Where(t => t.Id == tenantId)
                .Select(t => (ModuleFinama?)t.Modules)
                .FirstOrDefaultAsync() ?? ModuleFinama.Aucun;
        });

        // Une route peut appartenir à plusieurs modules : il suffit d'en avoir un
        if ((actifs & module.Value) == 0)
        {
            var libelle = string.Join(" ou ", ModulesFinama.Liste.Where(m => module.Value.HasFlag(m.Module)).Select(m => m.Libelle));
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                statut = 403,
                code = "MODULE_INACTIF",
                module = module.Value.ToString(),
                message = $"Le module « {libelle} » n'est pas inclus dans l'abonnement de votre entreprise."
            });
            return;
        }

        await _next(context);
    }

    private static ModuleFinama? ModuleRequis(PathString chemin)
    {
        foreach (var (prefixe, module) in Routes)
            if (chemin.StartsWithSegments(prefixe, StringComparison.OrdinalIgnoreCase))
                return module;
        return null;
    }
}

public static class ModulesMiddlewareExtensions
{
    public static IApplicationBuilder UseModulesFinama(this IApplicationBuilder app)
        => app.UseMiddleware<ModulesMiddleware>();
}
