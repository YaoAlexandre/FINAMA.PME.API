using Finama.Core.Securite;
using Finama.Infrastructure.Services;

namespace Finama.API.Middleware;

/// <summary>
/// Droits fins du commerce par route, sans modifier les contrôleurs (remplace RolesCommerceMiddleware).
/// Première règle qui correspond au chemin = permission exigée (lecture et/ou écriture).
/// Nouveau module : ajouter ses règles ici, ou mettre [Authorize(Policy = "perm:…")] sur ses contrôleurs.
/// </summary>
public class PermissionsCommerceMiddleware
{
    private sealed record Regle(string Prefixe, string[]? Lecture, string[]? Ecriture);

    private static readonly string[] MethodesLecture = ["GET", "HEAD", "OPTIONS"];

    private static readonly Regle[] Regles =
    [
        new("/api/commerce/pos/ventes", null, [Perm.CaisseVendre]),       // annulation : contrôlée dans son contrôleur
        new("/api/commerce/pos", [Perm.CaisseVendre, Perm.CaisseFinJournee], [Perm.CaisseVendre]),
        new("/api/commerce/clients", [Perm.CaisseVendre, Perm.VentesDevis], [Perm.CaisseVendre]),
        new("/api/commerce/stock/inventaires", [Perm.StockConsulter, Perm.StockInventaire], [Perm.StockInventaire]),
        new("/api/commerce/stock", [Perm.StockConsulter, Perm.CaisseVendre], [Perm.StockGerer]),
        new("/api/commerce/produits", [Perm.StockConsulter, Perm.CaisseVendre, Perm.VentesDevis], [Perm.StockGerer]),
        new("/api/commerce/categories", [Perm.StockConsulter, Perm.CaisseVendre], [Perm.StockGerer]),
        new("/api/commerce/editeurs", [Perm.StockConsulter, Perm.CaisseVendre], [Perm.StockGerer]),
        new("/api/commerce/magasins", [Perm.StockConsulter, Perm.CaisseVendre], [Perm.AdminParametres]),
        new("/api/commerce/parametres", [Perm.StockConsulter, Perm.CaisseVendre], [Perm.AdminParametres]),
        new("/api/exports/fin-journee", [Perm.CaisseFinJournee], null),
        new("/api/exports/stocks", [Perm.StockConsulter], null),
        new("/api/exports/inventaires", [Perm.StockConsulter, Perm.StockInventaire], null),
        new("/api/exports/clients", [Perm.CaisseVendre, Perm.VentesDevis], null),
    ];

    private readonly RequestDelegate _next;

    public PermissionsCommerceMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IPermissionsService permissions)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var regle = Regles.FirstOrDefault(r => context.Request.Path.StartsWithSegments(r.Prefixe, StringComparison.OrdinalIgnoreCase));
            if (regle is not null)
            {
                var lecture = MethodesLecture.Contains(context.Request.Method, StringComparer.OrdinalIgnoreCase);
                var exigees = lecture ? regle.Lecture : regle.Ecriture;

                if (exigees is not null)
                {
                    var accordees = await permissions.DeAsync(context.User);
                    if (!exigees.Any(accordees.Contains))
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsJsonAsync(new
                        {
                            statut = 403,
                            code = "PERMISSION_MANQUANTE",
                            message = "Votre profil ne permet pas cette action. Demandez à votre administrateur d'ajouter la permission nécessaire."
                        });
                        return;
                    }
                }
            }
        }

        await _next(context);
    }
}

public static class PermissionsCommerceMiddlewareExtensions
{
    public static IApplicationBuilder UsePermissionsCommerce(this IApplicationBuilder app)
        => app.UseMiddleware<PermissionsCommerceMiddleware>();
}
