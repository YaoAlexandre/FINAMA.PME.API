using Finama.Commerce.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finama.Commerce.Infrastructure.Configurations;

public class ProduitConfiguration : IEntityTypeConfiguration<Produit>
{
    public void Configure(EntityTypeBuilder<Produit> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Reference).IsRequired().HasMaxLength(50);
        builder.Property(p => p.Libelle).IsRequired().HasMaxLength(200);

        // Précisions décimales SQL Server
        builder.Property(p => p.PrixVenteTTC).HasPrecision(18, 2);
        builder.Property(p => p.TauxTva).HasPrecision(5, 2);
        builder.Property(p => p.PrixAchatMoyen).HasPrecision(18, 4);
        builder.Property(p => p.DernierPrixAchat).HasPrecision(18, 4);
        builder.Property(p => p.SeuilReappro).HasPrecision(18, 3);
        builder.Property(p => p.QuantiteReappro).HasPrecision(18, 3);

        // Relation 1-to-1 avec les spécificités de Librairie
        builder.HasOne(p => p.Livre)
              .WithOne(l => l.Produit)
              .HasForeignKey<ProduitLivre>(l => l.ProduitId)
              .OnDelete(DeleteBehavior.Cascade);

        // Index multi-tenant & performance
        builder.HasIndex(p => new { p.TenantId, p.Reference }).IsUnique();
    }
}

public class CodeBarresProduitConfiguration : IEntityTypeConfiguration<CodeBarresProduit>
{
    public void Configure(EntityTypeBuilder<CodeBarresProduit> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Code).IsRequired().HasMaxLength(50);
        builder.HasIndex(c => new { c.TenantId, c.Code }).IsUnique();
    }
}