using Finama.API.Middleware;
using Finama.Core.DTOs;
using Finama.Core.Entities;
using Finama.Core.Validators;
using Finama.Infrastructure.Data;
using Finama.Infrastructure.Seeds;
using Finama.Infrastructure.Services;
using Finama.Infrastructure.Services.Commercials;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Text;

// ⚠️ Désactive le remapping automatique des claims JWT par ASP.NET Core.
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

var builder = WebApplication.CreateBuilder(args);

// ==============================================================================
// 🔌 CHARGEMENT DYNAMIQUE DU FICHIER .ENV & CONFIGURATION
// ==============================================================================

// 1. Détection et chargement d'un fichier .env (support du dossier parent et du dossier binaire)
var baseDirectory = Directory.GetCurrentDirectory();
var envFilePath = Path.Combine(baseDirectory, ".env");

if (!File.Exists(envFilePath))
{
    // Remonte d'un niveau (racine de la solution) si exécuté depuis /Finama.API
    var parentDir = Directory.GetParent(baseDirectory)?.FullName;
    if (parentDir != null)
    {
        envFilePath = Path.Combine(parentDir, ".env");
    }
}

// Fallback pour les binaires publiés
if (!File.Exists(envFilePath))
{
    envFilePath = Path.Combine(AppContext.BaseDirectory, ".env");
}

if (File.Exists(envFilePath))
{
    Console.WriteLine($"[FINAMA CONFIG] Fichier .env détecté et injecté depuis : {envFilePath}");
    DotNetEnv.Env.Load(envFilePath);
}
else
{
    Console.WriteLine("[FINAMA CONFIG] Aucun fichier .env trouvé. Utilisation des variables système globales.");
}

// 2. Détermination de l'environnement actif (priorité au .env / variables système, sinon Production)
string env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
builder.Environment.EnvironmentName = env;

// 3. Construction de la hiérarchie de configuration unifiée
builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables(); // Permet à IConfiguration de lire le .env injecté et le système

Console.WriteLine($"[FINAMA] Démarrage de l'arborescence en mode : {env}");

// ─── Base de données ──────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                           ?? builder.Configuration["ConnectionStrings__DefaultConnection"]
                           ?? "";

    // Détection explicite de PostgreSQL (Render / Docker / .env)
    bool isPostgres = connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
                   || connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
                   || connectionString.Contains("Port=5432");

    if (isPostgres)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        options.UseNpgsql(connectionString, sql => sql.MigrationsAssembly("Finama.Infrastructure"));
    }
    else
    {
        var sqlServerPath = string.IsNullOrWhiteSpace(connectionString)
            ? "Server=localhost;Database=FinamaDb;Trusted_Connection=True;TrustServerCertificate=True;"
            : connectionString;

        options.UseSqlServer(sqlServerPath, sql => sql.MigrationsAssembly("Finama.Infrastructure"));
    }
});

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
builder.Services.AddScoped<ISuperAdminService, SuperAdminService>();

// ─── Validation FluentValidation ──────────────────────────────────────────────
builder.Services.AddScoped<IValidator<CreerEcritureRequest>, CreerEcritureValidator>();

// ─── Authentification JWT ─────────────────────────────────────────────────────
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()!;
var cle = Encoding.UTF8.GetBytes(jwtSettings.SecretKey);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(cle),
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

// ─── Autorisation par rôles ───────────────────────────────────────────────────
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminTenant", p =>
        p.RequireClaim("role", nameof(RoleUtilisateur.AdminTenant), nameof(RoleUtilisateur.SuperAdmin)));

    options.AddPolicy("Comptable", p =>
        p.RequireClaim("role", nameof(RoleUtilisateur.AdminTenant), nameof(RoleUtilisateur.Comptable), nameof(RoleUtilisateur.SuperAdmin)));

    options.AddPolicy("Saisie", p =>
        p.RequireClaim("role", nameof(RoleUtilisateur.AdminTenant), nameof(RoleUtilisateur.Comptable), nameof(RoleUtilisateur.Collaborateur), nameof(RoleUtilisateur.SuperAdmin)));

    options.AddPolicy("LectureSeule", p =>
        p.RequireClaim("role", nameof(RoleUtilisateur.AdminTenant), nameof(RoleUtilisateur.Comptable), nameof(RoleUtilisateur.Collaborateur), nameof(RoleUtilisateur.Lecture), nameof(RoleUtilisateur.Commercial), nameof(RoleUtilisateur.SuperAdmin)));

    options.AddPolicy("Commercial", p =>
        p.RequireClaim("role", nameof(RoleUtilisateur.Commercial), nameof(RoleUtilisateur.AdminTenant), nameof(RoleUtilisateur.SuperAdmin)));
});

// ─── CORS Dynamique pour Tunnels ngrok et Dev Local ───────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        // Dans builder.Services.AddCors(...)
        policy.SetIsOriginAllowed(origin =>
        {
            // Autorise les fichiers locaux ouvert via file://
            if (string.Equals(origin, "null", StringComparison.OrdinalIgnoreCase))
                return true;

            var allowedOrigins = builder.Configuration.GetSection("CorsSettings:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
            if (allowedOrigins.Any(o => o.Equals(origin, StringComparison.OrdinalIgnoreCase)))
                return true;

            if (origin.StartsWith("https://localhost") || origin.StartsWith("http://localhost"))
                return true;

            if (origin.EndsWith(".ngrok-free.app") || origin.EndsWith(".ngrok-free.dev") ||
                origin.EndsWith(".netlify.app") || origin.EndsWith(".onrender.com") || origin.EndsWith(".finamaweb.com"))
                return true;

            return false;
        })
                .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// ─── Swagger avec support JWT ─────────────────────────────────────────────────
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
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, [] } });
});

builder.Services.AddHealthChecks();

// ✅ CODE CORRIGÉ :
var app = builder.Build();

app.MapHealthChecks("/health");

// ─── Pipeline HTTP ───────────────────────────────────────────────────────────
app.UseExceptionHandling();
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Finama v1"));

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ─── Migration et Seeding sécurisés ──────────────────────────────────────────
await ApplyMigrationsAndSeedAsync(app);

await app.RunAsync();

// Méthode locale isolée
static async Task ApplyMigrationsAndSeedAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        var context = services.GetRequiredService<AppDbContext>();

        logger.LogInformation("[FINAMA INIT] Application des migrations EF Core...");

        // Timeout explicite pour les migrations pour éviter le blocage indéfini
        context.Database.SetCommandTimeout(TimeSpan.FromSeconds(30));
        await context.Database.MigrateAsync();

        var passwordHasher = services.GetService<IPasswordHasher<Utilisateur>>();
        await DataSeeder.SeedSuperAdminAsync(context, passwordHasher);

        logger.LogInformation("[FINAMA INIT] Migrations et seeding terminés avec succès.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "[FINAMA INIT ERROR] Une erreur est survenue lors de la migration ou du seeding.");
    }
}

app.Run();