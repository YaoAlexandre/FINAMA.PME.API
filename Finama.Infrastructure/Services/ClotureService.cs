using Finama.Core.Entities;
using Finama.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Finama.Infrastructure.Services;

public interface IClotureService
{
    Task CloturerExerciceAsync(Guid tenantId, Guid exerciceIdActuel, Guid utilisateurId);
}

/// <summary>
/// Clôture SYSCOHADA :
///  1. OD de solde des comptes de gestion (classes 6/7) vers 131 (bénéfice) ou 139 (perte), datée du dernier jour ;
///  2. Ouverture (ou réutilisation) de l'exercice N+1 ;
///  3. Écriture de Report à Nouveau (comptes de bilan + résultat) au premier jour de N+1 ;
///  4. Verrouillage de N.
/// Tout se fait dans une transaction unique.
/// </summary>
public class ClotureService : IClotureService
{
    private const string CompteBenefice = "131000";
    private const string ComptePerte = "139000";

    private readonly AppDbContext _db;

    public ClotureService(AppDbContext db)
    {
        _db = db;
    }

    public async Task CloturerExerciceAsync(Guid tenantId, Guid exerciceIdActuel, Guid utilisateurId)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync();

        // ─── 1. Contrôles préalables ──────────────────────────────────────────
        var exercice = await _db.Exercices
            .FirstOrDefaultAsync(e => e.Id == exerciceIdActuel && e.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Exercice comptable introuvable.");

        if (exercice.EstCloture)
            throw new InvalidOperationException("Cet exercice est déjà clôturé.");

        var exerciceAnterieurOuvert = await _db.Exercices
            .AnyAsync(e => e.TenantId == tenantId && e.Annee < exercice.Annee && !e.EstCloture);
        if (exerciceAnterieurOuvert)
            throw new InvalidOperationException("Clôturez d'abord les exercices antérieurs, dans l'ordre chronologique.");

        var nbBrouillons = await _db.Ecritures
            .CountAsync(e => e.ExerciceId == exercice.Id && e.Statut == StatutEcriture.Brouillon);
        if (nbBrouillons > 0)
            throw new InvalidOperationException(
                $"Impossible de clôturer : {nbBrouillons} écriture(s) sont encore au statut Brouillon. Validez-les ou supprimez-les.");

        var devise = await _db.Tenants
            .Where(t => t.Id == tenantId)
            .Select(t => t.DeviseBase)
            .FirstOrDefaultAsync() is { Length: > 0 } d ? d : "XOF";

        // ─── 2. Soldes par compte (écritures validées uniquement) ─────────────
        var soldes = await _db.LignesEcriture
            .Where(l => l.Ecriture.ExerciceId == exercice.Id && l.Ecriture.Statut == StatutEcriture.Validee)
            .GroupBy(l => new { l.CompteId, l.Compte.Type })
            .Select(g => new SoldeCompte(g.Key.CompteId, g.Key.Type, g.Sum(l => l.Debit), g.Sum(l => l.Credit)))
            .ToListAsync();

        if (soldes.Sum(s => s.Debit) != soldes.Sum(s => s.Credit))
            throw new InvalidOperationException("Clôture bloquée : la balance de l'exercice est déséquilibrée. Lancez un contrôle de la balance.");

        var gestion = soldes
            .Where(s => s.Type is TypeCompte.ResultatCharge or TypeCompte.ResultatProduit && s.Solde != 0)
            .ToList();

        // Résultat = Produits − Charges = −(somme des soldes D−C des comptes de gestion)
        var resultatNet = -gestion.Sum(s => s.Solde);

        var numeroResultat = resultatNet >= 0 ? CompteBenefice : ComptePerte;
        var compteResultat = await _db.CompteComptables
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Numero == numeroResultat)
            ?? throw new InvalidOperationException(
                $"Compte réglementaire du résultat ({numeroResultat}) absent du plan comptable. Créez-le puis relancez la clôture.");

        // ─── 3. OD de solde des comptes de gestion ────────────────────────────
        if (gestion.Count > 0)
        {
            var od = NouvelleEcriture(tenantId, exercice.Id, utilisateurId,
                reference: $"CLOT-GES-{exercice.Annee}",
                date: exercice.DateFin,
                libelle: $"Solde des comptes de gestion — clôture {exercice.Annee}",
                journal: "OD");

            foreach (var s in gestion)
                od.Lignes.Add(LigneSolde(tenantId, s.CompteId, s.Solde, devise)); // ramène chaque compte à zéro

            if (resultatNet != 0)
                od.Lignes.Add(LigneMontant(tenantId, compteResultat.Id, devise,
                    debit: resultatNet < 0 ? -resultatNet : 0,
                    credit: resultatNet > 0 ? resultatNet : 0));

            _db.Ecritures.Add(od);
        }

