namespace Finama.Core.Entities;

public class Tenant : BaseEntity
{
    public string Nom { get; set; } = string.Empty;
    public string SlugUnique { get; set; } = string.Empty;
    public string? NumeroFiscal { get; set; }
    public string? Adresse { get; set; }
    public string? Telephone { get; set; }
    public string Email { get; set; } = string.Empty;

    // ─── Localisation ─────────────────────────────────────────────────────────
    public Guid PaysId { get; set; }
    public PaysConfig Pays { get; set; } = null!;
    public string DeviseBase { get; set; } = string.Empty;    // copié depuis Pays à la création
    public decimal TauxTVA { get; set; }                      // copié depuis Pays, modifiable
    public bool AssujettTVA { get; set; } = true;
    public string PlanComptableCode { get; set; } = "OHADA";

    // ─── Abonnement ───────────────────────────────────────────────────────────
    public PlanAbonnement Plan { get; set; } = PlanAbonnement.Trial;
    public DateTime? AbonnementExpireAt { get; set; }
    public bool EstActif { get; set; } = true;
    public string? BanqueNom { get; set; }
    public string? BanqueBIC { get; set; }
    public string? BanqueRIB { get; set; }
    // ─── Navigation ───────────────────────────────────────────────────────────
    public ICollection<Utilisateur> Utilisateurs { get; set; } = [];
    public ICollection<CompteComptable> CompteComptables { get; set; } = [];
    public ICollection<EcritureComptable> Ecritures { get; set; } = [];
    public ICollection<Facture> Factures { get; set; } = [];
    public ICollection<ExerciceComptable> Exercices { get; set; } = [];
    public ICollection<Tiers> Tiers { get; set; } = [];

    /// <summary>
    /// Initialise DeviseBase et TauxTVA depuis le pays chargé.
    /// À appeler après avoir assigné Pays.
    /// </summary>
    public void AppliquerConfigPays()
    {
        DeviseBase = Pays.DeviseCode;
        TauxTVA = Pays.TauxTVAStandard;
    }
}

public enum PlanAbonnement
{
    Trial = 0,
    Starter = 1,
    Pro = 2,
    Business = 3,
}