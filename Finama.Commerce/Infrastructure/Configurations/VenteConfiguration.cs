using Finama.Commerce.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finama.Commerce.Infrastructure.Configurations;

public class VenteConfiguration : IEntityTypeConfiguration<Vente>
{
    public void Configure(EntityTypeBuilder<Vente> builder)
    {
        builder.HasKey(v => v.Id);
        builder.Property(v => v.NumeroTicket).IsRequired().HasMaxLength(50);

        // Montants monétaires
        builder.Property(v => v.TotalHT).HasPrecision(18, 2);
        builder.Property(v => v.TotalTVA).HasPrecision(18, 2);
        builder.Property(v => v.TotalRemise).HasPrecision(18, 2);
        builder.Property(v => v.TotalTTC).HasPrecision(18, 2);
        builder.Property(v => v.MontantPaye).HasPrecision(18, 2);
        builder.Property(v => v.RenduMonnaie).HasPrecision(18, 2);

        // Relations hiérarchiques des lignes et paiements
        builder.HasMany(v => v.Lignes)
              .WithOne()
              .HasForeignKey(l => l.VenteId)
              .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(v => v.Paiements)
              .WithOne()
              .HasForeignKey(p => p.VenteId)
              .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(v => new { v.TenantId, v.NumeroTicket }).IsUnique();
        builder.HasIndex(v => new { v.TenantId, v.DateVente });

        // Idempotence garantie par la base : deux renvois simultanés du même ticket ne peuvent pas créer deux ventes.
        // Filtré : les anciennes ventes enregistrées sans identifiant (Guid vide) ne bloquent pas la migration.
        builder.HasIndex(v => new { v.TenantId, v.ClientRequestId })
               .IsUnique()
               .HasFilter("[ClientRequestId] <> '00000000-0000-0000-0000-000000000000'");
    }
}

public class VenteLigneConfiguration : IEntityTypeConfiguration<VenteLigne>
{
    public void Configure(EntityTypeBuilder<VenteLigne> builder)
    {
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Libelle).IsRequired().HasMaxLength(200);
        builder.Property(l => l.Quantite).HasPrecision(18, 3);
        builder.Property(l => l.PrixUnitaireTTC).HasPrecision(18, 2);
        builder.Property(l => l.RemisePourcent).HasPrecision(5, 2);
        builder.Property(l => l.RemiseMontant).HasPrecision(18, 2);
        builder.Property(l => l.TauxTva).HasPrecision(5, 2);
        builder.Property(l => l.MontantHT).HasPrecision(18, 2);
        builder.Property(l => l.MontantTVA).HasPrecision(18, 2);
        builder.Property(l => l.MontantTTC).HasPrecision(18, 2);
        builder.Property(l => l.CoutUnitaire).HasPrecision(18, 4);
    }
}