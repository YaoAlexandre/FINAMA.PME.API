using System.ComponentModel.DataAnnotations;

namespace Finama.Core.Entities;

public class Utilisateur : TenantEntity
{
    public string Nom { get; set; } = string.Empty;
    public string Prenom { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string MotDePasseHash { get; set; } = string.Empty;
    public RoleUtilisateur Role { get; set; } = RoleUtilisateur.Comptable;
    public bool EstActif { get; set; } = true;
    public DateTime? DerniereConnexionAt { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpireAt { get; set; }
    public string? OtpCode { get; set; }
    public DateTime? OtpExpireAt { get; set; }
    public bool IsOtpValidated { get; set; }
}

public class AppareilConfiance : BaseEntity
{
    public Guid Id { get; set; }
    public Guid UtilisateurId { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public DateTime DateDerniereValidation { get; set; }

    // Navigation property (optionnel mais recommandé pour EF)
    public Utilisateur Utilisateur { get; set; }
}

public enum RoleUtilisateur
{
    AdminTenant = 0,   // Propriétaire de l'entreprise, accès total
    Comptable = 1,     // Saisie, validation et édition des états financiers
    Lecture = 2,       // Consultation seule (ex: expert-comptable externe, actionnaire)
    Collaborateur = 3, // Saisie brute/brouillons uniquement (ex: stagiaire, facturier)
    Commercial = 4,    // Gestion devis, clients et pipeline de vente
    SuperAdmin = 99    // Administrateur de la plateforme SaaS
}
