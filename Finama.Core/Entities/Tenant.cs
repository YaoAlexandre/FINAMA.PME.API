namespace Finama.Core.Entities;

public class Tenant : BaseEntity
{
    public string Nom { get; set; } = string.Empty;
    public string SlugUnique { get; set; } = string.Empty;  // ex: "boulangerie-silva"
    public string? NINEA { get; set; }                       // Numéro fiscal local
    public string? Adresse { get; set; }
    public string? Telephone { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DeviseBase { get; set; } = "STD";          // Dobra de STP par défaut
    public string PlanComptableCode { get; set; } = "OHADA"; // ou "STP"
    public PlanAbonnement Plan { get; set; } = PlanAbonnement.Starter;
    public DateTime? AbonnementExpireAt { get; set; }
    public bool EstActif { get; set; } = true;

    public ICollection<Utilisateur> Utilisateurs { get; set; } = [];
    public ICollection<CompteComptable> CompteComptables { get; set; } = [];
    public ICollection<EcritureComptable> Ecritures { get; set; } = [];
    public ICollection<Facture> Factures { get; set; } = [];
    public ICollection<ExerciceComptable> Exercices { get; set; } = [];
    public ICollection<Tiers> Tiers { get; set; } = [];
}

public enum PlanAbonnement
{
    Trial = 0,
    Starter = 1,
    Pro = 2,
    Business = 3
}
