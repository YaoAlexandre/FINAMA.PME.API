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

    // 🔹 Propriétés requises à ajouter :
    public bool Actif { get; set; } = true;

    public DateTime DateCreation { get; set; } = DateTime.UtcNow;

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

    /// <summary>
    /// Indique si l'entreprise dispose d'un accès valide (non expiré et actif).
    /// </summary>
    public bool HasActiveSubscription => EstActif &&
        (!AbonnementExpireAt.HasValue || AbonnementExpireAt.Value >= DateTime.UtcNow);

    /// <summary>
    /// Active ou renouvelle l'abonnement pour une durée donnée en mois.
    /// </summary>
    public void RenouvelerAbonnement(PlanAbonnement nouveauPlan, int mois)
    {
        Plan = nouveauPlan;
        var dateBase = (AbonnementExpireAt.HasValue && AbonnementExpireAt.Value > DateTime.UtcNow)
            ? AbonnementExpireAt.Value
            : DateTime.UtcNow;

        AbonnementExpireAt = dateBase.AddMonths(mois);
        EstActif = true;
    }
}

public enum PlanAbonnement
{
    Trial = 0,
    Starter = 1,
    Pro = 2,
    Business = 3,
}

public static class AbonnementConstants
{
    public const int DureeEssaiJours = 30;
}