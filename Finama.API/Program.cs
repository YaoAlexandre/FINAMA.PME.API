using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Finama.Core.DTOs;
using Finama.Core.Entities;
using Finama.Core.Validators;
using Finama.Infrastructure.Data;
using Finama.Infrastructure.Services;
using Finama.API.Middleware;

var builder = WebApplication.CreateBuilder(args);

// ─── Base de données ──────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("Default"),
        sql => sql.MigrationsAssembly("Finama.Infrastructure")
    ));

// ─── Multi-tenant ─────────────────────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, TenantContext>();

// ─── Services métier ──────────────────────────────────────────────────────────
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IEcritureService, EcritureService>();

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

// ─── CORS (à ajuster selon ton domaine frontend) ──────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy => policy
        .WithOrigins(
            "http://localhost:5173",   // dev Vite/React
            "http://localhost:5000",   // dev Blazor
            "https://finama.app"       // production
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Finama v1"));
}

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

app.Run();
