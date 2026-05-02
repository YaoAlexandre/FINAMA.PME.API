using Finama.Core.Entities;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Finama.Infrastructure.Data;

public class AppDbContext : DbContext
{
    private readonly Guid? _tenantId;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantId = tenantContext.TenantId;
    }

    // ─── DbSets ───────────────────────────────────────────────────────────────
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Utilisateur> Utilisateurs => Set<Utilisateur>();
    public DbSet<CompteComptable> CompteComptables => Set<CompteComptable>();
    public DbSet<ExerciceComptable> Exercices => Set<ExerciceComptable>();
    public DbSet<EcritureComptable> Ecritures => Set<EcritureComptable>();
    public DbSet<LigneEcriture> LignesEcriture => Set<LigneEcriture>();
    public DbSet<Tiers> Tiers => Set<Tiers>();
    public DbSet<Facture> Factures => Set<Facture>();
    public DbSet<LigneFacture> LignesFacture => Set<LigneFacture>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Applique toutes les configurations fluent depuis le même assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // ─── Filtres globaux multi-tenant ─────────────────────────────────────
        // Chaque entité TenantEntity est automatiquement filtrée par TenantId.
        // Les requêtes ne voient JAMAIS les données d'un autre tenant.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(TenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .HasQueryFilter(BuildTenantFilter(entityType.ClrType));
            }
        }

        // ─── Soft delete global ───────────────────────────────────────────────
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .HasQueryFilter(BuildSoftDeleteFilter(entityType.ClrType));
            }
        }

        // 🔥 BLOQUE TOUTES LES CASCADES AUTOMATIQUES
        foreach (var fk in modelBuilder.Model.GetEntityTypes()
            .SelectMany(e => e.GetForeignKeys()))
        {
            fk.DeleteBehavior = DeleteBehavior.NoAction;
        }
    }

    // ─── Mise à jour automatique de UpdatedAt ─────────────────────────────────
    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = DateTime.UtcNow;
        }
        return base.SaveChangesAsync(ct);
    }

    // ─── Helpers pour les filtres dynamiques ──────────────────────────────────
    private LambdaExpression BuildTenantFilter(Type type)
    {
        var param = Expression.Parameter(type, "e");

        var tenantProp = Expression.Property(param, nameof(TenantEntity.TenantId));

        var container = Expression.Constant(this);
        var tenantField = Expression.Field(container, nameof(_tenantId));

        // Convert Guid → Guid?
        var tenantPropNullable = Expression.Convert(tenantProp, typeof(Guid?));

        var eq = Expression.Equal(tenantPropNullable, tenantField);

        return Expression.Lambda(eq, param);
    }

    private System.Linq.Expressions.LambdaExpression BuildSoftDeleteFilter(Type type)
    {
        var param = System.Linq.Expressions.Expression.Parameter(type, "e");
        var prop = System.Linq.Expressions.Expression.Property(param, nameof(BaseEntity.IsDeleted));
        var notDeleted = System.Linq.Expressions.Expression.Equal(prop,
            System.Linq.Expressions.Expression.Constant(false));
        return System.Linq.Expressions.Expression.Lambda(notDeleted, param);
    }
}


