namespace Finama.Core.DTOs;

public class CreerCollaborateurDto
{
    public string Nom { get; set; } = string.Empty;

    public string Prenom { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string MotDePasse { get; set; } = string.Empty;

    /// <summary>Profil prédéfini = valeur de RoleUtilisateur (0 Administrateur, 1 Comptable, 2 Consultation,
    /// 3 Aide-comptable, 4 Commercial, 5 Caissier, 6 Magasinier). Ignoré si ProfilId est renseigné.</summary>
    public int Role { get; set; }

    /// <summary>Profil personnalisé de l'entreprise (facultatif).</summary>
    public Guid? ProfilId { get; set; }
}

/// <summary>Changement d'accès d'un utilisateur : profil prédéfini (Role) OU profil personnalisé (ProfilId).</summary>
public class ModifierAccesDto
{
    public int Role { get; set; }
    public Guid? ProfilId { get; set; }
}