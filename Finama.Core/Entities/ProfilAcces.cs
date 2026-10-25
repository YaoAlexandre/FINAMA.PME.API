namespace Finama.Core.Entities;

/// <summary>
/// Profil d'accès personnalisé d'une entreprise (ex. « Caissière + réception de stock »).
/// Les profils prédéfinis (Administrateur, Comptable, Caissier…) ne sont pas stockés : ils sont dans PermissionsFinama.
/// </summary>
public class ProfilAcces : TenantEntity
{
    public string Nom { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Permissions { get; set; } = string.Empty;   // codes séparés par des virgules
}
