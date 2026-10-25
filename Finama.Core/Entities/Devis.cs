namespace Finama.Core.Entities;

/// <summary>
/// Devis client. Cycle : Brouillon → Envoyé → Accepté / Refusé / Expiré → Converti (facture créée).
/// Les montants sont calculés à partir des lignes (remise de ligne puis remise globale), arrondis à 2 décimales.
/// </summary>
public class Devis : TenantEntity
{
    public string Numero { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;
    public DateTime DateCreation { get; set; } = DateTime.UtcNow;
    public DateTime? DateExpiration { get; set; }
    public StatutDevis Statut { get; set; } = StatutDevis.Brouillon;
    public Guid TiersId { get; set; }
    public Guid CreePar { get; set; }                      // utilisateur (commercial) auteur du devis
    public string? Notes { get; set; }                     // visibles sur le PDF
    public string? Conditions { get; set; }                // conditions de paiement / livraison
    public decimal RemiseGlobalePourcent { get; set; }

    // Suivi
    public DateTime? DateEnvoi { get; set; }
    public DateTime? DateDecision { get; set; }            // acceptation ou refus
    public string? MotifRefus { get; set; }
    public Guid? FactureId { get; set; }                   // facture créée depuis ce devis
    public string? FactureNumero { get; set; }
    public Guid? DevisOrigineId { get; set; }              // devis dupliqué / nouvelle version
    public int Version { get; set; } = 1;

    // Navigation
    public Tiers Tiers { get; set; } = null!;
    public ICollection<LigneDevis> Lignes { get; set; } = new List<LigneDevis>();

    // Montants calculés (non stockés)
    public decimal SousTotalHT => Math.Round(Lignes.Where(l => !l.IsDeleted).Sum(l => l.MontantHT), 2);
    public decimal RemiseGlobale => Math.Round(SousTotalHT * RemiseGlobalePourcent / 100m, 2);
    public decimal TotalHT => SousTotalHT - RemiseGlobale;
    public decimal TotalTVA => Math.Round(Lignes.Where(l => !l.IsDeleted)
        .Sum(l => Math.Round(l.MontantHT * (1 - RemiseGlobalePourcent / 100m) * l.TauxTVA / 100m, 2)), 2);
    public decimal TotalTTC => TotalHT + TotalTVA;
}

public class LigneDevis : BaseEntity
{
    public Guid DevisId { get; set; }
    public int Ordre { get; set; }
    public Guid? ProduitId { get; set; }                   // article du catalogue (facultatif)
    public string Designation { get; set; } = string.Empty;
    public decimal Quantite { get; set; }
    public decimal PrixUnitaireHT { get; set; }
    public decimal RemisePourcent { get; set; }
    public decimal TauxTVA { get; set; }

    public decimal MontantHT => Math.Round(Quantite * PrixUnitaireHT * (1 - RemisePourcent / 100m), 2);
    public decimal MontantTVA => Math.Round(MontantHT * TauxTVA / 100m, 2);
    public decimal MontantTTC => MontantHT + MontantTVA;

    public Devis Devis { get; set; } = null!;
}

public enum StatutDevis
{
    Brouillon = 0,
    Envoye = 1,
    Accepte = 2,
    Refuse = 3,
    Expire = 4,
    Converti = 5   // facture créée
}
