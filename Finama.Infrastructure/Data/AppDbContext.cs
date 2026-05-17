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
    public DbSet<PaysConfig> Pays => Set<PaysConfig>();        // ← ajouté
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Utilisateur> Utilisateurs => Set<Utilisateur>();
    public DbSet<CompteComptable> CompteComptables => Set<CompteComptable>();
    public DbSet<ExerciceComptable> Exercices => Set<ExerciceComptable>();
    public DbSet<EcritureComptable> Ecritures => Set<EcritureComptable>();
    public DbSet<LigneEcriture> LignesEcriture => Set<LigneEcriture>();
    public DbSet<Tiers> Tiers => Set<Tiers>();
    public DbSet<Facture> Factures => Set<Facture>();
    public DbSet<LigneFacture> LignesFacture => Set<LigneFacture>();

    public DbSet<ClasseComptable> ClassesComptables { get; set; }
    public DbSet<Devise> Devises { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;

            var isTenantEntity = typeof(TenantEntity).IsAssignableFrom(clrType);
            var isBaseEntity = typeof(BaseEntity).IsAssignableFrom(clrType);

            if (isTenantEntity)
            {
                // Entités tenant : filtre multi-tenant ET soft delete combinés
                modelBuilder.Entity(clrType)
                    .HasQueryFilter(BuildTenantAndSoftDeleteFilter(clrType));
            }
            else if (isBaseEntity)
            {
                // Entités globales (ex: PaysConfig) : soft delete uniquement
                modelBuilder.Entity(clrType)
                    .HasQueryFilter(BuildSoftDeleteFilter(clrType));
            }
        }

        // Bloque toutes les cascades automatiques
        foreach (var fk in modelBuilder.Model.GetEntityTypes()
            .SelectMany(e => e.GetForeignKeys()))
        {
            fk.DeleteBehavior = DeleteBehavior.NoAction;
        }
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        // 🌟 Le bon endroit pour ignorer l'avertissement de freeze du modèle
        optionsBuilder.ConfigureWarnings(warnings =>
            warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Intercepter les lignes d'écriture impactées par une modification (CUD)
        var lignesModifiees = ChangeTracker.Entries<LigneEcriture>()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted)
            .ToList();

        if (lignesModifiees.Any())
        {
            // Récupérer la liste des IDs des écritures parentes
            var ecritureIds = lignesModifiees
                .Select(e => e.State == EntityState.Deleted ? (Guid)e.OriginalValues["EcritureId"] : e.Entity.EcritureId)
                .Distinct()
                .ToList();

            // Vérifier si au moins une de ces écritures est rattachée à un exercice clos
            var impacteUnExerciceClos = await Ecritures
                .Where(e => ecritureIds.Contains(e.Id))
                .AnyAsync(e => e.Exercice.EstCloture, cancellationToken);

            if (impacteUnExerciceClos)
            {
                throw new InvalidOperationException("Action interdite : Les écritures comptables d'un exercice clôturé sont verrouillées et immuables.");
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }

    //public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    //{
    //    foreach (var entry in ChangeTracker.Entries<BaseEntity>())
    //    {
    //        if (entry.State == EntityState.Modified)
    //            entry.Entity.UpdatedAt = DateTime.UtcNow;
    //    }
    //    return base.SaveChangesAsync(ct);
    //}

    // ─── Filtre combiné : TenantId + IsDeleted (pour TenantEntity) ───────────
    private LambdaExpression BuildTenantAndSoftDeleteFilter(Type type)
    {
        var param = Expression.Parameter(type, "e");

        // e.TenantId == _tenantId
        var tenantProp = Expression.Property(param, nameof(TenantEntity.TenantId));
        var tenantPropNull = Expression.Convert(tenantProp, typeof(Guid?));
        var tenantVal = Expression.Field(Expression.Constant(this), nameof(_tenantId));
        var tenantEq = Expression.Equal(tenantPropNull, tenantVal);

        // e.IsDeleted == false
        var deletedProp = Expression.Property(param, nameof(BaseEntity.IsDeleted));
        var deletedEq = Expression.Equal(deletedProp, Expression.Constant(false));

        // Combiné : TenantId == _tenantId && IsDeleted == false
        var combined = Expression.AndAlso(tenantEq, deletedEq);

        return Expression.Lambda(combined, param);
    }

    // ─── Filtre soft delete seul (pour BaseEntity non-tenant) ─────────────────
    private static LambdaExpression BuildSoftDeleteFilter(Type type)
    {
        var param = Expression.Parameter(type, "e");
        var prop = Expression.Property(param, nameof(BaseEntity.IsDeleted));
        var notDeleted = Expression.Equal(prop, Expression.Constant(false));
        return Expression.Lambda(notDeleted, param);
    }
}