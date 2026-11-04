using Finama.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finama.Commerce.Core.Domain
{
    public enum TypeClient { Particulier = 1, Entreprise = 2 }
    public enum TypeMouvementFidelite { Gain = 1, Utilisation = 2, Ajustement = 3, Expiration = 4 }

    public class Client : TenantEntity
    {
        public string Code { get; set; } = "";               // CLI-00001, unique par tenant
        public string Nom { get; set; } = "";
        public TypeClient Type { get; set; } = TypeClient.Particulier;
        public string? Telephone { get; set; }
        public string? Email { get; set; }
        public string? Adresse { get; set; }
        public string? Nif { get; set; }                     // entreprises : identifiant fiscal
        public Guid? TiersId { get; set; }                   // rattachement futur au Tiers comptable Finama (compte 411)
        public decimal PlafondCredit { get; set; }           // 0 = vente à crédit interdite
        public decimal SoldeCredit { get; set; }             // créance en cours
        public int PointsFidelite { get; set; }
        public DateTime DateCreation { get; set; }
        public DateTime? DateDerniereVente { get; set; }
        public bool EstActif { get; set; } = true;

        /// <summary>
        /// Concurrence optimiste : deux caisses qui vendent au même client ne peuvent plus écraser
        /// le solde de crédit ni les points (la vente est rejouée par Transactions.ExecuterAsync).
        /// </summary>
        [System.ComponentModel.DataAnnotations.Timestamp]
        public byte[] RowVersion { get; set; } = [];
    }

    /// <summary>Journal de fidélité : insertion seule. Points signés (+ gain, − utilisation).</summary>
    public class MouvementFidelite : TenantEntity
    {
        public Guid ClientId { get; set; }
        public Guid? VenteId { get; set; }
        public TypeMouvementFidelite Type { get; set; }
        public int Points { get; set; }
        public int SoldeApres { get; set; }
        public string? Motif { get; set; }
        public DateTime DateMouvement { get; set; }
    }
}

namespace Finama.Commerce.Infrastructure.Configurations
{
    using Finama.Commerce.Core.Domain;

    public class ClientConfiguration : IEntityTypeConfiguration<Client>
    {
        public void Configure(EntityTypeBuilder<Client> builder)
        {
            builder.HasKey(c => c.Id);
            builder.Property(c => c.Code).IsRequired().HasMaxLength(30);
            builder.Property(c => c.Nom).IsRequired().HasMaxLength(200);
            builder.Property(c => c.Telephone).HasMaxLength(30);
            builder.Property(c => c.Email).HasMaxLength(150);
            builder.Property(c => c.Adresse).HasMaxLength(300);
            builder.Property(c => c.Nif).HasMaxLength(50);
            builder.Property(c => c.PlafondCredit).HasPrecision(18, 2);
            builder.Property(c => c.SoldeCredit).HasPrecision(18, 2);

            builder.HasIndex(c => new { c.TenantId, c.Code }).IsUnique();
            builder.HasIndex(c => new { c.TenantId, c.Telephone });
        }
    }

    public class MouvementFideliteConfiguration : IEntityTypeConfiguration<MouvementFidelite>
    {
        public void Configure(EntityTypeBuilder<MouvementFidelite> builder)
        {
            builder.HasKey(m => m.Id);
            builder.Property(m => m.Motif).HasMaxLength(200);
            builder.HasIndex(m => new { m.TenantId, m.ClientId, m.DateMouvement });
        }
    }
}