        // ─── 4. Exercice N+1 : réutilisé s'il existe déjà (créé à la connexion) ───
        var anneeSuivante = exercice.Annee + 1;
        var exerciceSuivant = await _db.Exercices
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Annee == anneeSuivante);

        if (exerciceSuivant is null)
        {
            exerciceSuivant = new ExerciceComptable
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Annee = anneeSuivante,
                Libelle = $"Exercice {anneeSuivante}",
                DateDebut = new DateTime(anneeSuivante, 1, 1),
                DateFin = new DateTime(anneeSuivante, 12, 31),
                EstCloture = false
            };
            _db.Exercices.Add(exerciceSuivant);
        }
        else if (exerciceSuivant.EstCloture)
        {
            throw new InvalidOperationException($"L'exercice {anneeSuivante} est déjà clôturé : report à nouveau impossible.");
        }
        else if (await _db.Ecritures.AnyAsync(e => e.ExerciceId == exerciceSuivant.Id && e.Journal == "RAN"))
        {
            throw new InvalidOperationException($"Un report à nouveau existe déjà dans l'exercice {anneeSuivante}.");
        }

        // ─── 5. Report à nouveau : soldes de bilan + résultat de l'exercice ───
        var ran = NouvelleEcriture(tenantId, exerciceSuivant.Id, utilisateurId,
            reference: $"RAN-{anneeSuivante}",
            date: exerciceSuivant.DateDebut,
            libelle: $"Report à nouveau — ouverture de l'exercice {anneeSuivante}",
            journal: "RAN");

        var soldesBilan = soldes
            .Where(s => s.Type is TypeCompte.Bilan or TypeCompte.Tresorerie)
            .ToDictionary(s => s.CompteId, s => s.Solde);

        // Le compte de résultat reçoit le résultat de l'OD, qu'il ait été mouvementé ou non
        soldesBilan[compteResultat.Id] = soldesBilan.GetValueOrDefault(compteResultat.Id) - resultatNet;

        foreach (var (compteId, solde) in soldesBilan.Where(kv => kv.Value != 0))
            ran.Lignes.Add(LigneMontant(tenantId, compteId, devise,
                debit: solde > 0 ? solde : 0,
                credit: solde < 0 ? -solde : 0));

        if (ran.Lignes.Sum(l => l.Debit) != ran.Lignes.Sum(l => l.Credit))
            throw new InvalidOperationException("Report à nouveau déséquilibré : vérifiez le typage (Bilan / Gestion) des comptes du plan comptable.");

        if (ran.Lignes.Count > 0)
            _db.Ecritures.Add(ran);

        // ─── 6. Verrouillage ──────────────────────────────────────────────────
        exercice.EstCloture = true;
        exercice.ClotureLe = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private sealed record SoldeCompte(Guid CompteId, TypeCompte Type, decimal Debit, decimal Credit)
    {
        /// <summary>Solde débiteur positif, créditeur négatif.</summary>
        public decimal Solde => Debit - Credit;
    }

    private static EcritureComptable NouvelleEcriture(Guid tenantId, Guid exerciceId, Guid utilisateurId,
        string reference, DateTime date, string libelle, string journal) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        ExerciceId = exerciceId,
        Reference = reference,
        DateEcriture = date,
        Libelle = libelle,
        Journal = journal,
        Statut = StatutEcriture.Validee,
        UtilisateurId = utilisateurId
    };

    /// <summary>Ligne qui annule un solde : solde débiteur → crédit, solde créditeur → débit (jamais de montant négatif).</summary>
    private static LigneEcriture LigneSolde(Guid tenantId, Guid compteId, decimal solde, string devise)
        => LigneMontant(tenantId, compteId, devise,
            debit: solde < 0 ? -solde : 0,
            credit: solde > 0 ? solde : 0);

    private static LigneEcriture LigneMontant(Guid tenantId, Guid compteId, string devise, decimal debit, decimal credit) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        CompteId = compteId,
        Debit = debit,
        Credit = credit,
        Devise = devise,
        MontantDeviseBase = debit > 0 ? debit : credit
    };
}
