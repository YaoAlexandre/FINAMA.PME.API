using Finama.Core.Securite;
using Finama.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;

namespace Finama.API.Securite;

/// <summary>Exige AU MOINS UNE des permissions listées.</summary>
public sealed class PermissionRequirement(params string[] permissions) : IAuthorizationRequirement
{
    public IReadOnlyList<string> Permissions { get; } = permissions;
}

public sealed class PermissionHandler(IPermissionsService permissions) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var accordees = await permissions.DeAsync(context.User);
        if (requirement.Permissions.Any(accordees.Contains))
            context.Succeed(requirement);
    }
}

public static class PermissionPolicies
{
    /// <summary>
    /// Les noms de policies historiques sont conservés (aucun contrôleur à modifier) ; ils vérifient désormais des permissions.
    /// Nouveau module : ajouter ici sa (ses) policy(s) — ou utiliser directement [Authorize(Policy = "perm:code")].
    /// </summary>
    public static IServiceCollection AjouterPermissionsFinama(this IServiceCollection services)
    {
        services.AddScoped<IPermissionsService, PermissionsService>();
        services.AddScoped<IAuthorizationHandler, PermissionHandler>();

        services.AddAuthorization(o =>
        {
            void P(string nom, params string[] perms) => o.AddPolicy(nom, p => p.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(perms)));

            // Policies historiques
            P(Policies.AdminTenant, Perm.AdminParametres);
            P(Policies.Comptable, Perm.ComptaValider);
            P(Policies.Saisie, Perm.ComptaSaisir);
            P(Policies.LectureSeule, Perm.ComptaConsulter);
            P(Policies.Commercial, Perm.CaisseVendre, Perm.StockConsulter, Perm.StockGerer, Perm.VentesDevis);
            o.AddPolicy(Policies.SuperAdmin, p => p.RequireRole("SuperAdmin"));

            // Policies métier
            P(Policies.Utilisateurs, Perm.AdminUtilisateurs);
            P(Policies.Ventes, Perm.VentesDevis, Perm.VentesTousDevis);
            P(Policies.Caisse, Perm.CaisseVendre);
            P(Policies.FinJournee, Perm.CaisseFinJournee);
            P(Policies.Stock, Perm.StockConsulter, Perm.StockGerer);

            // Une policy par permission : [Authorize(Policy = "perm:stock.gerer")]
            foreach (var code in PermissionsFinama.Codes) P($"perm:{code}", code);
        });

        return services;
    }
}
