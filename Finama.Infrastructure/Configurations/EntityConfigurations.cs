using Finama.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finama.Infrastructure.Data.Configurations;

// 🌟 Nouvelle configuration pour le tenant, avec sécurité renforcée et relations optimisées
public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> b)
    {
        b.HasKey(e => e.Id);
        b.HasIndex(e => e.SlugUnique).IsUnique();
        b.Property(e => e.Nom).HasMaxLength(200).IsRequired();
        b.Property(e => e.Email).HasMaxLength(200).IsRequired();
        b.Property(e => e.DeviseBase).HasMaxLength(3).IsRequired();
        b.Property(e => e.TauxTVA).HasColumnType("decimal(5,2)");
        b.Property(e => e.Plan).HasConversion<int>();

        // Relation Tenant → PaysConfig
        b.HasOne(e => e.Pays)
            .WithMany(p => p.Tenants)
            .HasForeignKey(e => e.PaysId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

// 🌟 Nouvelle configuration pour l'utilisateur, avec sécurité renforcée et relations optimisées
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

// 🌟 Nouvelle configuration pour le compte comptable, avec précision financière renforcée et relations optimisées
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

// 🌟 Nouvelle configuration pour l'écriture comptable, avec précision financière renforcée et relations optimisées
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

// 🌟 Nouvelle configuration pour la ligne d'écriture, avec précision financière renforcée et relations optimisées
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

// 🌟 Nouvelle configuration pour la facture, avec précision financière renforcée et relations optimisées
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

// 🌟 Nouvelle configuration pour la ligne de facture, avec calculs intégrés et précision financière renforcée
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


// 🌟 Nouvelle entité pour gérer les classes comptables de manière centralisée
public class ClasseComptableConfiguration : IEntityTypeConfiguration<ClasseComptable>
{
    public void Configure(EntityTypeBuilder<ClasseComptable> builder)
    {
        builder.ToTable("ClassesComptables");
        builder.HasKey(c => new { c.TenantId, c.Numero });
        builder.Property(c => c.Numero)
   .ValueGeneratedNever();

        builder.Property(c => c.Libelle)
            .IsRequired()
            .HasMaxLength(100);
    }
}

public class AppareilConfianceConfiguration : IEntityTypeConfiguration<AppareilConfiance>
{
    public void Configure(EntityTypeBuilder<AppareilConfiance> b)
    {
        b.ToTable("AppareilsConfiance");
        b.HasKey(e => e.Id);
        b.HasIndex(e => new { e.UtilisateurId, e.DeviceId }).IsUnique();
        b.Property(e => e.DeviceId).HasMaxLength(200).IsRequired();
        b.Property(e => e.DateDerniereValidation).IsRequired();
        b.HasOne(e => e.Utilisateur)
            .WithMany()
            .HasForeignKey(e => e.UtilisateurId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

// 🌟 Nouvelle entité pour gérer les devises de manière centralisée
public class DeviseConfiguration : IEntityTypeConfiguration<Devise>
{
    public void Configure(EntityTypeBuilder<Devise> b)
    {
        // 1. Structure de la table
        b.ToTable("Devises");
        b.HasKey(e => e.Id);
        b.HasIndex(e => e.Code).IsUnique();
        b.Property(e => e.Code).HasMaxLength(3).IsRequired();
        b.Property(e => e.Symbole).HasMaxLength(10).IsRequired();
        b.Property(e => e.Libelle).HasMaxLength(100).IsRequired();
        b.Property(e => e.TauxBaseDollar).HasColumnType("decimal(18,4)").IsRequired();
        b.Property(e => e.DateMiseAJour).IsRequired();
        b.Property(e => e.EstActive).HasDefaultValue(true);

        // 2. 🌟 Seed directement intégré ici !
        var dateSeeding = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // 2. Utilisez une chaîne brute ISO que SQL Server et EF Core liront comme une constante statique
        b.HasData(
            new Devise { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Code = "USD", Symbole = "$", Libelle = "Dollar américain", TauxBaseDollar = 1.0000m, DateMiseAJour = DateTime.Parse("2026-01-01T00:00:00Z") },
            new Devise { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Code = "XOF", Symbole = "FCFA", Libelle = "Franc CFA (BCEAO)", TauxBaseDollar = 615.0000m, DateMiseAJour = DateTime.Parse("2026-01-01T00:00:00Z") },
            new Devise { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), Code = "EUR", Symbole = "€", Libelle = "Euro", TauxBaseDollar = 0.9200m, DateMiseAJour = DateTime.Parse("2026-01-01T00:00:00Z") },
            new Devise { Id = Guid.Parse("44444444-4444-4444-4444-444444444444"), Code = "GHS", Symbole = "₵", Libelle = "Cedi ghanéen", TauxBaseDollar = 14.5000m, DateMiseAJour = DateTime.Parse("2026-01-01T00:00:00Z") },
            new Devise { Id = Guid.Parse("55555555-5555-5555-5555-555555555555"), Code = "NGN", Symbole = "₦", Libelle = "Naira nigérian", TauxBaseDollar = 1490.0000m, DateMiseAJour = DateTime.Parse("2026-01-01T00:00:00Z") }
        );
    }
}