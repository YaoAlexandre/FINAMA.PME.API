using System.ComponentModel.DataAnnotations;

namespace Finama.Core.Entities;

public class Utilisateur : TenantEntity
{
    public string Nom { get; set; } = string.Empty;
    public string Prenom { get; set; } = string.Empty;
    public string NomComplet => $"{Prenom} {Nom}".Trim();
    public string Email { get; set; } = string.Empty;
    public string MotDePasseHash { get; set; } = string.Empty;
    public RoleUtilisateur Role { get; set; } = RoleUtilisateur.Comptable;

    /// <summary>Profil personnalisé (facultatif). S'il est vide, les permissions sont celles du profil prédéfini du rôle.</summary>
    public Guid? ProfilId { get; set; }
    public bool EstActif { get; set; } = true;
    public DateTime? DerniereConnexionAt { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpireAt { get; set; }
    public string? OtpCode { get; set; }
    public int OtpTentatives { get; set; }              // anti brute-force (max 5 par code)
    public DateTime? OtpExpireAt { get; set; }
    public bool IsOtpValidated { get; set; }
    public bool DoitChangerMotDePasse { get; set; } = true;
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
    AdminTenant = 0,   // Gérant / propriétaire : accès total, utilisateurs, paramètres, validations (annulations, clôtures)
    Comptable = 1,     // Comptabilité complète : écritures, validation, factures, états financiers
    Lecture = 2,       // Consultation seule (expert-comptable externe, associé, banque)
    Collaborateur = 3, // Aide-comptable / facturier : saisie en brouillon, sans validation
    Commercial = 4,    // Vendeur polyvalent : devis, clients, caisse et stock
    Caissier = 5,      // Caisse uniquement : vendre, encaisser, clients fidélité ; ni stock ni paramètres
    Magasinier = 6,    // Stock uniquement : articles, réceptions, inventaires, étiquettes ; pas de caisse
    SuperAdmin = 99    // Administrateur de la plateforme SaaS (Finama)
}
