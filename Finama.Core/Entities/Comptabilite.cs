namespace Finama.Core.Entities;

// ─── Plan comptable ───────────────────────────────────────────────────────────

public class CompteComptable : TenantEntity
{
    public string Numero { get; set; } = string.Empty;   // ex: "411000"
    public string Libelle { get; set; } = string.Empty;  // ex: "Clients"
    public ClasseCompte Classe { get; set; }
    public TypeCompte Type { get; set; }
    public bool EstSysteme { get; set; } = false;        // vrai = importé du plan OHADA
    public bool EstActif { get; set; } = true;
    public Guid? CompteParentId { get; set; }
    public CompteComptable? CompteParent { get; set; }
    public ICollection<CompteComptable> SousComptes { get; set; } = [];
    public ICollection<LigneEcriture> Lignes { get; set; } = [];
}

public enum ClasseCompte
{
    Classe1 = 1,  // Capitaux
    Classe2 = 2,  // Immobilisations
    Classe3 = 3,  // Stocks
    Classe4 = 4,  // Tiers
    Classe5 = 5,  // Trésorerie
    Classe6 = 6,  // Charges
    Classe7 = 7,  // Produits
}

public enum TypeCompte
{
    Bilan,
    ResultatCharge,
    ResultatProduit,
    Tresorerie,
}

// ─── Exercice comptable ───────────────────────────────────────────────────────

public class ExerciceComptable : TenantEntity
{
    public int Annee { get; set; }
    public DateTime DateDebut { get; set; }
    public DateTime DateFin { get; set; }
    public bool EstCloture { get; set; } = false;
    public DateTime? ClotureLe { get; set; }
    public ICollection<EcritureComptable> Ecritures { get; set; } = [];
}

// ─── Écriture comptable (en-tête) ─────────────────────────────────────────────

public class EcritureComptable : TenantEntity
{
    public string Reference { get; set; } = string.Empty;   // ex: "VTE-2024-001"
    public DateTime DateEcriture { get; set; }
    public string Libelle { get; set; } = string.Empty;
    public string Journal { get; set; } = string.Empty;     // AC, VT, BQ, CA, OD
    public StatutEcriture Statut { get; set; } = StatutEcriture.Brouillon;
    public Guid ExerciceId { get; set; }
    public ExerciceComptable Exercice { get; set; } = null!;
    public Guid? FactureId { get; set; }
    public Facture? Facture { get; set; }
    public Guid UtilisateurId { get; set; }
    public Utilisateur Utilisateur { get; set; } = null!;
    public ICollection<LigneEcriture> Lignes { get; set; } = [];

    // Propriété calculée — vérification équilibre débit/crédit
    public bool EstEquilibree =>
        Lignes.Sum(l => l.Debit) == Lignes.Sum(l => l.Credit);
}

public enum StatutEcriture
{
    Brouillon = 0,
    Validee = 1,
    Cloturee = 2,
}

// ─── Ligne d'écriture (détail) ────────────────────────────────────────────────

public class LigneEcriture : TenantEntity
{
    public Guid EcritureId { get; set; }
    public EcritureComptable Ecriture { get; set; } = null!;
    public Guid CompteId { get; set; }
    public CompteComptable Compte { get; set; } = null!;
    public Guid? TiersId { get; set; }
    public Tiers? Tiers { get; set; }
    public string? Libelle { get; set; }
    public decimal Debit { get; set; } = 0;
    public decimal Credit { get; set; } = 0;
    public string Devise { get; set; } = "STD";
    public decimal? TauxChange { get; set; }            // si devise != devise de base
    public decimal? MontantDeviseBase { get; set; }
}
