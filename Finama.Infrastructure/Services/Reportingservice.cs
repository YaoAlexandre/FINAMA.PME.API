using Microsoft.EntityFrameworkCore;
using Finama.Core.DTOs;
using Finama.Core.Entities;
using Finama.Infrastructure.Data;

namespace Finama.Infrastructure.Services;

public interface IReportingService
{
    Task<BalanceDto> GetBalanceAsync(FiltreBalanceQuery filtre);
    Task<GrandLivreDto> GetGrandLivreAsync(FiltreGrandLivreQuery filtre);
}

public class ReportingService : IReportingService
{
    private readonly AppDbContext _db;

    public ReportingService(AppDbContext db)
    {
        _db = db;
    }

    // ─── Balance des comptes ──────────────────────────────────────────────────
    public async Task<BalanceDto> GetBalanceAsync(FiltreBalanceQuery filtre)
    {
        var exercice = await _db.Exercices
            .FirstOrDefaultAsync(e => e.Id == filtre.ExerciceId)
            ?? throw new KeyNotFoundException("Exercice introuvable.");

        var dateDebut = filtre.DateDebut ?? exercice.DateDebut;
        var dateFin = filtre.DateFin ?? exercice.DateFin;

        // Toutes les lignes d'écritures validées dans la période
        var lignesQuery = _db.LignesEcriture
            .Include(l => l.Compte)
            .Include(l => l.Ecriture)
            .Where(l => l.Ecriture.ExerciceId == filtre.ExerciceId
                     && l.Ecriture.Statut == StatutEcriture.Validee
                     && l.Ecriture.DateEcriture >= dateDebut
                     && l.Ecriture.DateEcriture <= dateFin);

        // Filtre par classe si demandé
        if (!string.IsNullOrEmpty(filtre.ClasseCompte)
            && int.TryParse(filtre.ClasseCompte, out var classeInt)
            && Enum.IsDefined(typeof(ClasseCompte), classeInt))
        {
            var classe = (ClasseCompte)classeInt;
            lignesQuery = lignesQuery.Where(l => l.Compte.Classe == classe);
        }

        var lignes = await lignesQuery.ToListAsync();

        // Regrouper par compte
        var parCompte = lignes
            .GroupBy(l => l.Compte)
            .Select(g => new LigneBalanceDto(
                CompteId: g.Key.Id,
                Numero: g.Key.Numero,
                Libelle: g.Key.Libelle,
                Classe: (int)g.Key.Classe,
                SoldeOuvertureDebit: 0,   // TODO: report à-nouveau exercice précédent
                SoldeOuvertureCredit: 0,
                MouvementsDebit: g.Sum(l => l.Debit),
                MouvementsCredit: g.Sum(l => l.Credit),
                SoldeFinalDebit: Math.Max(0, g.Sum(l => l.Debit) - g.Sum(l => l.Credit)),
                SoldeFinalCredit: Math.Max(0, g.Sum(l => l.Credit) - g.Sum(l => l.Debit))
            ))
            .OrderBy(l => l.Numero)
            .ToList();

        // Ajouter les comptes sans mouvement si demandé
        if (filtre.IncludeVides)
        {
            var comptesAvecMouvement = parCompte.Select(l => l.CompteId).ToHashSet();
            var comptesVides = await _db.CompteComptables
                .Where(c => c.EstActif && !comptesAvecMouvement.Contains(c.Id))
                .Where(c => string.IsNullOrEmpty(filtre.ClasseCompte)
                    || c.Numero.StartsWith(filtre.ClasseCompte))
                .Select(c => new LigneBalanceDto(
                    c.Id, c.Numero, c.Libelle, (int)c.Classe,
                    0, 0, 0, 0, 0, 0))
                .ToListAsync();

            parCompte = parCompte.Concat(comptesVides)
                .OrderBy(l => l.Numero)
                .ToList();
        }

        // Vérification équilibre OHADA — débit total doit = crédit total
        var totaux = new TotauxBalanceDto(
            TotalOuvertureDebit: parCompte.Sum(l => l.SoldeOuvertureDebit),
            TotalOuvertureCredit: parCompte.Sum(l => l.SoldeOuvertureCredit),
            TotalMouvementsDebit: parCompte.Sum(l => l.MouvementsDebit),
            TotalMouvementsCredit: parCompte.Sum(l => l.MouvementsCredit),
            TotalSoldeFinalDebit: parCompte.Sum(l => l.SoldeFinalDebit),
            TotalSoldeFinalCredit: parCompte.Sum(l => l.SoldeFinalCredit)
        );

        return new BalanceDto(
            ExerciceId: exercice.Id,
            Annee: exercice.Annee,
            DateDebut: exercice.DateDebut,
            DateFin: exercice.DateFin,
            FiltreDebut: filtre.DateDebut,
            FiltreFin: filtre.DateFin,
            Lignes: parCompte,
            Totaux: totaux
        );
    }

