using Finama.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finama.Infrastructure.Configurations;

/// <summary>Les entreprises existantes gardent TOUS leurs modules lors de la migration (valeur par défaut 7).</summary>
public class TenantModulesConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> b)
    {
        b.Property(t => t.Modules)
         .HasConversion<int>()
         .HasDefaultValue(ModuleFinama.Tous)
         .HasSentinel(ModuleFinama.Aucun);
    }
}
