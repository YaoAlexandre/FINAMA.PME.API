using Finama.Commerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace Finama.Commerce.Infrastructure.Persistence;

public class CommerceDbContextFactory : IDesignTimeDbContextFactory<CommerceDbContext>
{
    public CommerceDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();

        // 1. Lecture manuelle et basique du fichier .env s'il existe (pour charger les variables d'env dans le process)
        var envFilePath = Path.Combine(basePath, "../.env");
        if (!File.Exists(envFilePath)) envFilePath = Path.Combine(basePath, ".env");

        if (File.Exists(envFilePath))
        {
            foreach (var line in File.ReadAllLines(envFilePath))
            {
                var parts = line.Split('=', 2);
                if (parts.Length == 2)
                {
                    var key = parts[0].Trim();
                    var value = parts[1].Trim().Trim('"', '\'');
                    if (!string.IsNullOrEmpty(key) && Environment.GetEnvironmentVariable(key) == null)
                    {
                        Environment.SetEnvironmentVariable(key, value);
                    }
                }
            }
        }

        // 2. Construction de la configuration
        var apiPath = Path.Combine(basePath, "../Finama.API");
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.Exists(apiPath) ? apiPath : basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        // 3. Récupération de la chaîne de connexion
        var connectionString = configuration.GetConnectionString("DefaultConnection")
                               ?? configuration["ConnectionStrings__DefaultConnection"]
                               ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                               ?? Environment.GetEnvironmentVariable("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = "Server=localhost;Database=FinamaDb;Trusted_Connection=True;TrustServerCertificate=True;";
        }

        var builder = new DbContextOptionsBuilder<CommerceDbContext>();
        builder.UseSqlServer(connectionString);

        return new CommerceDbContext(builder.Options);
    }
}