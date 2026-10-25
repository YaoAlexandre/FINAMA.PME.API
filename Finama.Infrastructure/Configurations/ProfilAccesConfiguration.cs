using Finama.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finama.Infrastructure.Configurations;

public class ProfilAccesConfiguration : IEntityTypeConfiguration<ProfilAcces>
{
    public void Configure(EntityTypeBuilder<ProfilAcces> b)
    {
        b.ToTable("ProfilsAcces");
        b.Property(p => p.Nom).HasMaxLength(80).IsRequired();
        b.Property(p => p.Description).HasMaxLength(250);
        b.Property(p => p.Permissions).HasMaxLength(2000);
        b.HasIndex(p => new { p.TenantId, p.Nom });
    }
}
