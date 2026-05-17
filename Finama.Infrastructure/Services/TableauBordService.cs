using Microsoft.EntityFrameworkCore;
using Finama.Core.DTOs;
using Finama.Core.Entities;
using Finama.Infrastructure.Data;

namespace Finama.Infrastructure.Services;

public interface ITableauBordService
{
    Task<TableauBordDto> GetAsync(Guid exerciceId);
}

public class TableauBordService : ITableauBordService
{
    private readonly AppDbContext _db;

    public TableauBordService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<TableauBordDto> GetAsync(Guid exerciceId)
    {
        // ─── Exercice + tenant ────────────────────────────────────────────────
        var exercice = await _db.Exercices
            .FirstOrDefaultAsync(e => e.Id == exerciceId)
            ?? throw new KeyNotFoundException("Exercice introuvable.");

        var tenant = await _db.Tenants
            .Include(t => t.Pays)
            .IgnoreQueryFilters()
            .FirstAsync(t => t.Id == exercice.TenantId);

        // ─── Données comptables (écritures validées) ──────────────────────────
        var lignes = await _db.LignesEcriture
            .Include(l => l.Compte)
            .Include(l => l.Ecriture)
            .Where(l => l.Ecriture.ExerciceId == exerciceId
                     && l.Ecriture.Statut == StatutEcriture.Validee)
            .ToListAsync();

        // CA = total crédits classe 7 (produits)
        var chiffreAffaires = lignes
            .Where(l => l.Compte.Classe == ClasseCompte.Classe7)
            .Sum(l => l.Credit - l.Debit);

        // Charges = total débits classe 6
        var totalCharges = lignes
            .Where(l => l.Compte.Classe == ClasseCompte.Classe6)
            .Sum(l => l.Debit - l.Credit);

        // Trésorerie = solde classe 5
        var tresorerie = lignes
            .Where(l => l.Compte.Classe == ClasseCompte.Classe5)
            .Sum(l => l.Debit - l.Credit);

        var resultatNet = chiffreAffaires - totalCharges;

        // ─── CA mois précédent ────────────────────────────────────────────────
        var moisCourant     = DateTime.Today.Month;
        var anneeCourante   = DateTime.Today.Year;
        var debutMoisPrec   = new DateTime(anneeCourante, moisCourant, 1).AddMonths(-1);
        var finMoisPrec     = new DateTime(anneeCourante, moisCourant, 1).AddDays(-1);

        var caMoisPrecedent = lignes
            .Where(l => l.Compte.Classe == ClasseCompte.Classe7
                     && l.Ecriture.DateEcriture >= debutMoisPrec
                     && l.Ecriture.DateEcriture <= finMoisPrec)
            .Sum(l => l.Credit - l.Debit);

        var caMoisCourant = lignes
            .Where(l => l.Compte.Classe == ClasseCompte.Classe7
                     && l.Ecriture.DateEcriture.Month == moisCourant
                     && l.Ecriture.DateEcriture.Year == anneeCourante)
            .Sum(l => l.Credit - l.Debit);

        var evolutionCA = caMoisPrecedent == 0 ? 0
            : Math.Round((caMoisCourant - caMoisPrecedent) / caMoisPrecedent * 100, 1);

        // ─── Factures ─────────────────────────────────────────────────────────
        var factures = await _db.Factures
            .Include(f => f.Tiers)
            .Where(f => f.DateFacture.Year == exercice.Annee
                     && f.Type == TypeFacture.Vente)
            .ToListAsync();

        var facturesEnAttente = factures
            .Where(f => f.Statut != StatutFacture.Regle
                     && f.Statut != StatutFacture.Annulee)
            .ToList();

        // ─── Tiers ────────────────────────────────────────────────────────────
        var nombreClients      = await _db.Tiers.CountAsync(t => t.Type == TypeTiers.Client && t.EstActif);
        var nombreFournisseurs = await _db.Tiers.CountAsync(t => t.Type == TypeTiers.Fournisseur && t.EstActif);

        // ─── Écritures non validées ───────────────────────────────────────────
        var ecrituresNonValidees = await _db.Ecritures
            .CountAsync(e => e.ExerciceId == exerciceId
                          && e.Statut == StatutEcriture.Brouillon);

        // ─── Graphique CA mensuel (12 mois) ───────────────────────────────────
        var moisNoms = new[]
        {
            "", "Jan", "Fév", "Mar", "Avr", "Mai", "Jun",
            "Jul", "Aoû", "Sep", "Oct", "Nov", "Déc"
        };

        var caMensuel = Enumerable.Range(1, 12).Select(mois => new PointCaMensuelDto(
            Mois:           mois,
            LibelleMois:    moisNoms[mois],
            ChiffreAffaires: lignes
                .Where(l => l.Compte.Classe == ClasseCompte.Classe7
                         && l.Ecriture.DateEcriture.Month == mois
                         && l.Ecriture.DateEcriture.Year == exercice.Annee)
                .Sum(l => l.Credit - l.Debit),
            Charges: lignes
                .Where(l => l.Compte.Classe == ClasseCompte.Classe6
                         && l.Ecriture.DateEcriture.Month == mois
                         && l.Ecriture.DateEcriture.Year == exercice.Annee)
                .Sum(l => l.Debit - l.Credit)
        )).ToList();

        // ─── Top 5 clients ────────────────────────────────────────────────────
        var topClients = factures
            .Where(f => f.Statut != StatutFacture.Annulee)
            .GroupBy(f => f.Tiers)
            .Select(g => new TopClientDto(
                TiersId:        g.Key.Id,
                Nom:            g.Key.Nom,
                Code:           g.Key.Code,
                TotalFacture:   g.Sum(f => f.TotalTTC),
                NombreFactures: g.Count()
            ))
            .OrderByDescending(t => t.TotalFacture)
            .Take(5)
            .ToList();

        // ─── Dernières factures (10) ──────────────────────────────────────────
        var dernieresFactures = factures
            .OrderByDescending(f => f.DateFacture)
            .Take(10)
            .Select(f => new DerniereFactureDto(
                Id:           f.Id,
                Numero:       f.Numero,
                TiersNom:     f.Tiers.Nom,
                DateFacture:  f.DateFacture,
                DateEcheance: f.DateEcheance,
                TotalTTC:     f.TotalTTC,
                Solde:        f.Solde,
                Statut:       f.Statut.ToString(),
                EstEnRetard:  f.DateEcheance.HasValue
                              && f.DateEcheance < DateTime.Today
                              && f.Statut != StatutFacture.Regle
                              && f.Statut != StatutFacture.Annulee
            ))
            .ToList();

        return new TableauBordDto(
            Annee:                       exercice.Annee,
            ExerciceId:                  exercice.Id,
            DateDebut:                   exercice.DateDebut,
            DateFin:                     exercice.DateFin,
            Devise:                      tenant.DeviseBase,
            DeviseSymbole:               tenant.Pays.DeviseSymbole,
            ChiffreAffaires:             chiffreAffaires,
            ChiffreAffairesMoisPrecedent:caMoisPrecedent,
            EvolutionCA:                 evolutionCA,
            TotalCharges:                totalCharges,
            ResultatNet:                 resultatNet,
            Tresorerie:                  tresorerie,
            NombreFacturesEmises:        factures.Count,
            NombreFacturesEnAttente:     facturesEnAttente.Count,
            MontantFacturesEnAttente:    facturesEnAttente.Sum(f => f.Solde),
            NombreClients:               nombreClients,
            NombreFournisseurs:          nombreFournisseurs,
            NombreEcrituresNonValidees:  ecrituresNonValidees,
            CaMensuel:                   caMensuel,
            TopClients:                  topClients,
            DernieresFactures:           dernieresFactures
        );
    }
}
