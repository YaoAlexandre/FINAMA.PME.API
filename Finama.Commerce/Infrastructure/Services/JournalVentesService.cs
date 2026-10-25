using Finama.Commerce.Application.Common;
using Finama.Commerce.Core.Domain;
using Finama.Commerce.Core.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Finama.Commerce.Infrastructure.Services;

public interface IJournalVentesService
{
    Task<JournalJourDto> ObtenirJournalAsync(DateOnly date, Guid? magasinId, CancellationToken ct = default);
}

/// <summary>
/// Fin de journée : tout est recalculé à partir des ventes, paiements et clôtures enregistrés
/// (aucune donnée dupliquée). Les ventes annulées sont listées mais exclues des totaux.
/// </summary>
public class JournalVentesService : IJournalVentesService
{
    private static readonly ModePaiement[] ModesPhysiques = [ModePaiement.Especes, ModePaiement.Carte, ModePaiement.MobileMoney];

    private readonly ICommerceDbContext _db;

    public JournalVentesService(ICommerceDbContext db)
    {
        _db = db;
    }

    public async Task<JournalJourDto> ObtenirJournalAsync(DateOnly date, Guid? magasinId, CancellationToken ct = default)
    {
        var magasin = await ResoudreMagasinAsync(magasinId, ct);

        // Journée calendaire (UTC = heure de Lomé)
        var debut = date.ToDateTime(TimeOnly.MinValue);
        var fin = debut.AddDays(1);

        var ventes = await _db.Ventes
            .AsNoTracking()
            .Include(v => v.Lignes)
            .Include(v => v.Paiements)
            .Where(v => v.MagasinId == magasin.Id && v.DateVente >= debut && v.DateVente < fin)
            .OrderBy(v => v.DateVente)
            .ToListAsync(ct);

        var validees = ventes.Where(v => v.Statut == StatutVente.Validee).ToList();

        // Sessions ouvertes ce jour-là, ou ayant encaissé ce jour-là
        var sessionIds = ventes.Select(v => v.SessionCaisseId).Distinct().ToList();
        var sessions = await _db.Sessions
            .AsNoTracking()
            .Where(s => s.MagasinId == magasin.Id &&
                        ((s.DateOuverture >= debut && s.DateOuverture < fin) || sessionIds.Contains(s.Id)))
            .OrderBy(s => s.DateOuverture)
            .ToListAsync(ct);

        var idsSessions = sessions.Select(s => s.Id).ToList();
        var clotures = await _db.Clotures
            .AsNoTracking()
            .Include(c => c.Lignes)
            .Where(c => idsSessions.Contains(c.SessionCaisseId))
            .ToListAsync(ct);

        // Paiements de TOUTE la session (une session peut déborder sur la veille/le lendemain)
        var paiementsSessions = await (from p in _db.Paiements
                                       join v in _db.Ventes on p.VenteId equals v.Id
                                       where idsSessions.Contains(p.SessionCaisseId) && v.Statut == StatutVente.Validee
                                       select p).AsNoTracking().ToListAsync(ct);

        var caisseIds = ventes.Select(v => v.CaisseId).Concat(sessions.Select(s => s.CaisseId)).Distinct().ToList();
        var caisses = await _db.Caisses.AsNoTracking()
            .Where(c => caisseIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Libelle, ct);

        var clientIds = ventes.Where(v => v.ClientId.HasValue).Select(v => v.ClientId!.Value).Distinct().ToList();
        var clients = await _db.Clients.AsNoTracking()
            .Where(c => clientIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, ct);

        // ── Encaissements du jour par mode ──
        var paiementsJour = validees.SelectMany(v => v.Paiements).ToList();
        var encaissements = Enum.GetValues<ModePaiement>()
            .Select(m => new EncaissementModeDto(
                m,
                paiementsJour.Where(p => p.Mode == m).Sum(p => p.Montant),
                paiementsJour.Count(p => p.Mode == m)))
            .Where(e => e.NbOperations > 0)
            .ToList();

        // ── Sessions : théorique / déclaré / écart (même calcul que le rapport Z) ──
        var sessionsDto = sessions.Select(s =>
        {
            var cloture = clotures.FirstOrDefault(c => c.SessionCaisseId == s.Id);
            var lignes = ModesPhysiques.Select(mode =>
            {
                var theorique = paiementsSessions.Where(p => p.SessionCaisseId == s.Id && p.Mode == mode).Sum(p => p.Montant)
                                + (mode == ModePaiement.Especes ? s.FondDeCaisse : 0);
                var decl = cloture?.Lignes.FirstOrDefault(l => l.Mode == mode);
                return new ClotureLigneJourDto(mode, theorique, decl?.MontantDeclare, decl is null ? null : decl.MontantDeclare - theorique);
            }).ToList();

            return new SessionJourDto(
                s.Id, caisses.GetValueOrDefault(s.CaisseId, "Caisse"), s.DateOuverture, s.DateCloture, s.FondDeCaisse,
                s.Statut == StatutSession.Cloturee, lignes, cloture?.EcartTotal, cloture?.Commentaire);
        }).ToList();

        // ── Tickets ──
        var tickets = ventes.Select(v => new TicketJourDto(
            v.Id, v.NumeroTicket, v.DateVente, caisses.GetValueOrDefault(v.CaisseId, "Caisse"),
            v.ClientId is { } cid && clients.TryGetValue(cid, out var cl) ? cl.Nom : null,
            v.TotalHT, v.TotalTVA, v.TotalRemise, v.TotalTTC, v.RenduMonnaie,
            v.Statut == StatutVente.Annulee, v.MotifAnnulation,
            v.Lignes.Select(l => new TicketLigneJourDto(l.Libelle, l.Quantite, l.PrixUnitaireTTC, l.RemiseMontant, l.MontantTTC)).ToList(),
            v.Paiements.Select(p => new TicketPaiementJourDto(p.Mode, p.Montant, p.Reference)).ToList()))
            .ToList();

        // ── Articles vendus (marge au coût moyen enregistré au moment de la vente) ──
        var articles = validees.SelectMany(v => v.Lignes)
            .GroupBy(l => l.ProduitId)
            .Select(g =>
            {
                var cout = g.Sum(l => l.CoutUnitaire * l.Quantite);
                var ht = g.Sum(l => l.MontantHT);
                return new ArticleVenduDto(
                    g.Key, g.First().Libelle, g.Sum(l => l.Quantite),
                    g.Sum(l => l.MontantTTC), ht, Math.Round(cout, 2), Math.Round(ht - cout, 2));
            })
            .OrderByDescending(a => a.ChiffreAffairesTTC)
            .ToList();

        // ── Crédits accordés ce jour ──
        var credits = validees
            .Where(v => v.ClientId.HasValue)
            .Select(v => new { ClientId = v.ClientId!.Value, Credit = v.Paiements.Where(p => p.Mode == ModePaiement.Credit).Sum(p => p.Montant) })
            .Where(x => x.Credit > 0)
            .GroupBy(x => x.ClientId)
            .Select(g =>
            {
                var c = clients[g.Key];
                return new CreditJourDto(g.Key, c.Nom, c.Telephone, g.Sum(x => x.Credit), c.SoldeCredit, c.PlafondCredit);
            })
            .OrderByDescending(c => c.CreditDuJour)
            .ToList();

        var coutTotal = articles.Sum(a => a.Cout);
        var totalHT = validees.Sum(v => v.TotalHT);

        return new JournalJourDto(
            date, magasin.Id, magasin.Nom,
            validees.Count, ventes.Count - validees.Count,
            validees.Sum(v => v.TotalTTC), totalHT, validees.Sum(v => v.TotalTVA), validees.Sum(v => v.TotalRemise),
            coutTotal, Math.Round(totalHT - coutTotal, 2),
            encaissements, sessionsDto, tickets, articles, credits);
    }

    private async Task<Magasin> ResoudreMagasinAsync(Guid? magasinId, CancellationToken ct)
    {
        Magasin? magasin = null;
        if (magasinId is { } id && id != Guid.Empty)
            magasin = await _db.Magasins.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct);

        magasin ??= await _db.Magasins.AsNoTracking()
            .Where(m => m.EstActif && m.Type == TypeMagasin.PointDeVente)
            .OrderByDescending(m => m.EstPrincipal)
            .ThenBy(m => m.Nom)
            .FirstOrDefaultAsync(ct);

        return magasin ?? throw new BusinessRuleException("Aucun point de vente actif.");
    }
}
