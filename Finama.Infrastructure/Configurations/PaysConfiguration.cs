using Finama.Core.Entities;
using Finama.Infrastructure.Seeds;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finama.Infrastructure.Data.Configurations;

public class PaysConfigConfiguration : IEntityTypeConfiguration<PaysConfig>
{
    public void Configure(EntityTypeBuilder<PaysConfig> b)
    {
        b.HasKey(e => e.Id);
        b.HasIndex(e => e.CodeISO).IsUnique();
        b.HasIndex(e => e.DeviseCode);
        b.Property(e => e.Nom).HasMaxLength(100).IsRequired();
        b.Property(e => e.CodeISO).HasMaxLength(2).IsRequired();
        b.Property(e => e.DeviseCode).HasMaxLength(3).IsRequired();
        b.Property(e => e.DeviseSymbole).HasMaxLength(10).IsRequired();
        b.Property(e => e.TauxTVAStandard).HasColumnType("decimal(5,2)");
        b.Property(e => e.CodeFiscal).HasMaxLength(20);
        b.Property(e => e.Langue).HasMaxLength(2);

        // Seed des données initiales — IDs fixes pour la reproductibilité
        b.HasData(PaysSeed.Pays);
    }
}

