using Finama.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finama.Infrastructure.Configurations;

public class ParametresComptablesConfiguration : IEntityTypeConfiguration<ParametresComptables>
{
    public void Configure(EntityTypeBuilder<ParametresComptables> b)
    {
        b.ToTable("ParametresComptables");
        b.HasIndex(x => x.TenantId).IsUnique();
        foreach (var p in new[] { "CompteVentes", "CompteAchats", "CompteTvaCollectee", "CompteTvaDeductible", "CompteClients",
                                  "CompteFournisseurs", "CompteBanque", "CompteCaisse", "CompteMobileMoney", "CompteClientsCaisse", "CompteRemisesFidelite" })
            b.Property<string>(p).HasMaxLength(20).IsRequired();
    }
}

public class ReglementFactureConfiguration : IEntityTypeConfiguration<ReglementFacture>
{
    public void Configure(EntityTypeBuilder<ReglementFacture> b)
    {
        b.ToTable("ReglementsFacture");
        b.Property(x => x.Montant).HasPrecision(18, 2);
        b.Property(x => x.Reference).HasMaxLength(100);
        b.HasOne(x => x.Facture).WithMany().HasForeignKey(x => x.FactureId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.FactureId);
    }
}

public class PieceComptabiliseeConfiguration : IEntityTypeConfiguration<PieceComptabilisee>
{
    public void Configure(EntityTypeBuilder<PieceComptabilisee> b)
    {
        b.ToTable("PiecesComptabilisees");
        b.Property(x => x.Source).HasMaxLength(20).IsRequired();
        b.HasIndex(x => new { x.TenantId, x.Source, x.SourceId }).IsUnique();
    }
}
