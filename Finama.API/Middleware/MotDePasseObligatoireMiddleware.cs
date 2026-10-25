namespace Finama.API.Middleware;

/// <summary>
/// Tant que le JWT porte le claim "pwd_change=1" (mot de passe temporaire), seul /api/auth/* est accessible.
/// Empêche de contourner la page « Changer le mot de passe » en naviguant directement dans l'application.
/// </summary>
public class MotDePasseObligatoireMiddleware
{
    private readonly RequestDelegate _next;

    public MotDePasseObligatoireMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && context.User.HasClaim(JwtClaims.ChangementMotDePasse, "1")
            && !context.Request.Path.StartsWithSegments("/api/auth")
            && !context.Request.Path.StartsWithSegments("/health"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                "{\"statut\":403,\"code\":\"PWD_CHANGE_REQUIRED\",\"message\":\"Vous devez changer votre mot de passe temporaire avant de continuer.\"}");
            return;
        }

        await _next(context);
    }
}

public static class JwtClaims
{
    public const string ChangementMotDePasse = "pwd_change";
}

public static class MotDePasseObligatoireExtensions
{
    public static IApplicationBuilder UseMotDePasseObligatoire(this IApplicationBuilder app)
        => app.UseMiddleware<MotDePasseObligatoireMiddleware>();
}
