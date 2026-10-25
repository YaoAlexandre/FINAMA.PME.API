namespace Finama.API.Middleware;

public static class SupportClaims
{
    public const string SessionId = "support_id";
    public const string LectureSeule = "support_ro";
}

/// <summary>
/// Session support en lecture seule : toute requête d'écriture (POST, PUT, PATCH, DELETE) est refusée,
/// sauf pour quitter la session et pour l'authentification. Les exports et consultations restent possibles.
/// </summary>
public class SupportMiddleware
{
    private static readonly string[] MethodesLecture = ["GET", "HEAD", "OPTIONS"];
    private readonly RequestDelegate _next;

    public SupportMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var lectureSeule = context.User.Identity?.IsAuthenticated == true
                           && context.User.HasClaim(SupportClaims.LectureSeule, "1");

        if (lectureSeule
            && !MethodesLecture.Contains(context.Request.Method, StringComparer.OrdinalIgnoreCase)
            && !context.Request.Path.StartsWithSegments("/api/auth")
            && !context.Request.Path.StartsWithSegments("/api/superadmin/support"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                statut = 403,
                code = "SUPPORT_LECTURE_SEULE",
                message = "Mode support en lecture seule : aucune modification n'est possible sur les données du client."
            });
            return;
        }

        await _next(context);
    }
}

public static class SupportMiddlewareExtensions
{
    public static IApplicationBuilder UseSupportFinama(this IApplicationBuilder app)
        => app.UseMiddleware<SupportMiddleware>();
}
