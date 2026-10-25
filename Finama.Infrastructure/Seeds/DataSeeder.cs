using Finama.Core.Entities;
using Finama.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Finama.Infrastructure.Seeds;

public static class DataSeeder
{
    /// <summary>
    /// Crée le SuperAdmin au premier démarrage uniquement.
    /// Variables requises (.env / systemd) : SuperAdmin__Email, SuperAdmin__MotDePasseInitial.
    /// Le mot de passe initial devra être changé à la première connexion.
    /// </summary>
    public static async Task SeedSuperAdminAsync(AppDbContext context, IConfiguration config)
    {
        var existe = await context.Utilisateurs
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Role == RoleUtilisateur.SuperAdmin && !u.IsDeleted);

        if (existe) return;

        var email = config["SuperAdmin:Email"];
        var motDePasse = config["SuperAdmin:MotDePasseInitial"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(motDePasse))
            throw new InvalidOperationException(
                "Aucun SuperAdmin en base : définissez SuperAdmin__Email et SuperAdmin__MotDePasseInitial.");

        if (motDePasse.Length < 12)
            throw new InvalidOperationException("SuperAdmin__MotDePasseInitial doit faire au moins 12 caractères.");

        context.Utilisateurs.Add(new Utilisateur
        {
            Nom = config["SuperAdmin:Nom"] ?? "VOKOU",
            Prenom = config["SuperAdmin:Prenom"] ?? "Yao Alexandre",
            Email = email.Trim().ToLowerInvariant(),
            Role = RoleUtilisateur.SuperAdmin,
            TenantId = null,
            EstActif = true,
            MotDePasseHash = BCrypt.Net.BCrypt.HashPassword(motDePasse),
            DoitChangerMotDePasse = true,
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
    }
}
