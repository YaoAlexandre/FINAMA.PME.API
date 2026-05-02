using Finama.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finama.Infrastructure.Data.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> b)
    {
        b.HasKey(e => e.Id);
        b.HasIndex(e => e.SlugUnique).IsUnique();
        b.Property(e => e.Nom).HasMaxLength(200).IsRequired();
        b.Property(e => e.Email).HasMaxLength(200).IsRequired();
        b.Property(e => e.DeviseBase).HasMaxLength(3).IsRequired();
        b.Property(e => e.Plan).HasConversion<int>();
    }
}

public class UtilisateurConfiguration : IEntityTypeConfiguration<Utilisateur>
{
    public void Configure(EntityTypeBuilder<Utilisateur> b)
    {
        b.HasKey(e => e.Id);
        b.HasIndex(e => new { e.TenantId, e.Email }).IsUnique();
        b.Property(e => e.Email).HasMaxLength(200).IsRequired();
        b.Property(e => e.Nom).HasMaxLength(100).IsRequired();
        b.Property(e => e.Prenom).HasMaxLength(100).IsRequired();
        b.Property(e => e.MotDePasseHash).HasMaxLength(500).IsRequired();
        b.Property(e => e.Role).HasConversion<int>();
        b.HasOne(e => e.Tenant)
            .WithMany(t => t.Utilisateurs)
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class CompteComptableConfiguration : IEntityTypeConfiguration<CompteComptable>
{
    public void Configure(EntityTypeBuilder<CompteComptable> b)
    {
        b.HasKey(e => e.Id);
        b.HasIndex(e => new { e.TenantId, e.Numero }).IsUnique();
        b.Property(e => e.Numero).HasMaxLength(10).IsRequired();
        b.Property(e => e.Libelle).HasMaxLength(200).IsRequired();
        b.Property(e => e.Classe).HasConversion<int>();
        b.Property(e => e.Type).HasConversion<string>();
        b.HasOne(e => e.CompteParent)
            .WithMany(e => e.SousComptes)
            .HasForeignKey(e => e.CompteParentId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.Tenant)
            .WithMany(t => t.CompteComptables)
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public class EcritureComptableConfiguration : IEntityTypeConfiguration<EcritureComptable>
{
    public void Configure(EntityTypeBuilder<EcritureComptable> b)
    {
        b.HasKey(e => e.Id);
        b.HasIndex(e => new { e.TenantId, e.Reference }).IsUnique();
        b.Property(e => e.Reference).HasMaxLength(30).IsRequired();
        b.Property(e => e.Libelle).HasMaxLength(300).IsRequired();
        b.Property(e => e.Journal).HasMaxLength(5).IsRequired();
        b.Property(e => e.Statut).HasConversion<int>();
        b.Ignore(e => e.EstEquilibree); // propriété calculée, pas stockée
        b.HasOne(e => e.Exercice)
            .WithMany(ex => ex.Ecritures)
            .HasForeignKey(e => e.ExerciceId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.Facture)
            .WithMany(f => f.Ecritures)
            .HasForeignKey(e => e.FactureId)
            .OnDelete(DeleteBehavior.SetNull);
        b.HasOne(e => e.Tenant)
            .WithMany(t => t.Ecritures)
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.NoAction);

    }
}

public class LigneEcritureConfiguration : IEntityTypeConfiguration<LigneEcriture>
{
    public void Configure(EntityTypeBuilder<LigneEcriture> b)
    {
        b.HasKey(e => e.Id);
        b.Property(e => e.Debit).HasColumnType("decimal(18,2)");
        b.Property(e => e.Credit).HasColumnType("decimal(18,2)");
        b.Property(e => e.TauxChange).HasColumnType("decimal(18,6)");
        b.Property(e => e.MontantDeviseBase).HasColumnType("decimal(18,2)");
        b.Property(e => e.Devise).HasMaxLength(3);
        b.HasOne(e => e.Compte)
            .WithMany(c => c.Lignes)
            .HasForeignKey(e => e.CompteId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.Tiers)
            .WithMany(t => t.Lignes)
            .HasForeignKey(e => e.TiersId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class FactureConfiguration : IEntityTypeConfiguration<Facture>
{
    public void Configure(EntityTypeBuilder<Facture> b)
    {
        b.HasKey(e => e.Id);
        b.HasIndex(e => new { e.TenantId, e.Numero }).IsUnique();
        b.Property(e => e.Numero).HasMaxLength(30).IsRequired();
        b.Property(e => e.TotalHT).HasColumnType("decimal(18,2)");
        b.Property(e => e.TotalTVA).HasColumnType("decimal(18,2)");
        b.Property(e => e.TotalTTC).HasColumnType("decimal(18,2)");
        b.Property(e => e.MontantRegle).HasColumnType("decimal(18,2)");
        b.Property(e => e.Type).HasConversion<string>();
        b.Property(e => e.Statut).HasConversion<int>();
        b.Ignore(e => e.Solde);
        b.HasOne(e => e.Tiers)
            .WithMany(t => t.Factures)
            .HasForeignKey(e => e.TiersId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.Tenant)
            .WithMany(t => t.Factures)
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public class LigneFactureConfiguration : IEntityTypeConfiguration<LigneFacture>
{
    public void Configure(EntityTypeBuilder<LigneFacture> b)
    {
        b.HasKey(e => e.Id);
        b.Property(e => e.Quantite).HasColumnType("decimal(18,4)");
        b.Property(e => e.PrixUnitaireHT).HasColumnType("decimal(18,2)");
        b.Property(e => e.TauxTVA).HasColumnType("decimal(5,2)");
        b.Ignore(e => e.MontantHT);
        b.Ignore(e => e.MontantTVA);
        b.Ignore(e => e.MontantTTC);
        b.HasOne(e => e.Facture)
            .WithMany(f => f.Lignes)
            .HasForeignKey(e => e.FactureId)
            .OnDelete(DeleteBehavior.Restrict);

    }
}
