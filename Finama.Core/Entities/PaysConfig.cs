namespace Finama.Core.Entities;

public class PaysConfig : BaseEntity
{
    public string Nom { get; set; } = string.Empty;           // "Togo"
    public string CodeISO { get; set; } = string.Empty;       // "TG"
    public string DeviseCode { get; set; } = string.Empty;    // "XOF"
    public string DeviseSymbole { get; set; } = string.Empty; // "FCFA"
    public decimal TauxTVAStandard { get; set; }              // 18.00
    public string CodeFiscal { get; set; } = string.Empty;    // "NIF"
    public string Langue { get; set; } = "fr";
    public bool EstActif { get; set; } = true;

    public ICollection<Tenant> Tenants { get; set; } = [];
}