namespace Finama.Core.Entities;

/// <summary>
/// Compteur de numérotation continue par tenant, clé (journal / type de pièce) et année.
/// Incrémenté sous verrou SQL (UPDLOCK, HOLDLOCK) : aucun doublon ni trou dû à la concurrence.
/// Volontairement hors TenantEntity : manipulé uniquement en SQL brut, toujours filtré par TenantId.
/// </summary>
public class SequenceComptable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Cle { get; set; } = string.Empty;   // ex : "ECR-VT", "FAC-FA"
    public int Annee { get; set; }
    public int DernierNumero { get; set; }
}
