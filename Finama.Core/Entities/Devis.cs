using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Finama.Core.Entities
{
    public class Devis : TenantEntity
    {
        public string Numero { get; set; } = string.Empty;
        public string Libelle { get; set; } = string.Empty;
        public DateTime DateCreation { get; set; } = DateTime.UtcNow;
        public DateTime? DateExpiration { get; set; }
        public StatutDevis Statut { get; set; } = StatutDevis.Brouillon;
        public Guid TiersId { get; set; }
        public Guid CreePar { get; set; } // UserId du commercial
        public string? Notes { get; set; }

        // Calculés
        public decimal TotalHT => Lignes?.Sum(l => l.MontantHT) ?? 0;
        public decimal TotalTVA => Lignes?.Sum(l => l.MontantTVA) ?? 0;
        public decimal TotalTTC => Lignes?.Sum(l => l.MontantTTC) ?? 0;

        // Navigation
        public Tiers Tiers { get; set; } = null!;
        public ICollection<LigneDevis> Lignes { get; set; } = new List<LigneDevis>();
    }

    public class LigneDevis : BaseEntity
    {
        public Guid DevisId { get; set; }
        public string Designation { get; set; } = string.Empty;
        public decimal Quantite { get; set; }
        public decimal PrixUnitaireHT { get; set; }
        public decimal TauxTVA { get; set; }

        // Calculés
        public decimal MontantHT => Quantite * PrixUnitaireHT;
        public decimal MontantTVA => MontantHT * TauxTVA / 100;
        public decimal MontantTTC => MontantHT + MontantTVA;

        // Navigation
        public Devis Devis { get; set; } = null!;
    }

    public enum StatutDevis
    {
        Brouillon = 0,
        Envoye = 1,
        Accepte = 2,
        Refuse = 3,
        Expire = 4,
        Converti = 5  // Converti en facture
    }
}
