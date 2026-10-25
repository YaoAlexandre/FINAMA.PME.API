using Finama.Core.Entities;

namespace Finama.Commerce.Core.Domain;

public enum ModePaiement { Especes = 1, Carte = 2, MobileMoney = 3, Credit = 4, Points = 5 }
public enum StatutSession { Ouverte = 0, Cloturee = 1 }
public enum StatutVente { Validee = 1, Annulee = 2 }

public class Caisse : TenantEntity
{
    public Guid MagasinId { get; set; }
    public string Code { get; set; } = "";
    public string Libelle { get; set; } = "";
    public bool EstActif { get; set; } = true;
}

public class SessionCaisse : TenantEntity
{
    public Guid CaisseId { get; set; }
    public Guid MagasinId { get; set; }
    public Guid CaissierId { get; set; }
    public DateTime DateOuverture { get; set; }
    public decimal FondDeCaisse { get; set; }
    public StatutSession Statut { get; set; } = StatutSession.Ouverte;
    public DateTime? DateCloture { get; set; }
    public ClotureCaisse? Cloture { get; set; }
}

/// <summary>Clôture Z : le caissier déclare ses montants sans voir le théorique ; le système calcule l'écart après coup.</summary>
public class ClotureCaisse : TenantEntity
{
    public Guid SessionCaisseId { get; set; }
    public DateTime DateCloture { get; set; }
    public decimal TotalDeclare { get; set; }
    public decimal TotalTheorique { get; set; }
    public decimal EcartTotal { get; set; }
    public bool EcartSignale { get; set; }
    public string? Commentaire { get; set; }
    public Guid? ValideParId { get; set; }
    public DateTime? DateValidation { get; set; }
    public List<ClotureCaisseLigne> Lignes { get; set; } = [];
}

public class ClotureCaisseLigne : TenantEntity
{
    public Guid ClotureCaisseId { get; set; }
    public ModePaiement Mode { get; set; }
    public decimal MontantDeclare { get; set; }
    public decimal MontantTheorique { get; set; }
    public decimal Ecart { get; set; }
}

public class Vente : TenantEntity
{
    public Guid MagasinId { get; set; }
    public Guid CaisseId { get; set; }
    public Guid SessionCaisseId { get; set; }
    public Guid CaissierId { get; set; }
    public string NumeroTicket { get; set; } = "";
    public Guid ClientRequestId { get; set; }

    // Vente faite sans réseau puis synchronisée : numéro imprimé sur le ticket papier (ex. HL-C1-261021-0003)
    public bool EstHorsLigne { get; set; }
    public string? NumeroHorsLigne { get; set; }            // idempotence : un renvoi réseau ne crée jamais de doublon
    public DateTime DateVente { get; set; }
    public StatutVente Statut { get; set; } = StatutVente.Validee;
    public Guid? ClientId { get; set; }                  // module 5 (fidélité, vente en compte)
    public decimal TotalHT { get; set; }
    public decimal TotalTVA { get; set; }
    public decimal TotalRemise { get; set; }
    public decimal TotalTTC { get; set; }
    public decimal MontantPaye { get; set; }
    public decimal RenduMonnaie { get; set; }
    public Guid? RemiseAutoriseeParId { get; set; }
    public string? MotifAnnulation { get; set; }
    public Guid? AnnuleParId { get; set; }
    public DateTime? DateAnnulation { get; set; }
    public List<VenteLigne> Lignes { get; set; } = [];
    public List<Paiement> Paiements { get; set; } = [];
}

public class VenteLigne : TenantEntity
{
    public Guid VenteId { get; set; }
    public Guid ProduitId { get; set; }
    public string Libelle { get; set; } = "";            // instantané : le ticket ne change jamais, même si le produit est modifié
    public string? CodeBarres { get; set; }
    public decimal Quantite { get; set; }
    public decimal PrixUnitaireTTC { get; set; }
    public decimal RemisePourcent { get; set; }
    public decimal RemiseMontant { get; set; }
    public decimal TauxTva { get; set; }
    public decimal MontantHT { get; set; }
    public decimal MontantTVA { get; set; }
    public decimal MontantTTC { get; set; }
    public decimal CoutUnitaire { get; set; }            // CMUP au moment de la vente : base du calcul de marge
}

public class Paiement : TenantEntity
{
    public Guid VenteId { get; set; }
    public Guid SessionCaisseId { get; set; }
    public ModePaiement Mode { get; set; }
    public decimal Montant { get; set; }                 // montant imputé à la vente (net du rendu monnaie)
    public decimal MontantRecu { get; set; }             // espèces tendues, ou montant débité
    public string? Operateur { get; set; }               // Flooz, T-Money, banque...
    public string? Reference { get; set; }
    public DateTime DatePaiement { get; set; }
}

public class SuperviseurPin : TenantEntity
{
    public Guid UtilisateurId { get; set; }
    public string PinHash { get; set; } = "";
    public bool EstActif { get; set; } = true;
    public int TentativesEchouees { get; set; }
    public DateTime? VerrouJusqua { get; set; }
}

public class ParametresCommerce : TenantEntity
{
    public decimal SeuilRemiseSansPinPourcent { get; set; } = 5;
    public decimal RemiseMaxPourcent { get; set; } = 30;
    public decimal SeuilEcartCaisseSignale { get; set; } = 500;
    public string PrefixeTicket { get; set; } = "T";
    public bool AutoriserStockNegatif { get; set; }
    public decimal MontantParPoint { get; set; } = 1000;   // 1 point gagné par tranche de X FCFA (0 = fidélité désactivée)
    public decimal ValeurPointFcfa { get; set; } = 10;     // valeur d'un point à l'utilisation

    // ── Profil d'activité : ce que l'entreprise utilise réellement (rien n'est imposé par défaut) ──
    public string? Activite { get; set; }                  // code du profil appliqué (GENERAL, ALIMENTATION, MODE...) ; null = pas encore configuré
    public bool UtiliseLots { get; set; }                  // lots + date de péremption (sortie FEFO)
    public bool UtiliseVrac { get; set; }                  // vente au poids / à la mesure
    public bool UtiliseLivres { get; set; }                // ISBN, auteur, éditeurs
    public decimal TauxTvaDefaut { get; set; } = 18;       // TVA proposée à la création d'un article
}

/// <summary>Numérotation continue des documents (tickets, inventaires), incrémentée atomiquement en SQL.</summary>
public class SequenceDocument : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Cle { get; set; } = "";
    public int Annee { get; set; }
    public long DernierNumero { get; set; }
}
