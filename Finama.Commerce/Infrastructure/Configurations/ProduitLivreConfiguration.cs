using Finama.Commerce.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finama.Commerce.Infrastructure.Configurations;

public class ProduitLivreConfiguration : IEntityTypeConfiguration<ProduitLivre>
{
    public void Configure(EntityTypeBuilder<ProduitLivre> builder)
    {
        // Définition explicite de la clé primaire (souvent le ProduitId dans une relation 1-to-1)
        builder.HasKey(l => l.ProduitId);

        builder.Property(l => l.Isbn).IsRequired().HasMaxLength(20);
        builder.Property(l => l.Auteur).IsRequired().HasMaxLength(200);

        // Relation avec Produit
        builder.HasOne(l => l.Produit)
               .WithOne(p => p.Livre)
               .HasForeignKey<ProduitLivre>(l => l.ProduitId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}