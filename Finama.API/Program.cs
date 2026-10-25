using Finama.API.Middleware;
using Finama.API.Securite;
using Finama.API.Services;
using Finama.Commerce.Infrastructure;
using Finama.Commerce.Infrastructure.Persistence;
using Finama.Commerce.Infrastructure.Services;
using Finama.Core.DTOs;
using Finama.Core.Validators;
using Finama.Infrastructure.Data;
using Finama.Infrastructure.Seeds;
using Finama.Infrastructure.Services;
using Finama.Infrastructure.Services.Commercials;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Text;
using System.Threading.RateLimiting;

// ⚠️ Conservé : JwtService.ValiderToken (refresh) lit le claim brut "sub".
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

var builder = WebApplication.CreateBuilder(args);

// ==============================================================================
// 🔌 CHARGEMENT DU .ENV (dev / VPS) & CONFIGURATION
// ==============================================================================
var envFilePath = new[]
{
    Path.Combine(Directory.GetCurrentDirectory(), ".env"),
    Path.Combine(Directory.GetParent(Directory.GetCurrentDirectory())?.FullName ?? "", ".env"),
    Path.Combine(AppContext.BaseDirectory, ".env")
}.FirstOrDefault(File.Exists);

if (envFilePath is not null)
{
    // Les valeurs fournies par le serveur (systemd, variables du système) restent PRIORITAIRES :
    // le .env ne fait que compléter ce qui manque (indispensable en développement, sans effet sur ce que le VPS impose).
    DotNetEnv.Env.Load(envFilePath, new DotNetEnv.LoadOptions(clobberExistingVars: false));
    Console.WriteLine($"[FINAMA CONFIG] .env chargé : {envFilePath}");
}

var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
builder.Environment.EnvironmentName = env;
Console.WriteLine($"[FINAMA CONFIG] Environnement : {env}");

builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// ─── Base de données (SQL Server : les migrations des deux contextes sont SQL Server) ───
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("ConnectionStrings__DefaultConnection est manquante.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.MigrationsAssembly("Finama.Infrastructure")));

// ─── Multi-tenant ─────────────────────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, TenantContext>();

// ─── Services métier ──────────────────────────────────────────────────────────
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IEcritureService, EcritureService>();
builder.Services.AddScoped<IReportingService, ReportingService>();
builder.Services.AddScoped<IFacturePdfService, FacturePdfService>();
builder.Services.AddScoped<IFactureService, FactureService>();
builder.Services.AddScoped<ITiersService, TiersService>();
builder.Services.AddScoped<ITableauBordService, TableauBordService>();
builder.Services.AddScoped<IPlanComptableService, PlanComptableService>();
builder.Services.AddScoped<IClasseComptableService, ClasseComptableService>();
builder.Services.AddScoped<ITenantInitializationService, TenantInitializationService>();
builder.Services.AddScoped<IDeviseService, DeviseService>();
builder.Services.AddScoped<IClotureService, ClotureService>();
builder.Services.AddTransient<IEmailService, EmailService>();
builder.Services.AddScoped<IDevisService, DevisService>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<ISuperAdminService, SuperAdminService>();
builder.Services.AddScoped<IDevisPdfService, DevisPdfService>();
builder.Services.Configure<PushOptions>(builder.Configuration.GetSection("Push"));
builder.Services.AddScoped<IPushService, PushService>();
builder.Services.AddHostedService<AlertesPushService>();
builder.Services.AddFinamaCommerce(builder.Configuration);
builder.Services.AddHostedService<AlertesPlateformeService>();
builder.Services.AddScoped<ILiaisonComptableService, LiaisonComptableService>();
builder.Services.AddScoped<IValidator<CreerEcritureRequest>, CreerEcritureValidator>();

// ─── Authentification JWT ─────────────────────────────────────────────────────
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
    ?? throw new InvalidOperationException("Section Jwt manquante.");
if (string.IsNullOrWhiteSpace(jwtSettings.SecretKey) || jwtSettings.SecretKey.Length < 32)
    throw new InvalidOperationException("Jwt__SecretKey doit faire au moins 32 caractères.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

// ─── Autorisation : RequireRole (le claim "role" est mappé en ClaimTypes.Role par JwtBearer .NET 9) ───

builder.Services.AjouterPermissionsFinama();
//builder.Services.AddAuthorization(options =>
//{
//    options.AddPolicy(Policies.AdminTenant, p => p.RequireRole("AdminTenant", "SuperAdmin"));
//    options.AddPolicy(Policies.Comptable, p => p.RequireRole("AdminTenant", "Comptable", "SuperAdmin"));
//    options.AddPolicy(Policies.Saisie, p => p.RequireRole("AdminTenant", "Comptable", "Collaborateur", "SuperAdmin"));
//    options.AddPolicy(Policies.LectureSeule, p => p.RequireRole("AdminTenant", "Comptable", "Collaborateur", "Lecture", "Commercial", "SuperAdmin"));
//    options.AddPolicy(Policies.Commercial, p => p.RequireRole("Commercial", "AdminTenant", "SuperAdmin"));
//    options.AddPolicy(Policies.SuperAdmin, p => p.RequireRole("SuperAdmin"));
//});

// ─── Reverse proxy nginx : IP client réelle + schéma HTTPS ────────────────────
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownProxies.Add(IPAddress.Loopback);
    o.KnownProxies.Add(IPAddress.IPv6Loopback);
});

