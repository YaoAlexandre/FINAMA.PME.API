using Finama.Commerce.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finama.Commerce.Infrastructure.Configurations;

public class SequenceDocumentConfiguration : IEntityTypeConfiguration<SequenceDocument>
{
    public void Configure(EntityTypeBuilder<SequenceDocument> builder)
    {
        // Nom de table fixe : il est référencé tel quel par le SQL d'incrément atomique dans PosService.
        builder.ToTable("SequencesDocuments");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Cle).IsRequired().HasMaxLength(50);
        builder.HasIndex(s => new { s.TenantId, s.Cle, s.Annee }).IsUnique();
    }
}
