using Finama.Core.Entities;
using Finama.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Finama.Infrastructure.Seeds;

public static class DataSeeder
{
    public static async Task SeedSuperAdminAsync(AppDbContext context, IPasswordHasher<Utilisateur>? passwordHasher = null)
    {
        // 1. Vérifier si un SuperAdmin existe déjà
        var superAdminExiste = await context.Set<Utilisateur>()
            .AnyAsync(u => u.Role == RoleUtilisateur.SuperAdmin);

        if (superAdminExiste)
        {
            return;
        }

        // 2. Instancier le compte SuperAdmin
        var superAdmin = new Utilisateur
        {
            Nom = "VOKOU",
            Prenom = "Yao Alexandre",
            Email = "vokouyaoalexandre@gmail.com", // Votre adresse e-mail réelle pour recevoir les messages
            Role = RoleUtilisateur.SuperAdmin,
            TenantId = null,
            EstActif = true,
            CreatedAt = DateTime.UtcNow
        };

        // 3. Hachage du mot de passe avec BCrypt
        if (passwordHasher != null)
        {
            superAdmin.MotDePasseHash = passwordHasher.HashPassword(superAdmin, "SuperAdmin2026!");
        }
        else
        {
            superAdmin.MotDePasseHash = BCrypt.Net.BCrypt.HashPassword("SuperAdmin2026!");
        }

        context.Set<Utilisateur>().Add(superAdmin);
        await context.SaveChangesAsync();
    }
}