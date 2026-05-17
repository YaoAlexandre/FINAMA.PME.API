using Finama.Core.Entities;
using Finama.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Finama.Infrastructure.Services;

public interface ITenantInitializationService
{
    Task InitialiserPlanComptableParDefautAsync(Guid tenantId);
}

public class TenantInitializationService : ITenantInitializationService
{
    private readonly AppDbContext _db;

    public TenantInitializationService(AppDbContext db)
    {
        _db = db;
    }

    public async Task InitialiserPlanComptableParDefautAsync(Guid tenantId)
    {
        // 1. Vérifier si le tenant a déjà des classes pour éviter les doublons
        var existeDeja = await _db.ClassesComptables.AnyAsync(c => c.TenantId == tenantId);
        if (existeDeja) return;

        // 2. Définir la liste standard OHADA pour ce nouveau Tenant
        var classesParDefaut = new List<ClasseComptable>
        {
            new() { Numero = 1, Libelle = "Capitaux", TenantId = tenantId },
            new() { Numero = 2, Libelle = "Immobilisations", TenantId = tenantId },
            new() { Numero = 3, Libelle = "Stocks", TenantId = tenantId },
            new() { Numero = 4, Libelle = "Tiers", TenantId = tenantId },
            new() { Numero = 5, Libelle = "Trésorerie", TenantId = tenantId },
            new() { Numero = 6, Libelle = "Charges", TenantId = tenantId },
            new() { Numero = 7, Libelle = "Produits", TenantId = tenantId }
        };

        // 3. Sauvegarder en base de données
        await _db.ClassesComptables.AddRangeAsync(classesParDefaut);
        await _db.SaveChangesAsync();
    }
}