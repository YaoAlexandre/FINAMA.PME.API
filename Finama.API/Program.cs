using Finama.API.Middleware;
using Finama.Core.DTOs;
using Finama.Core.Entities;
using Finama.Core.Validators;
using Finama.Infrastructure.Data;
using Finama.Infrastructure.Services;
using Finama.Infrastructure.Services.Commercials;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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

// 1. Détection et chargement d'un fichier .env local s'il existe
var envFilePath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (File.Exists(envFilePath))
{
    Console.WriteLine("[FINAMA CONFIG] Fichier .env détecté et injecté localement.");
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
    .AddEnvironmentVariables(); // Permet à IConfiguration de lire directement le .env ou le système

Console.WriteLine($"[FINAMA] Démarrage de l'arborescence en mode : {env}");

// 🌟 Force Npgsql à mapper les DateTime locaux en UTC (Règle le problème de format de date)
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// ─── Base de données ──────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
{
    // Récupération unifiée depuis le provider (.env ou appsettings ou Render)
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                           ?? builder.Configuration["ConnectionStrings__DefaultConnection"]
                           ?? "";

    // 🌟 Si on est sur Render, Docker ou qu'on force Postgres via .env
    if (connectionString.Contains("postgres://") || connectionString.Contains("Host="))
    {
        options.UseNpgsql(connectionString,
            sql => sql.MigrationsAssembly("Finama.Infrastructure"));
    }
    // 🏠 Sinon, on utilise SQL Server (Local ou fallback)
    else
    {
        var sqlServerPath = string.IsNullOrEmpty(connectionString)
            ? "Server=localhost;Database=Dummy;Trusted_Connection=True;"
            : connectionString;

        options.UseSqlServer(sqlServerPath,
            sql => sql.MigrationsAssembly("Finama.Infrastructure"));
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
        policy.SetIsOriginAllowed(origin =>
        {
            if (origin.StartsWith("https://localhost") || origin.StartsWith("http://localhost"))
                return true;

            if (origin.EndsWith(".ngrok-free.app") || origin.EndsWith(".ngrok-free.dev"))
                return true;

            if (origin.EndsWith(".netlify.app") || origin.EndsWith(".onrender.com"))
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

// ─── Migration et Synchronisation automatique au démarrage ───────────────────
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var db = services.GetRequiredService<AppDbContext>();

    try
    {
        Console.WriteLine("[DEPLOIEMENT] Analyse de la structure de la base de données...");

        using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'Utilisateurs');";

        if (db.Database.GetDbConnection().State != System.Data.ConnectionState.Open)
            await db.Database.GetDbConnection().OpenAsync();

        bool tableExiste = (bool)(await command.ExecuteScalarAsync() ?? false);

        if (tableExiste)
        {
            command.CommandText = "SELECT EXISTS (SELECT FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Utilisateurs' AND column_name = 'otp_code');";
            bool colonneOtpExiste = (bool)(await command.ExecuteScalarAsync() ?? false);

            if (!colonneOtpExiste)
            {
                Console.WriteLine("[DEPLOIEMENT] Ancienne structure détectée. Alignement de l'historique EF Core...");

                command.CommandText = "CREATE TABLE IF NOT EXISTS \"__EFMigrationsHistory\" (\"MigrationId\" varchar(150) NOT NULL CONSTRAINT \"PK___EFMigrationsHistory\" PRIMARY KEY, \"ProductVersion\" varchar(32) NOT NULL);";
                await command.ExecuteNonQueryAsync();

                command.CommandText = "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ('20260517232310_Inot', '8.0.0') ON CONFLICT DO NOTHING;";
                await command.ExecuteNonQueryAsync();
            }
            else
            {
                Console.WriteLine("[DEPLOIEMENT] Structure à jour détectée.");
            }
        }

        await db.Database.GetDbConnection().CloseAsync();

        await db.Database.MigrateAsync();
        Console.WriteLine("[DEPLOIEMENT] Base de données synchronisée avec succès !");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[DEPLOIEMENT CRASH] Erreur critique : {ex.Message}");
    }
}

app.Run();