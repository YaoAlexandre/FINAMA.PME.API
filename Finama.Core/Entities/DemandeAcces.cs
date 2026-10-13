namespace Finama.Core.Entities;

public enum StatutDemandeAcces
{
    EnAttente = 0,
    Approuvee = 1,
    Rejetee = 2
}

public class DemandeAcces : BaseEntity
{
    public string NomEntreprise { get; set; } = string.Empty;
    public string NomContact { get; set; } = string.Empty;
    public string PrenomContact { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Telephone { get; set; } = string.Empty;
    public Guid PaysId { get; set; }
    public PaysConfig Pays { get; set; } = null!; // Note : Vous avez nommé le DbSet PaysConfig 'Pays'
    public string? Message { get; set; }

    public StatutDemandeAcces Statut { get; set; } = StatutDemandeAcces.EnAttente;
    public DateTime DateDemande { get; set; } = DateTime.UtcNow;
    public DateTime? DateTraitement { get; set; }
    public string? MotifRefus { get; set; }
}