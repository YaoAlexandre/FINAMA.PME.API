using System;

namespace Finama.Core.Entities;

public class Devise : BaseEntity
{
    // Code ISO (Ex: "XOF", "EUR", "GHS", "USD", "NGN")
    public string Code { get; set; } = string.Empty;

    // Symbole graphique (Ex: "FCFA", "€", "₵", "$", "₦")
    public string Symbole { get; set; } = string.Empty;

    // Libellé complet (Ex: "Franc CFA", "Euro")
    public string Libelle { get; set; } = string.Empty;

    // 🌟 Le taux par rapport à l'USD (Ex: 1 USD = 615.0000 XOF)
    public decimal TauxBaseDollar { get; set; }

    public DateTime DateMiseAJour { get; set; } = DateTime.UtcNow;
    public bool EstActive { get; set; } = true;
}