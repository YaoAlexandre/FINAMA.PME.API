namespace Finama.Core.DTOs;

public class CreerCollaborateurDto
{
    public string Nom { get; set; } = string.Empty;

    public string Prenom { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string MotDePasse { get; set; } = string.Empty;

    /// <summary>
    /// Correspond à la valeur numérique de l'Enum RoleUtilisateur
    /// (0 = AdminTenant, 1 = Comptable, 2 = Lecture)
    /// </summary>
    public int Role { get; set; }
}