    // ─── Grand livre ──────────────────────────────────────────────────────────
    public async Task<GrandLivreDto> GetGrandLivreAsync(FiltreGrandLivreQuery filtre)
    {
        var exercice = await _db.Exercices
            .FirstOrDefaultAsync(e => e.Id == filtre.ExerciceId)
            ?? throw new KeyNotFoundException("Exercice introuvable.");

        var dateDebut = filtre.DateDebut ?? exercice.DateDebut;
        var dateFin = filtre.DateFin ?? exercice.DateFin;

        // Construire la requête de base
        var comptesQuery = _db.CompteComptables.Where(c => c.EstActif);

        if (filtre.CompteId.HasValue)
            comptesQuery = comptesQuery.Where(c => c.Id == filtre.CompteId.Value);
        else if (!string.IsNullOrEmpty(filtre.CompteNumero))
            comptesQuery = comptesQuery.Where(c => c.Numero.StartsWith(filtre.CompteNumero));

        var comptes = await comptesQuery
            .OrderBy(c => c.Numero)
            .Skip((filtre.Page - 1) * filtre.PageSize)
            .Take(filtre.PageSize)
            .ToListAsync();

        var compteIds = comptes.Select(c => c.Id).ToList();

        // Charger tous les mouvements en une seule requête
        var mouvements = await _db.LignesEcriture
            .Include(l => l.Ecriture)
            .Include(l => l.Tiers)
            .Where(l => compteIds.Contains(l.CompteId)
                     && l.Ecriture.ExerciceId == filtre.ExerciceId
                     && l.Ecriture.Statut == StatutEcriture.Validee
                     && l.Ecriture.DateEcriture >= dateDebut
                     && l.Ecriture.DateEcriture <= dateFin)
            .OrderBy(l => l.Ecriture.DateEcriture)
            .ThenBy(l => l.Ecriture.Reference)
            .ToListAsync();

        // Construire le grand livre par compte
        var grandLivreComptes = comptes.Select(compte =>
        {
            var mvtsCompte = mouvements
                .Where(l => l.CompteId == compte.Id)
                .ToList();

            decimal soldeCumule = 0;
            var lignes = mvtsCompte.Select(l =>
            {
                // Solde cumulé : débit augmente, crédit diminue pour comptes actif/charge
                // Inversé pour comptes passif/produit
                var estDebiteur = compte.Classe is ClasseCompte.Classe1
                    or ClasseCompte.Classe2 or ClasseCompte.Classe3
                    ? false  // passif : crédit normal
                    : compte.Classe is ClasseCompte.Classe6
                        ? true   // charges : débit normal
                        : compte.Classe is ClasseCompte.Classe7
                            ? false  // produits : crédit normal
                            : true;  // trésorerie/tiers : débit normal

                soldeCumule += l.Debit - l.Credit;

                return new MouvementGrandLivreDto(
                    EcritureId: l.EcritureId,
                    Reference: l.Ecriture.Reference,
                    Date: l.Ecriture.DateEcriture,
                    Journal: l.Ecriture.Journal,
                    Libelle: l.Libelle ?? l.Ecriture.Libelle,
                    TiersNom: l.Tiers?.Nom,
                    Debit: l.Debit,
                    Credit: l.Credit,
                    SoldeCumule: soldeCumule
                );
            }).ToList();

            return new CompteGrandLivreDto(
                CompteId: compte.Id,
                Numero: compte.Numero,
                Libelle: compte.Libelle,
                SoldeOuverture: 0,   // TODO: report à-nouveau
                Mouvements: lignes,
                TotalDebit: mvtsCompte.Sum(l => l.Debit),
                TotalCredit: mvtsCompte.Sum(l => l.Credit),
                SoldeFinal: mvtsCompte.Sum(l => l.Debit) - mvtsCompte.Sum(l => l.Credit)
            );
        })
        .Where(c => c.Mouvements.Any()) // exclure comptes sans mouvement
        .ToList();

        return new GrandLivreDto(
            ExerciceId: exercice.Id,
            Annee: exercice.Annee,
            Comptes: grandLivreComptes
        );
    }
}