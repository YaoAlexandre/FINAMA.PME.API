using System.Net;
using System.Text.Json;

namespace Finama.API.Middleware;

/// <summary>
/// Intercepte toutes les exceptions non gérées et retourne
/// une réponse JSON cohérente — jamais de stack trace en production.
/// </summary>
public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionMiddleware(
        RequestDelegate next,
        ILogger<ExceptionMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await GererExceptionAsync(context, ex);
        }
    }

    private async Task GererExceptionAsync(HttpContext context, Exception ex)
    {
        var (statusCode, message) = ex switch
        {
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, ex.Message),
            KeyNotFoundException => (HttpStatusCode.NotFound, ex.Message),
            InvalidOperationException => (HttpStatusCode.UnprocessableEntity, ex.Message),
            ArgumentException => (HttpStatusCode.BadRequest, ex.Message),
            _ => (HttpStatusCode.InternalServerError, "Une erreur interne est survenue.")
        };

        // Log toujours l'exception complète côté serveur
        if (statusCode == HttpStatusCode.InternalServerError)
            _logger.LogError(ex, "Erreur non gérée sur {Method} {Path}",
                context.Request.Method, context.Request.Path);
        else
            _logger.LogWarning("Erreur {StatusCode} sur {Method} {Path} : {Message}",
                (int)statusCode, context.Request.Method, context.Request.Path, ex.Message);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var reponse = new
        {
            statut = (int)statusCode,
            message,
            chemin = context.Request.Path.Value,
            // Stack trace uniquement en développement
            detail = _env.IsDevelopment() ? ex.ToString() : null,
        };

        var json = JsonSerializer.Serialize(reponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}

// Extension pour enregistrer proprement le middleware
public static class ExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder app)
        => app.UseMiddleware<ExceptionMiddleware>();
}
