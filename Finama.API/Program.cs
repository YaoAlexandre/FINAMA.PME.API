using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.IdentityModel.Tokens.Jwt;
using Finama.Core.DTOs;
using Finama.Core.Entities;
using Finama.Core.Validators;
using Finama.Infrastructure.Data;
using Finama.Infrastructure.Services;
using Finama.API.Middleware;

// ⚠️ Désactive le remapping automatique des claims JWT par ASP.NET Core.
// Sans ça, "sub" devient NameIdentifier et "role" devient un claim long URI.
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

var builder = WebApplication.CreateBuilder(args);

// 🌟 Force Npgsql à mapper les DateTime locaux en UTC (Règle le problème de format de date)
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// ─── Configuration de l'environnement ─────────────────────────────────────────
// Note : 'allowedOrigins' reste disponible ici si tu en as besoin ailleurs, 
// mais ton CORS utilise désormais l'analyse dynamique SetIsOriginAllowed.
var allowedOrigins = builder.Configuration
    .GetSection("CorsSettings:AllowedOrigins")
    .Get<string[]>() ?? [];

// ─── Base de données ──────────────────────────────────────────────────────────

//builder.Services.AddDbContext<AppDbContext>(options =>
//    options.UseSqlServer(
//        builder.Configuration.GetConnectionString("Default"),
//        sql => sql.MigrationsAssembly("Finama.Infrastructure")
//    ));

builder.Services.AddDbContext<AppDbContext>(options =>
{
    // On récupère la chaîne, et si elle est null, on met une chaîne vide temporaire
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "";

    // 🌟 Si on est sur Render ou qu'on force Postgres
    if (connectionString.Contains("postgres://") || connectionString.Contains("Host="))
    {
        options.UseNpgsql(connectionString,
            sql => sql.MigrationsAssembly("Finama.Infrastructure"));
    }
    // 🏠 Sinon, on utilise SQL Server (Local ou fallback)
    else
    {
        // Si la chaîne est vide (pendant le design-time), on met une chaîne de secours pour éviter le crash
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
            ClockSkew = TimeSpan.Zero,
            //RoleClaimType = "role"
        };
    });

// ─── Autorisation par rôles ───────────────────────────────────────────────────
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminTenant", p =>
        p.RequireClaim("role", nameof(RoleUtilisateur.AdminTenant),
                               nameof(RoleUtilisateur.SuperAdmin)));

    options.AddPolicy("Comptable", p =>
        p.RequireClaim("role", nameof(RoleUtilisateur.AdminTenant),
                               nameof(RoleUtilisateur.Comptable),
                               nameof(RoleUtilisateur.SuperAdmin)));

    options.AddPolicy("SuperAdmin", p =>
        p.RequireClaim("role", nameof(RoleUtilisateur.SuperAdmin)));
});

// ─── CORS Dynamique pour Tunnels ngrok et Dev Local ───────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.SetIsOriginAllowed(origin =>
        {
            // Autorise automatiquement le localhost de dev
            if (origin.StartsWith("https://localhost") || origin.StartsWith("http://localhost"))
                return true;

            // Autorise dynamiquement TOUS les tunnels ngrok (front et back) au vol
            if (origin.EndsWith(".ngrok-free.app") || origin.EndsWith(".ngrok-free.dev"))
                return true;

            // Autorise ton domaine de production Netlify
            if (origin.EndsWith(".netlify.app") || origin.EndsWith(".onrender.com"))
                return true;

            return false; // Bloque le reste du web par sécurité
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
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Finama API",
        Version = "v1",
        Description = "SaaS de comptabilité pour PME africaines"
    });
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
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            []
        }
    });
});

var app = builder.Build();

// ─── Pipeline HTTP (ordre important) ─────────────────────────────────────────
app.UseExceptionHandling(); // en premier — capture tout

// Activé hors du bloc IsDevelopment pour que tu puisses voir ton Swagger à distance via ngrok si besoin !
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Finama v1"));

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthentication(); // JWT d'abord
app.UseAuthorization();  // puis les policies

app.MapControllers();

// ─── Migration automatique au démarrage (dev seulement) ───────────────────────
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

//using (var scope = app.Services.CreateScope())
//{
//    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
//    db.Database.Migrate(); // 🚀 C'est cette ligne qui fait le travail !
//}

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var db = services.GetRequiredService<AppDbContext>();

    try
    {
        Console.WriteLine("[DEPLOIEMENT] Analyse de la structure de la base de données...");

        // 1. On utilise une connexion ADO.NET standard pour lire la réponse (0 ou 1) de Postgres
        using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'Utilisateurs');";

        // On s'assure que la connexion est ouverte
        if (db.Database.GetDbConnection().State != System.Data.ConnectionState.Open)
            await db.Database.GetDbConnection().OpenAsync();

        // On récupère le résultat sous forme de booléen
        bool tableExiste = (bool)(await command.ExecuteScalarAsync() ?? false);

        if (tableExiste)
        {
            // 2. Même logique pour vérifier si la colonne OTP existe
            command.CommandText = "SELECT EXISTS (SELECT FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Utilisateurs' AND column_name = 'otp_code');"; // Attention : Postgres passe souvent les noms en minuscules 'otp_code'
            bool colonneOtpExiste = (bool)(await command.ExecuteScalarAsync() ?? false);

            // Cas A : La table existe mais PAS la colonne OTP -> Ancienne base détectée
            if (!colonneOtpExiste)
            {
                Console.WriteLine("[DEPLOIEMENT] Ancienne structure détectée. Alignement de l'historique EF Core...");

                command.CommandText = "CREATE TABLE IF NOT EXISTS \"__EFMigrationsHistory\" (\"MigrationId\" varchar(150) NOT NULL CONSTRAINT \"PK___EFMigrationsHistory\" PRIMARY KEY, \"ProductVersion\" varchar(32) NOT NULL);";
                await command.ExecuteNonQueryAsync();

                // Remplace '20260517232310_Inot' par le nom exact de ton fichier de migration initiale
                command.CommandText = "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ('20260517232310_Inot', '8.0.0') ON CONFLICT DO NOTHING;";
                await command.ExecuteNonQueryAsync();
            }
            else
            {
                Console.WriteLine("[DEPLOIEMENT] Structure à jour détectée.");
            }
        }

        // 3. On ferme proprement la connexion manuelle avant de laisser EF Core migrer
        await db.Database.GetDbConnection().CloseAsync();

        // 4. On applique les migrations restantes (comme l'ajout des colonnes OTP)
        await db.Database.MigrateAsync();
        Console.WriteLine("[DEPLOIEMENT] Base de données synchronisée avec succès !");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[DEPLOIEMENT CRASH] Erreur critique : {ex.Message}");
    }
}

app.Run();