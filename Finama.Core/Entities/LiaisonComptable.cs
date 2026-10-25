namespace Finama.Core.Entities;

/// <summary>
/// Réglages de la liaison automatique entre les modules et la comptabilité (une ligne par entreprise).
/// Désactivée par défaut : rien ne change tant que l'administrateur ne l'active pas.
/// Les comptes sont donnés par leur numéro ; s'il n'existe pas exactement, le premier compte actif
/// qui commence par ce numéro (sans les zéros de fin) est utilisé (ex. « 701100 » → « 7011… »).
/// </summary>
public class ParametresComptables : TenantEntity
{
    public bool LiaisonActive { get; set; }
    public bool ValiderAutomatiquement { get; set; } = true;   // sinon : écritures en brouillon, à valider
    public DateTime? ActiveDepuis { get; set; }

    public string CompteVentes { get; set; } = "701100";
    public string CompteAchats { get; set; } = "601100";
    public string CompteTvaCollectee { get; set; } = "443100";
    public string CompteTvaDeductible { get; set; } = "445200";
    public string CompteClients { get; set; } = "411100";        // tiers sans sous-compte propre
    public string CompteFournisseurs { get; set; } = "401100";
    public string CompteBanque { get; set; } = "521100";
    public string CompteCaisse { get; set; } = "571100";
    public string CompteMobileMoney { get; set; } = "552100";    // SYSCOHADA révisé : monnaie électronique (téléphone)
    public string CompteClientsCaisse { get; set; } = "411100";  // ventes à crédit en caisse
    public string CompteRemisesFidelite { get; set; } = "709100"; // paiements en points de fidélité
}

public enum ModeReglement { Banque = 0, Caisse = 1, MobileMoney = 2 }

/// <summary>Encaissement d'une facture de vente ou paiement d'une facture d'achat (total ou partiel).</summary>
public class ReglementFacture : TenantEntity
{
    public Guid FactureId { get; set; }
    public Facture Facture { get; set; } = null!;
    public DateTime DateReglement { get; set; }
    public decimal Montant { get; set; }
    public ModeReglement Mode { get; set; }
    public string? Reference { get; set; }
    public Guid? EcritureId { get; set; }
}

/// <summary>Trace « telle pièce a donné telle écriture » : une pièce n'est jamais comptabilisée deux fois.</summary>
public class PieceComptabilisee : TenantEntity
{
    public string Source { get; set; } = string.Empty;   // FACTURE | REGLEMENT | CAISSE
    public Guid SourceId { get; set; }
    public Guid EcritureId { get; set; }
    public DateTime DateComptabilisation { get; set; } = DateTime.UtcNow;
}
