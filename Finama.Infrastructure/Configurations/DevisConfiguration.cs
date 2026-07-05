using Finama.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finama.Infrastructure.Configurations;

public class DevisConfiguration : IEntityTypeConfiguration<Devis>
{
    public void Configure(EntityTypeBuilder<Devis> b)
    {
        b.ToTable("Devis");
        b.HasKey(e => e.Id);

        // Index unique par Tenant et par Numéro de devis (Crucial pour le multi-tenant)
        b.HasIndex(e => new { e.TenantId, e.Numero }).IsUnique();

        b.Property(e => e.Numero).HasMaxLength(30).IsRequired();
        b.Property(e => e.Libelle).HasMaxLength(300).IsRequired();
        b.Property(e => e.Notes).HasMaxLength(1000);
        b.Property(e => e.Statut).HasConversion<int>();

        // Ignorer les propriétés calculées pour éviter qu'EF cherche à les créer en colonnes
        b.Ignore(e => e.TotalHT);
        b.Ignore(e => e.TotalTVA);
        b.Ignore(e => e.TotalTTC);

        // Relation avec le Tiers (Interdiction de supprimer un tiers lié à un devis)
        b.HasOne(e => e.Tiers)
            .WithMany()
            .HasForeignKey(e => e.TiersId)
            .OnDelete(DeleteBehavior.Restrict);

    }
}