// ─── Limitation de débit (anti brute-force OTP / login / formulaires publics) ───
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.ContentType = "application/json";
        await ctx.HttpContext.Response.WriteAsync(
            "{\"statut\":429,\"message\":\"Trop de tentatives. Patientez une minute puis réessayez.\"}", ct);
    };

    o.AddPolicy(RateLimits.Auth, ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

    o.AddPolicy(RateLimits.Public, ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(10), QueueLimit = 0 }));
});

// ─── CORS : liste blanche explicite ───────────────────────────────────────────
var originesAutorisees = builder.Configuration.GetSection("CorsSettings:AllowedOrigins").Get<string[]>() ?? [];
var estDev = builder.Environment.IsDevelopment();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy => policy
        .SetIsOriginAllowed(origin =>
        {
            if (originesAutorisees.Contains(origin, StringComparer.OrdinalIgnoreCase)) return true;

            if (Uri.TryCreate(origin, UriKind.Absolute, out var uri))
            {
                // Sous-domaines HTTPS de Finama (app., site., www.)
                if (uri.Scheme == Uri.UriSchemeHttps &&
                    (uri.Host == "finamaweb.com" || uri.Host.EndsWith(".finamaweb.com", StringComparison.OrdinalIgnoreCase)))
                    return true;

                if (estDev && uri.IsLoopback) return true;
            }

            // Fichier local (file://) : uniquement en développement
            return estDev && origin == "null";
        })
        .AllowAnyHeader()
        .AllowAnyMethod()
        .WithExposedHeaders("Content-Disposition"));
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Finama API", Version = "v1", Description = "SaaS de comptabilité pour PME africaines" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Entrez votre token JWT ici"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, [] }
    });
});

builder.Services.AddHealthChecks();

var app = builder.Build();

// Première mise en route : génère une paire de clés de notification à copier dans la configuration
if (!app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<PushOptions>>().Value.EstConfigure)
{
    var cles = WebPush.VapidHelper.GenerateVapidKeys();
    app.Logger.LogWarning("Notifications désactivées : clés absentes. À copier dans la configuration :\nPush__VapidPublicKey={Pub}\nPush__VapidPrivateKey={Priv}",
        cles.PublicKey, cles.PrivateKey);
}

// ─── Pipeline HTTP ────────────────────────────────────────────────────────────
app.UseForwardedHeaders();
app.UseExceptionHandling();

if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Finama v1"));
}

if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseMotDePasseObligatoire();// bloque l'API tant que le mot de passe temporaire n'est pas changé
app.UseSupportFinama();
app.UsePermissionsCommerce();
app.UseModulesFinama();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();

await ApplyMigrationsAndSeedAsync(app);

await app.RunAsync();

// ─── Migrations (deux contextes) + seed — échec bloquant hors développement ───
static async Task ApplyMigrationsAndSeedAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        var compta = services.GetRequiredService<AppDbContext>();
        compta.Database.SetCommandTimeout(TimeSpan.FromSeconds(120));
        await compta.Database.MigrateAsync();

        var commerce = services.GetRequiredService<CommerceDbContext>();
        commerce.Database.SetCommandTimeout(TimeSpan.FromSeconds(120));
        await commerce.Database.MigrateAsync();

        await DataSeeder.SeedSuperAdminAsync(compta, app.Configuration);

        logger.LogInformation("[FINAMA INIT] Migrations (Compta + Commerce) et seed terminés.");
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "[FINAMA INIT] Échec des migrations ou du seed.");
        if (!app.Environment.IsDevelopment())
            throw; // Ne jamais servir une API sur un schéma incohérent
    }
}

// ─── Constantes partagées ─────────────────────────────────────────────────────
public static class Policies
{
    public const string AdminTenant = "AdminTenant";
    public const string Comptable = "Comptable";
    public const string Saisie = "Saisie";
    public const string LectureSeule = "LectureSeule";
    public const string Commercial = "Commercial";
    public const string SuperAdmin = "SuperAdminPolicy";
    public const string Utilisateurs = "Utilisateurs";
    public const string Ventes = "Ventes";
    public const string Caisse = "Caisse";
    public const string FinJournee = "FinJournee";
    public const string Stock = "Stock";
}

public static class RateLimits
{
    public const string Auth = "auth";
    public const string Public = "public";
}
