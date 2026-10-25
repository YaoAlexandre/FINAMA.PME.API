using Finama.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finama.Infrastructure.Configurations;

public class DevisConfiguration : IEntityTypeConfiguration<Devis>
{
    public void Configure(EntityTypeBuilder<Devis> b)
    {
        b.Property(d => d.Numero).HasMaxLength(30);
        b.Property(d => d.RemiseGlobalePourcent).HasPrecision(5, 2);
        b.Property(d => d.FactureNumero).HasMaxLength(30);
        b.Property(d => d.MotifRefus).HasMaxLength(500);
        b.HasIndex(d => new { d.TenantId, d.Numero });
        b.HasIndex(d => new { d.TenantId, d.Statut });
    }
}

public class LigneDevisConfiguration : IEntityTypeConfiguration<LigneDevis>
{
    public void Configure(EntityTypeBuilder<LigneDevis> b)
    {
        b.Property(l => l.Quantite).HasPrecision(18, 3);
        b.Property(l => l.PrixUnitaireHT).HasPrecision(18, 2);
        b.Property(l => l.RemisePourcent).HasPrecision(5, 2);
        b.Property(l => l.TauxTVA).HasPrecision(5, 2);
    }
}
