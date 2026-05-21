using Finama.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Finama.Infrastructure.Configurations;

public class DevisConfiguration : IEntityTypeConfiguration<Devis>
{
    public void Configure(EntityTypeBuilder<Devis> b)
    {
        b.ToTable("Devis");
        b.HasKey(e => e.Id);
        b.HasIndex(e => new { e.TenantId, e.Numero }).IsUnique();
        b.Property(e => e.Numero).HasMaxLength(30).IsRequired();
        b.Property(e => e.Libelle).HasMaxLength(300).IsRequired();
        b.Property(e => e.Notes).HasMaxLength(1000);
        b.Property(e => e.Statut).HasConversion<int>();
        b.Ignore(e => e.TotalHT);
        b.Ignore(e => e.TotalTVA);
        b.Ignore(e => e.TotalTTC);
        b.HasOne(e => e.Tiers)
            .WithMany()
            .HasForeignKey(e => e.TiersId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public class LigneDevisConfiguration : IEntityTypeConfiguration<LigneDevis>
{
    public void Configure(EntityTypeBuilder<LigneDevis> b)
    {
        b.ToTable("LignesDevis");
        b.HasKey(e => e.Id);
        b.Property(e => e.Designation).HasMaxLength(300).IsRequired();
        b.Property(e => e.Quantite).HasColumnType("decimal(18,4)");
        b.Property(e => e.PrixUnitaireHT).HasColumnType("decimal(18,2)");
        b.Property(e => e.TauxTVA).HasColumnType("decimal(5,2)");
        b.Ignore(e => e.MontantHT);
        b.Ignore(e => e.MontantTVA);
        b.Ignore(e => e.MontantTTC);
        b.HasOne(e => e.Devis)
            .WithMany(d => d.Lignes)
            .HasForeignKey(e => e.DevisId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
