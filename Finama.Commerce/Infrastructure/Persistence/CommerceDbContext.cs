using System.Reflection;
using Finama.Commerce.Application.Common;
using Finama.Commerce.Core.Domain;
using Finama.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Finama.Commerce.Infrastructure.Persistence;

public class CommerceDbContext : DbContext, ICommerceDbContext
{
    private readonly ICurrentUser? _currentUser;

    // ICurrentUser est optionnel : null au design-time (migrations). Si tu utilises AddDbContextPool,
    // passe en AddDbContext (un service scoped ne peut pas être injecté dans un contexte poolé).
    public CommerceDbContext(DbContextOptions<CommerceDbContext> options, ICurrentUser? currentUser = null) : base(options)
    {
        _currentUser = currentUser;
    }

    // Relu à chaque requête par le filtre global (EF lit la propriété de l'instance courante du contexte).
    /// <summary>
    /// Entreprise du jeton, ou Guid.Empty si le jeton n'en porte pas (SuperAdmin, tâche système, design-time).
    /// Guid.Empty ne correspond à aucune donnée : le SuperAdmin ne voit rien du commerce, sans erreur.
    /// </summary>
    private Guid TenantCourant
    {
        get
        {
            if (_currentUser is null) return Guid.Empty;
            try { return _currentUser.TenantId; }
            catch (UnauthorizedAccessException) { return Guid.Empty; }
        }
    }

    public DbSet<Categorie> Categories => Set<Categorie>();
    public DbSet<Editeur> Editeurs => Set<Editeur>();
    public DbSet<Produit> Produits => Set<Produit>();
    public DbSet<CodeBarresProduit> CodesBarres => Set<CodeBarresProduit>();
    public DbSet<ProduitLivre> ProduitsLivres => Set<ProduitLivre>();
    public DbSet<Magasin> Magasins => Set<Magasin>();
    public DbSet<StockProduit> StocksProduits => Set<StockProduit>();
    public DbSet<LotStock> Lots => Set<LotStock>();
    public DbSet<MouvementStock> Mouvements => Set<MouvementStock>();
    public DbSet<Inventaire> Inventaires => Set<Inventaire>();
    public DbSet<InventaireLigne> InventaireLignes => Set<InventaireLigne>();
    public DbSet<Caisse> Caisses => Set<Caisse>();
    public DbSet<SessionCaisse> Sessions => Set<SessionCaisse>();
    public DbSet<ClotureCaisse> Clotures => Set<ClotureCaisse>();
    public DbSet<Vente> Ventes => Set<Vente>();
    public DbSet<VenteLigne> VenteLignes => Set<VenteLigne>();
    public DbSet<Paiement> Paiements => Set<Paiement>();
    public DbSet<SuperviseurPin> SuperviseurPins => Set<SuperviseurPin>();
    public DbSet<ParametresCommerce> Parametres => Set<ParametresCommerce>();
    public DbSet<ImageProduit> ImagesProduits => Set<ImageProduit>();
    public DbSet<SequenceDocument> Sequences => Set<SequenceDocument>();

    // ── Clientèle ──
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<MouvementFidelite> MouvementsFidelite => Set<MouvementFidelite>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Entités de la comptabilité : gérées par AppDbContext, jamais par le module Commerce
        modelBuilder.Ignore<Tenant>();
        modelBuilder.Ignore<Utilisateur>();
        modelBuilder.Ignore<PaysConfig>();
        modelBuilder.Ignore<Devise>();
        modelBuilder.Ignore<DemandeAcces>();
        modelBuilder.Ignore<AppareilConfiance>();
        modelBuilder.Ignore<ClasseComptable>();
        modelBuilder.Ignore<CompteComptable>();
        modelBuilder.Ignore<ExerciceComptable>();
        modelBuilder.Ignore<EcritureComptable>();
        modelBuilder.Ignore<LigneEcriture>();
        modelBuilder.Ignore<Tiers>();
        modelBuilder.Ignore<Facture>();
        modelBuilder.Ignore<LigneFacture>();
        modelBuilder.Ignore<Devis>();
        modelBuilder.Ignore<LigneDevis>();
        modelBuilder.Entity<ImageProduit>(b =>
        {
            b.ToTable("ImagesProduits");
            b.Property(x => x.TypeMime).HasMaxLength(30).IsRequired();
            b.HasIndex(x => x.ProduitId).IsUnique();
            b.HasIndex(x => x.JetonPublic).IsUnique();
        });

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CommerceDbContext).Assembly);

        // 🔒 Isolation multi-tenant : toute requête est automatiquement restreinte au tenant du jeton.
        // Deux familles d'entités : TenantEntity (TenantId nullable, la quasi-totalité du module)
        // et ITenantEntity (TenantId non nullable : ProduitLivre, SequenceDocument).
        // ⚠️ Avant ce correctif, seules les ITenantEntity étaient filtrées : produits, ventes, clients, stocks…
        //    de TOUTES les entreprises étaient visibles par chacune.
        var filtreNullable = typeof(CommerceDbContext).GetMethod(nameof(AppliquerFiltreTenantEntity), BindingFlags.NonPublic | BindingFlags.Instance)!;
        var filtreStrict = typeof(CommerceDbContext).GetMethod(nameof(AppliquerFiltreTenant), BindingFlags.NonPublic | BindingFlags.Instance)!;

        foreach (var entite in modelBuilder.Model.GetEntityTypes().Where(e => e.BaseType == null && !e.IsOwned()).ToList())
        {
            var type = entite.ClrType;
            if (typeof(TenantEntity).IsAssignableFrom(type))
                filtreNullable.MakeGenericMethod(type).Invoke(this, new object[] { modelBuilder });
            else if (typeof(ITenantEntity).IsAssignableFrom(type))
                filtreStrict.MakeGenericMethod(type).Invoke(this, new object[] { modelBuilder });
        }
    }

    private void AppliquerFiltreTenantEntity<T>(ModelBuilder modelBuilder) where T : TenantEntity
        => modelBuilder.Entity<T>().HasQueryFilter(e => e.TenantId == TenantCourant);

    private void AppliquerFiltreTenant<T>(ModelBuilder modelBuilder) where T : class, ITenantEntity
        => modelBuilder.Entity<T>().HasQueryFilter(e => e.TenantId == TenantCourant);

    // 🔒 Tout nouvel enregistrement reçoit le tenant du jeton : TenantId null ou vide → tenant courant.
    //    Un enregistrement d'un AUTRE tenant est refusé (écriture croisée impossible).
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        AppliquerTenant();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        AppliquerTenant();
        return base.SaveChanges();
    }

    private void AppliquerTenant()
    {
        var tenant = TenantCourant;
        if (tenant == Guid.Empty) return;   // design-time / tâche système : rien à imposer

        foreach (var entree in ChangeTracker.Entries())
        {
            if (entree.State is not (EntityState.Added or EntityState.Modified)) continue;

            switch (entree.Entity)
            {
                case TenantEntity e:
                    if (entree.State == EntityState.Added && (e.TenantId is null || e.TenantId == Guid.Empty))
                        e.TenantId = tenant;
                    else if (e.TenantId != tenant)
                        throw new UnauthorizedAccessException("Écriture refusée : donnée d'une autre entreprise.");
                    break;

                case ITenantEntity e:
                    if (entree.State == EntityState.Added && e.TenantId == Guid.Empty)
                        e.TenantId = tenant;
                    else if (e.TenantId != tenant)
                        throw new UnauthorizedAccessException("Écriture refusée : donnée d'une autre entreprise.");
                    break;
            }
        }
    }
}