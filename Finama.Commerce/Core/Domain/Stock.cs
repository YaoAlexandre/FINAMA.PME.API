using Finama.Core.Entities;

namespace Finama.Commerce.Core.Domain;

/// <summary>Quantité courante d'un produit dans un magasin. RowVersion : deux caisses ne peuvent pas écraser le même solde.</summary>
public class StockProduit : TenantEntity
{
    public Guid MagasinId { get; set; }
    public Guid ProduitId { get; set; }
    public decimal Quantite { get; set; }
    public DateTime? DateDernierMouvement { get; set; }

    /// <summary>Concurrence optimiste : deux caisses ne peuvent plus écraser le même solde (retry dans Transactions.ExecuterAsync).</summary>
    [System.ComponentModel.DataAnnotations.Timestamp]
    public byte[] RowVersion { get; set; } = [];
}

public enum StatutLot { Actif = 0, Epuise = 1, Perime = 2, Retire = 3 }

public class LotStock : TenantEntity
{
    public Guid MagasinId { get; set; }
    public Guid ProduitId { get; set; }
    public string NumeroLot { get; set; } = "";
    public DateOnly? DatePeremption { get; set; }
    public DateOnly DateReception { get; set; }
    public decimal QuantiteInitiale { get; set; }
    public decimal QuantiteRestante { get; set; }
    public decimal CoutUnitaire { get; set; }
    public StatutLot Statut { get; set; } = StatutLot.Actif;

    [System.ComponentModel.DataAnnotations.Timestamp]
    public byte[] RowVersion { get; set; } = [];
}

public enum TypeMouvement
{
    EntreeInitiale = 1, EntreeAchat = 2, EntreeRetourClient = 3, EntreeAjustement = 4, EntreeTransfert = 5,
    SortieVente = 10, SortieRetourFournisseur = 11, SortieCasse = 12, SortiePeremption = 13,
    SortieAjustement = 14, SortieTransfert = 15, SortiePilonnage = 16
}

/// <summary>Journal des mouvements : insertion seule (jamais modifié ni supprimé). Quantité signée : + entrée, − sortie.</summary>
public class MouvementStock : TenantEntity
{
    public Guid MagasinId { get; set; }
    public Guid ProduitId { get; set; }
    public Guid? LotId { get; set; }
    public TypeMouvement Type { get; set; }
    public decimal Quantite { get; set; }
    public decimal StockApres { get; set; }
    public decimal? CoutUnitaire { get; set; }
    public string? DocumentType { get; set; }            // VENTE, ACHAT, INVENTAIRE, AJUSTEMENT...
    public Guid? DocumentId { get; set; }
    public string? Motif { get; set; }
    public DateTime DateMouvement { get; set; }
    public Guid? UtilisateurId { get; set; }
}

public enum StatutInventaire { Ouvert = 0, Cloture = 1, Annule = 2 }

public class Inventaire : TenantEntity
{
    public Guid MagasinId { get; set; }
    public string Numero { get; set; } = "";
    public string Libelle { get; set; } = "";
    public StatutInventaire Statut { get; set; } = StatutInventaire.Ouvert;
    public DateTime DateOuverture { get; set; }
    public DateTime? DateCloture { get; set; }
    public Guid? ClotureParId { get; set; }
    public List<InventaireLigne> Lignes { get; set; } = [];
}

public class InventaireLigne : TenantEntity
{
    public Guid InventaireId { get; set; }
    public Guid ProduitId { get; set; }
    public Guid? LotId { get; set; }
    public decimal QuantiteTheorique { get; set; }       // figée à l'ouverture, masquée au compteur (comptage à l'aveugle)
    public decimal? QuantiteComptee { get; set; }
    public decimal? Ecart => QuantiteComptee - QuantiteTheorique;
}

public class StockLotDto
{
    public Guid Id { get; set; } // 🌟 Ajout de l'ID unique du lot
    public Guid ProduitId { get; set; }
    public string DesignationProduit { get; set; } = "";
    public string NumeroLot { get; set; } = "";
    public DateTime? DateExpiration { get; set; }
    public decimal QuantiteDisponible { get; set; }
}