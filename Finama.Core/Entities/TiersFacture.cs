namespace Finama.Core.Entities;

// ─── Tiers (clients & fournisseurs) ──────────────────────────────────────────

public class Tiers : TenantEntity
{
    public string Code { get; set; } = string.Empty;       // ex: "CLI-001"
    public string Nom { get; set; } = string.Empty;
    public TypeTiers Type { get; set; }
    public string? NINEA { get; set; }
    public string? Adresse { get; set; }
    public string? Telephone { get; set; }
    public string? Email { get; set; }
    public string? Devise { get; set; }                    // devise préférée du tiers
    public Guid? CompteComptableId { get; set; }           // compte 411xxx ou 401xxx
    public CompteComptable? CompteComptable { get; set; }
    public bool EstActif { get; set; } = true;
    public ICollection<Facture> Factures { get; set; } = [];
    public ICollection<LigneEcriture> Lignes { get; set; } = [];
}

public enum TypeTiers
{
    Client = 0,
    Fournisseur = 1,
    ClientFournisseur = 2,
}

// ─── Facture ──────────────────────────────────────────────────────────────────

public class Facture : TenantEntity
{
    public string Numero { get; set; } = string.Empty;     // ex: "FA-2024-001"
    public TypeFacture Type { get; set; }
    public DateTime DateFacture { get; set; }
    public DateTime? DateEcheance { get; set; }
    public StatutFacture Statut { get; set; } = StatutFacture.Brouillon;
    public Guid TiersId { get; set; }
    public Tiers Tiers { get; set; } = null!;
    public string Devise { get; set; } = "STD";
    public decimal TotalHT { get; set; }
    public decimal TotalTVA { get; set; }
    public decimal TotalTTC { get; set; }
    public decimal MontantRegle { get; set; } = 0;
    public decimal Solde => TotalTTC - MontantRegle;
    public string? Notes { get; set; }
    public ICollection<LigneFacture> Lignes { get; set; } = [];
    public ICollection<EcritureComptable> Ecritures { get; set; } = [];
}

public enum TypeFacture { Vente, Achat, AvoirVente, AvoirAchat }
public enum StatutFacture { Brouillon, Emise, PartiellemntRegle, Regle, Annulee }

public class LigneFacture : TenantEntity
{
    public Guid FactureId { get; set; }
    public Facture Facture { get; set; } = null!;
    public string Description { get; set; } = string.Empty;
    public decimal Quantite { get; set; }
    public decimal PrixUnitaireHT { get; set; }
    public decimal TauxTVA { get; set; } = 0;
    public decimal MontantHT => Quantite * PrixUnitaireHT;
    public decimal MontantTVA => MontantHT * TauxTVA / 100;
    public decimal MontantTTC => MontantHT + MontantTVA;
    public Guid? CompteProduitsId { get; set; }
    public CompteComptable? CompteProduits { get; set; }
}
