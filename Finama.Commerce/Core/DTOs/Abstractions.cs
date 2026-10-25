using Finama.Commerce.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Finama.Commerce.Application.Common;

public interface ICurrentUser
{
    Guid UserId { get; }
    Guid TenantId { get; }                       // lève UnauthorizedAccessException si le jeton ne porte pas de tenant
    bool EstAutorise(string permission);
}

/// <summary>Port vers la base. Choix assumé : pas de couche « repository » au-dessus d'EF Core, qui est déjà un Unit of Work.</summary>
public interface ICommerceDbContext
{
    DbSet<Categorie> Categories { get; }
    DbSet<Editeur> Editeurs { get; }
    DbSet<Produit> Produits { get; }
    DbSet<CodeBarresProduit> CodesBarres { get; }
    DbSet<ProduitLivre> ProduitsLivres { get; }
    DbSet<Magasin> Magasins { get; }
    DbSet<StockProduit> StocksProduits { get; }
    DbSet<LotStock> Lots { get; }
    DbSet<MouvementStock> Mouvements { get; }
    DbSet<Inventaire> Inventaires { get; }
    DbSet<InventaireLigne> InventaireLignes { get; }
    DbSet<Caisse> Caisses { get; }
    DbSet<SessionCaisse> Sessions { get; }
    DbSet<ClotureCaisse> Clotures { get; }
    DbSet<Vente> Ventes { get; }
    DbSet<VenteLigne> VenteLignes { get; }
    DbSet<Paiement> Paiements { get; }
    DbSet<SuperviseurPin> SuperviseurPins { get; }
    DbSet<ParametresCommerce> Parametres { get; }
    DbSet<ImageProduit> ImagesProduits { get; }
    DatabaseFacade Database { get; }
    ChangeTracker ChangeTracker { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    DbSet<Client> Clients { get; }
    DbSet<MouvementFidelite> MouvementsFidelite { get; }
}

/// <summary>Journal d'audit sécurité. Module 6 : implémentation append-only sur une connexion distincte (survit aux rollbacks).</summary>
public interface IAuditTrail
{
    Task EcrireAsync(string action, string entite, Guid? entiteId, object? details = null, bool sensible = false, CancellationToken ct = default);
}

public interface ISequenceService { Task<long> ProchainAsync(string cle, int annee, CancellationToken ct = default); }
public interface IPinHasher { string Hash(string pin); bool Verify(string pin, string hash); }

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalItems / (double)PageSize);
}

public class NotFoundException(string message) : Exception(message);
public class BusinessRuleException(string message) : Exception(message);

public static class Permissions
{
    public const string ProduitsLire = "produits.lire";
    public const string ProduitsEcrire = "produits.ecrire";
    public const string StockLire = "stock.lire";
    public const string StockAjuster = "stock.ajuster";
    public const string InventaireCompter = "inventaire.compter";
    public const string InventaireGerer = "inventaire.gerer";
    public const string CaisseVendre = "caisse.vendre";
    public const string CaisseAnnuler = "caisse.annuler";
    public const string CaisseCloturer = "caisse.cloturer";
    public const string CaisseRapportZ = "caisse.rapportz";
    public const string SecuritePins = "securite.pins";

    public static readonly string[] All =
    [
        ProduitsLire, ProduitsEcrire, StockLire, StockAjuster, InventaireCompter, InventaireGerer,
        CaisseVendre, CaisseAnnuler, CaisseCloturer, CaisseRapportZ, SecuritePins
    ];
}
