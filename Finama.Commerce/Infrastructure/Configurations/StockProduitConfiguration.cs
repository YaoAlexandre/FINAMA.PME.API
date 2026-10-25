using Finama.Commerce.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finama.Commerce.Infrastructure.Configurations;

public class StockProduitConfiguration : IEntityTypeConfiguration<StockProduit>
{
    public void Configure(EntityTypeBuilder<StockProduit> builder)
    {
        builder.HasKey(s => s.Id);

        // Précision pour la gestion au poids/vrac (3 décimales)
        builder.Property(s => s.Quantite).HasPrecision(18, 3);


        builder.HasIndex(s => new { s.TenantId, s.MagasinId, s.ProduitId }).IsUnique();
    }
}

public class LotStockConfiguration : IEntityTypeConfiguration<LotStock>
{
    public void Configure(EntityTypeBuilder<LotStock> builder)
    {
        builder.HasKey(l => l.Id);
        builder.Property(l => l.NumeroLot).IsRequired().HasMaxLength(50);
        builder.Property(l => l.QuantiteInitiale).HasPrecision(18, 3);
        builder.Property(l => l.QuantiteRestante).HasPrecision(18, 3);
        builder.Property(l => l.CoutUnitaire).HasPrecision(18, 4);

        builder.HasIndex(l => new { l.TenantId, l.MagasinId, l.ProduitId, l.NumeroLot });
    }
}