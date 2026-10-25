using Finama.Commerce.Application.Common;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.Json;

namespace Finama.API.Middleware;

/// <summary>
/// Intercepte toutes les exceptions non gérées et retourne une réponse JSON homogène :
/// { statut, message, erreurs?, chemin, detail? } — jamais de stack trace hors développement.
/// </summary>
public class ExceptionMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger, IHostEnvironment env)
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
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client parti (onglet fermé, réseau coupé) : rien à renvoyer.
        }
        catch (Exception ex)
        {
            await GererExceptionAsync(context, ex);
        }
    }

    private async Task GererExceptionAsync(HttpContext context, Exception ex)
    {
        IEnumerable<object>? erreurs = null;

        var (statusCode, message) = ex switch
        {
            BusinessRuleException => (HttpStatusCode.UnprocessableEntity, ex.Message),

            FluentValidation.ValidationException v => (HttpStatusCode.BadRequest,
                v.Errors.Any() ? string.Join(" ", v.Errors.Select(e => e.ErrorMessage).Distinct()) : "Données invalides."),

            DbUpdateConcurrencyException => (HttpStatusCode.Conflict,
                "Les données ont été modifiées par un autre utilisateur. Rechargez puis réessayez."),

            DbUpdateException => (HttpStatusCode.Conflict,
                "Opération refusée : doublon ou contrainte de données non respectée."),

            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, ex.Message),
            KeyNotFoundException => (HttpStatusCode.NotFound, ex.Message),
            InvalidOperationException => (HttpStatusCode.UnprocessableEntity, ex.Message),
            ArgumentException => (HttpStatusCode.BadRequest, ex.Message),
            NotFoundException => (HttpStatusCode.NotFound, ex.Message),
            _ => (HttpStatusCode.InternalServerError, "Une erreur interne est survenue.")
        };

        if (ex is FluentValidation.ValidationException ve)
            erreurs = ve.Errors.Select(e => new { champ = e.PropertyName, message = e.ErrorMessage });

        // Rafale d'erreurs serveur → alerte au SuperAdmin (AlertesPlateformeService)
        if (statusCode == HttpStatusCode.InternalServerError)
            Finama.API.Services.CompteurErreurs.Signaler(context.Request.Path);

        if (statusCode == HttpStatusCode.InternalServerError || ex is DbUpdateException)
            _logger.LogError(ex, "Erreur {StatusCode} sur {Method} {Path}", (int)statusCode, context.Request.Method, context.Request.Path);
        else
            _logger.LogWarning("Erreur {StatusCode} sur {Method} {Path} : {Message}",
                (int)statusCode, context.Request.Method, context.Request.Path, ex.Message);

        if (context.Response.HasStarted) return;

        // Pas de Response.Clear() : il effacerait les en-têtes CORS déjà posés (message illisible côté Blazor).
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var reponse = new
        {
            statut = (int)statusCode,
            message,
            erreurs,
            chemin = context.Request.Path.Value,
            detail = _env.IsDevelopment() ? ex.ToString() : null,
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(reponse, JsonOptions));
    }
}

public static class ExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder app)
        => app.UseMiddleware<ExceptionMiddleware>();
}
