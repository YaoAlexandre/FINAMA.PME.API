namespace Finama.Core.Entities;

/// <summary>Un appareil (téléphone, ordinateur) abonné aux notifications d'un utilisateur.</summary>
public class AbonnementPush : TenantEntity
{
    public Guid UtilisateurId { get; set; }
    public string Endpoint { get; set; } = string.Empty;   // adresse du service de notification du navigateur
    public string P256dh { get; set; } = string.Empty;     // clé publique de chiffrement de l'appareil
    public string Auth { get; set; } = string.Empty;       // secret d'authentification de l'appareil
    public string? Appareil { get; set; }                   // ex. « iPhone · Safari »
    public DateTime DateAbonnement { get; set; } = DateTime.UtcNow;
    public DateTime? DernierEnvoi { get; set; }
}

/// <summary>Trace des alertes déjà envoyées : une même alerte n'est jamais répétée (par jour, ou une seule fois).</summary>
public class NotificationEnvoyee : TenantEntity
{
    public string Cle { get; set; } = string.Empty;        // ex. « stock-bas:2026-10-21 », « devis-accepte:{id} »
    public DateTime DateEnvoi { get; set; } = DateTime.UtcNow;
}
