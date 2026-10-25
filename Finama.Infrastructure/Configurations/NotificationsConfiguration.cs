using Finama.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finama.Infrastructure.Configurations;

public class AbonnementPushConfiguration : IEntityTypeConfiguration<AbonnementPush>
{
    public void Configure(EntityTypeBuilder<AbonnementPush> b)
    {
        b.ToTable("AbonnementsPush");
        b.Property(x => x.Endpoint).HasMaxLength(1000).IsRequired();
        b.Property(x => x.P256dh).HasMaxLength(200).IsRequired();
        b.Property(x => x.Auth).HasMaxLength(100).IsRequired();
        b.Property(x => x.Appareil).HasMaxLength(100);
        b.HasIndex(x => x.Endpoint).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.UtilisateurId });
    }
}

public class NotificationEnvoyeeConfiguration : IEntityTypeConfiguration<NotificationEnvoyee>
{
    public void Configure(EntityTypeBuilder<NotificationEnvoyee> b)
    {
        b.ToTable("NotificationsEnvoyees");
        b.Property(x => x.Cle).HasMaxLength(150).IsRequired();
        b.HasIndex(x => new { x.TenantId, x.Cle }).IsUnique();
    }
}
