using Finama.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finama.Infrastructure.Configurations;

public class SequenceComptableConfiguration : IEntityTypeConfiguration<SequenceComptable>
{
    public void Configure(EntityTypeBuilder<SequenceComptable> b)
    {
        b.ToTable("SequencesComptables");
        b.HasKey(e => e.Id);
        b.Property(e => e.Cle).HasMaxLength(30).IsRequired();
        b.HasIndex(e => new { e.TenantId, e.Cle, e.Annee }).IsUnique();
    }
}

/// <summary>Un seul exercice actif par tenant et par année (évite le doublon créé par la clôture).</summary>
public class ExerciceComptableConfiguration : IEntityTypeConfiguration<ExerciceComptable>
{
    public void Configure(EntityTypeBuilder<ExerciceComptable> b)
    {
        b.HasIndex(e => new { e.TenantId, e.Annee })
         .IsUnique()
         .HasFilter("[IsDeleted] = 0");
    }
}
