namespace Finama.Core.Entities;

/// <summary>
/// Journal des accès du support Finama à une entreprise (TenantId = entreprise consultée).
/// Visible par l'administrateur de l'entreprise : transparence sur qui a vu ses données, quand et pourquoi.
/// </summary>
public class AccesSupport : TenantEntity
{
    public Guid SuperAdminId { get; set; }
    public string SuperAdminNom { get; set; } = string.Empty;
    public string Motif { get; set; } = string.Empty;
    public bool LectureSeule { get; set; } = true;
    public DateTime DateDebut { get; set; }
    public DateTime DateExpiration { get; set; }
    public DateTime? DateFin { get; set; }        // sortie volontaire ; null = terminé par expiration
    public string? AdresseIp { get; set; }
}
