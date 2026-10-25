using Finama.API.Exports;
using Finama.Commerce.Application.Common;
using Finama.Commerce.Core.Domain;
using Finama.Commerce.Infrastructure.Services;
using Finama.Core.DTOs;
using Finama.Core.Entities;
using Finama.Infrastructure.Data;
using Finama.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Finama.API.Controllers;

/// <summary>
/// Éditions Excel (.xlsx) et PDF (?format=pdf), décrites une seule fois via IEdition. Comptabilité : balance, grand livre, journal, états financiers SYSCOHADA.
/// Commerce : fin de journée, stocks, inventaire, clients.
/// Les données passent par les mêmes services que les écrans : un export affiche exactement ce que l'écran montre.
/// </summary>
[ApiController]
[Route("api/exports")]
[Authorize]
public class ExportsController : ControllerBase
{

    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IReportingService _reporting;
    private readonly ICommerceDbContext _commerce;
    private readonly IJournalVentesService _journalVentes;
    private readonly IStockService _stock;

    public ExportsController(AppDbContext db, ITenantContext tenant, IReportingService reporting,
        ICommerceDbContext commerce, IJournalVentesService journalVentes, IStockService stock)
    {
        _db = db;
        _tenant = tenant;
        _reporting = reporting;
        _commerce = commerce;
        _journalVentes = journalVentes;
        _stock = stock;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // COMPTABILITÉ
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Balance générale à 6 colonnes, sous-totaux par classe.</summary>
    [HttpGet("balance")]
    [Authorize(Policy = Policies.LectureSeule)]
    public async Task<IActionResult> Balance([FromQuery] Guid? exerciceId, [FromQuery] DateTime? dateDebut, [FromQuery] DateTime? dateFin, [FromQuery] string? format = null)
    {
        var exercice = await ExerciceAsync(exerciceId);
        var balance = await _reporting.GetBalanceAsync(new FiltreBalanceQuery(exercice.Id, dateDebut, dateFin));

        using var xl = await EditionAsync(format);
        var f = xl.AjouterFeuille("Balance", "Balance générale des comptes", Periode(exercice, dateDebut, dateFin),
        [
            new("Compte", 12), new("Intitulé", 40),
            new("Ouverture débit", 15, FormatColonne.Montant), new("Ouverture crédit", 15, FormatColonne.Montant),
            new("Mouvements débit", 15, FormatColonne.Montant), new("Mouvements crédit", 15, FormatColonne.Montant),
            new("Solde débiteur", 15, FormatColonne.Montant), new("Solde créditeur", 15, FormatColonne.Montant),
        ]);

        foreach (var classe in balance.Lignes.GroupBy(l => l.Classe).OrderBy(g => g.Key))
        {
            f.AjouterSection($"Classe {classe.Key} — {LibelleClasse(classe.Key)}");
            var debut = f.LigneCourante;
            foreach (var l in classe.OrderBy(l => l.Numero))
                f.AjouterLigne(l.Numero, l.Libelle, l.SoldeOuvertureDebit, l.SoldeOuvertureCredit,
                               l.MouvementsDebit, l.MouvementsCredit, l.SoldeFinalDebit, l.SoldeFinalCredit);
            f.AjouterSousTotalFormule($"Total classe {classe.Key}", debut, 3, 4, 5, 6, 7, 8);
        }

        f.AjouterTotalFormule("TOTAL GÉNÉRAL", 3, 4, 5, 6, 7, 8);
        if (balance.Totaux.TotalMouvementsDebit != balance.Totaux.TotalMouvementsCredit)
            f.AjouterNote("⚠ Balance déséquilibrée : vérifiez les écritures (mouvements débit ≠ crédit).");
        f.Terminer();

        return Fichier(xl, $"Balance_{exercice.Annee}");
    }

    /// <summary>Grand livre : un bloc par compte (solde d'ouverture, mouvements, totaux, solde final).</summary>
    [HttpGet("grand-livre")]
    [Authorize(Policy = Policies.LectureSeule)]
    public async Task<IActionResult> GrandLivre([FromQuery] Guid? exerciceId, [FromQuery] string? compteNumero,
        [FromQuery] DateTime? dateDebut, [FromQuery] DateTime? dateFin, [FromQuery] string? format = null)
    {
        var exercice = await ExerciceAsync(exerciceId);
        var gl = await _reporting.GetGrandLivreAsync(new FiltreGrandLivreQuery(
            exercice.Id, CompteNumero: compteNumero, DateDebut: dateDebut, DateFin: dateFin, Page: 1, PageSize: 100_000));

        using var xl = await EditionAsync(format);
        var f = xl.AjouterFeuille("Grand livre", "Grand livre des comptes", Periode(exercice, dateDebut, dateFin),
        [
            new("Date", 11, FormatColonne.Date), new("Journal", 8), new("Pièce", 16), new("Libellé", 38), new("Tiers", 20),
            new("Débit", 15, FormatColonne.Montant), new("Crédit", 15, FormatColonne.Montant), new("Solde", 15, FormatColonne.Montant),
        ]);

        foreach (var c in gl.Comptes.OrderBy(c => c.Numero))
        {
            f.AjouterSection($"{c.Numero} — {c.Libelle}");
            if (c.SoldeOuverture != 0)
                f.AjouterLigne(null, null, null, "Solde d'ouverture", null, null, null, c.SoldeOuverture);

            var debut = f.LigneCourante;
            foreach (var m in c.Mouvements.OrderBy(m => m.Date))
                f.AjouterLigne(m.Date, m.Journal, m.Reference, m.Libelle, m.TiersNom, m.Debit, m.Credit, m.SoldeCumule);

            f.AjouterSousTotalFormule($"Total {c.Numero}", debut, 6, 7);
            f.AjouterLigneTotal(null, null, null, "Solde final", null, null, null, c.SoldeFinal);
        }

        f.AjouterTotalFormule("TOTAL GÉNÉRAL", 6, 7);
        f.Terminer(filtres: false);

        return Fichier(xl, $"GrandLivre_{exercice.Annee}{(string.IsNullOrWhiteSpace(compteNumero) ? "" : "_" + compteNumero)}");
    }

    /// <summary>Journal des écritures : une ligne par ligne d'écriture, filtrable dans Excel.</summary>
    [HttpGet("journal")]
    [Authorize(Policy = Policies.LectureSeule)]
    public async Task<IActionResult> Journal([FromQuery] Guid? exerciceId, [FromQuery] string? journal,
        [FromQuery] DateTime? dateDebut, [FromQuery] DateTime? dateFin, [FromQuery] bool inclureBrouillons = false, [FromQuery] string? format = null)
    {
        var exercice = await ExerciceAsync(exerciceId);

        var query = _db.Ecritures.AsNoTracking()
            .Include(e => e.Lignes).ThenInclude(l => l.Compte)
            .Include(e => e.Lignes).ThenInclude(l => l.Tiers)
            .Where(e => e.ExerciceId == exercice.Id);

        if (!inclureBrouillons) query = query.Where(e => e.Statut == StatutEcriture.Validee);
        if (!string.IsNullOrWhiteSpace(journal)) query = query.Where(e => e.Journal == journal.ToUpper());
        if (dateDebut.HasValue) query = query.Where(e => e.DateEcriture >= dateDebut.Value.Date);
        if (dateFin.HasValue) query = query.Where(e => e.DateEcriture <= dateFin.Value.Date);

        var ecritures = await query.OrderBy(e => e.DateEcriture).ThenBy(e => e.Reference).AsSplitQuery().ToListAsync();

        using var xl = await EditionAsync(format);
        var titre = string.IsNullOrWhiteSpace(journal) ? "Journal général" : $"Journal {journal.ToUpper()}";
        var f = xl.AjouterFeuille("Journal", titre, Periode(exercice, dateDebut, dateFin) + (inclureBrouillons ? " · brouillons inclus" : ""),
        [
            new("Date", 11, FormatColonne.Date), new("Journal", 8), new("Pièce", 16), new("Libellé écriture", 32),
            new("Compte", 11), new("Intitulé du compte", 30), new("Tiers", 20), new("Libellé ligne", 26),
            new("Débit", 15, FormatColonne.Montant), new("Crédit", 15, FormatColonne.Montant), new("Statut", 10),
        ]);

        foreach (var e in ecritures)
            foreach (var l in e.Lignes.Where(l => !l.IsDeleted).OrderByDescending(l => l.Debit))
                f.AjouterLigne(e.DateEcriture, e.Journal, e.Reference, e.Libelle, l.Compte?.Numero, l.Compte?.Libelle,
                               l.Tiers?.Nom, l.Libelle, l.Debit, l.Credit, e.Statut.ToString());

        f.AjouterTotalFormule("TOTAL", 9, 10);
        f.Terminer();

        return Fichier(xl, $"Journal_{(string.IsNullOrWhiteSpace(journal) ? "General" : journal.ToUpper())}_{exercice.Annee}");
    }

    /// <summary>Bilan actif / passif et compte de résultat SYSCOHADA (3 feuilles + contrôle d'équilibre).</summary>
    [HttpGet("etats-financiers")]
    [Authorize(Policy = Policies.LectureSeule)]
    public async Task<IActionResult> EtatsFinanciers([FromQuery] Guid? exerciceId, [FromQuery] string? format = null)
    {
        var exercice = await ExerciceAsync(exerciceId);
        var balance = (await _reporting.GetBalanceAsync(new FiltreBalanceQuery(exercice.Id))).Lignes;
        var periode = $"Exercice {exercice.Annee} (du {exercice.DateDebut:dd/MM/yyyy} au {exercice.DateFin:dd/MM/yyyy}){(exercice.EstCloture ? "" : " · exercice non clôturé")}";

        using var xl = await EditionAsync(format);

        // ── Bilan actif ──
        var actif = EtatsFinanciersSyscohada.Actif(balance);
        var fa = xl.AjouterFeuille("Bilan actif", "Bilan — Actif", periode,
        [
            new("Réf.", 6), new("Rubrique", 52),
            new("Brut", 16, FormatColonne.Montant), new("Amort. / Dépréc.", 16, FormatColonne.Montant), new("Net", 16, FormatColonne.Montant),
        ]);
        foreach (var (section, lignes) in actif)
        {
            fa.AjouterSection(section);
            foreach (var l in lignes) fa.AjouterLigne(l.Ref, l.Libelle, l.Brut, l.AmortDeprec, l.Net);
            fa.AjouterLigneTotal(null, $"Total {section.ToLower()}", lignes.Sum(x => x.Brut), lignes.Sum(x => x.AmortDeprec), lignes.Sum(x => x.Net));
        }
        var totalActif = actif.SelectMany(a => a.Lignes).Sum(l => l.Net);
        fa.AjouterLigneTotal("BZ", "TOTAL GÉNÉRAL ACTIF", actif.SelectMany(a => a.Lignes).Sum(l => l.Brut),
                             actif.SelectMany(a => a.Lignes).Sum(l => l.AmortDeprec), totalActif);
        fa.Terminer(filtres: false);

        // ── Bilan passif ──
        var passif = EtatsFinanciersSyscohada.Passif(balance);
        var fp = xl.AjouterFeuille("Bilan passif", "Bilan — Passif", periode,
        [
            new("Réf.", 6), new("Rubrique", 60), new("Net", 18, FormatColonne.Montant),
        ]);
        foreach (var (section, lignes) in passif)
        {
            fp.AjouterSection(section);
            foreach (var l in lignes) fp.AjouterLigne(l.Ref, l.Libelle, l.Montant);
            fp.AjouterLigneTotal(null, $"Total {section.ToLower()}", lignes.Sum(x => x.Montant));
        }
        var totalPassif = passif.SelectMany(p => p.Lignes).Sum(l => l.Montant);
        fp.AjouterLigneTotal("DZ", "TOTAL GÉNÉRAL PASSIF", totalPassif);

        var ecart = totalActif - totalPassif;
        fp.AjouterNote(ecart == 0
            ? "✓ Bilan équilibré : total actif = total passif."
            : $"⚠ Écart actif − passif : {ecart:N0}. Un ou plusieurs comptes ne sont rattachés à aucune rubrique : vérifiez la balance avec votre expert-comptable.");
        fp.Terminer(filtres: false);

        // ── Compte de résultat ──
        var cr = EtatsFinanciersSyscohada.CompteDeResultat(balance);
        var fr = xl.AjouterFeuille("Compte de résultat", "Compte de résultat", periode,
        [
            new("Réf.", 6), new("Libellé", 60), new("Montant", 18, FormatColonne.Montant),
        ]);
        foreach (var l in cr)
        {
            if (l.EstSolde) fr.AjouterLigneTotal(l.Ref, l.Libelle, l.Montant);
            else fr.AjouterLigne(l.Ref, l.Libelle, l.Montant);
        }
        fr.AjouterNote("Produits en positif, charges en négatif. Présentation SYSCOHADA révisé (système normal), calculée par préfixe de compte : à faire valider avant tout dépôt officiel.");
        fr.Terminer(filtres: false);

        return Fichier(xl, $"EtatsFinanciers_SYSCOHADA_{exercice.Annee}");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // COMMERCE
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Fin de journée : synthèse, tickets (détail), articles vendus, crédits clients.</summary>
    [HttpGet("fin-journee")]
    [Authorize(Policy = Policies.FinJournee)]
    public async Task<IActionResult> FinJournee([FromQuery] DateOnly? date, [FromQuery] Guid? magasinId, [FromQuery] string? format = null)
    {
        var jour = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var j = await _journalVentes.ObtenirJournalAsync(jour, magasinId);
        var sousTitre = $"{j.Magasin} · journée du {jour:dd/MM/yyyy}";

        using var xl = await EditionAsync(format);

        // Synthèse
        var fs = xl.AjouterFeuille("Synthèse", "Fin de journée — synthèse", sousTitre,
            [new("Indicateur", 34), new("Valeur", 18, FormatColonne.Montant), new("Détail", 40)]);
        fs.AjouterSection("ACTIVITÉ");
        fs.AjouterLigne("Chiffre d'affaires TTC", j.TotalTTC, $"HT {j.TotalHT:N0} · TVA {j.TotalTVA:N0}");
        fs.AjouterLigne("Nombre de tickets", (decimal)j.NbTickets, j.NbAnnules > 0 ? $"{j.NbAnnules} annulé(s)" : null);
        fs.AjouterLigne("Panier moyen", j.NbTickets == 0 ? 0m : Math.Round(j.TotalTTC / j.NbTickets, 0), null);
        fs.AjouterLigne("Remises accordées", j.TotalRemises, null);
        fs.AjouterLigne("Coût d'achat des ventes", j.CoutTotal, "au coût moyen pondéré");
        fs.AjouterLigne("Marge brute", j.MargeBrute, j.TotalHT == 0 ? null : $"{j.MargeBrute / j.TotalHT:P1} du CA HT");
        fs.AjouterSection("ENCAISSEMENTS PAR MODE");
        foreach (var e in j.Encaissements)
            fs.AjouterLigne(LibelleMode(e.Mode), e.Montant, $"{e.NbOperations} opération(s)");
        foreach (var s in j.Sessions)
        {
            fs.AjouterSection($"CONTRÔLE {s.Caisse.ToUpper()} — {(s.EstCloturee ? "clôturée" : "non clôturée")}");
            foreach (var l in s.Lignes)
                fs.AjouterLigne($"{LibelleMode(l.Mode)} — attendu", l.Theorique,
                    l.Declare is { } d ? $"déclaré {d:N0} · écart {l.Ecart:+#,##0;-#,##0;0}" : "non déclaré");
        }
        fs.Terminer(filtres: false);

        // Tickets (une ligne par article vendu)
        var ft = xl.AjouterFeuille("Tickets", "Détail des tickets", sousTitre,
        [
            new("Ticket", 18), new("Heure", 16, FormatColonne.DateHeure), new("Client", 22), new("Article", 34),
            new("Quantité", 10, FormatColonne.Quantite), new("Prix unitaire", 13, FormatColonne.Montant),
            new("Montant TTC", 14, FormatColonne.Montant), new("Paiement", 26), new("Statut", 10),
        ]);
        foreach (var t in j.Tickets)
        {
            var paiements = string.Join(" + ", t.Paiements.Select(p => $"{LibelleMode(p.Mode)} {p.Montant:N0}"));
            foreach (var l in t.Lignes)
                ft.AjouterLigne(t.Numero, t.DateVente.ToLocalTime(), t.Client, l.Libelle, l.Quantite, l.PrixUnitaireTTC,
                                t.EstAnnule ? 0m : l.MontantTTC, paiements, t.EstAnnule ? "Annulé" : "Validé");
        }
        ft.AjouterTotalFormule("TOTAL", 7);
        ft.Terminer();

        // Articles
        var fart = xl.AjouterFeuille("Articles vendus", "Articles vendus", sousTitre,
        [
            new("Article", 36), new("Quantité", 11, FormatColonne.Quantite), new("CA TTC", 14, FormatColonne.Montant),
            new("CA HT", 14, FormatColonne.Montant), new("Coût", 14, FormatColonne.Montant), new("Marge", 14, FormatColonne.Montant),
            new("Taux de marge", 12, FormatColonne.Pourcentage),
        ]);
        foreach (var a in j.Articles)
            fart.AjouterLigne(a.Libelle, a.Quantite, a.ChiffreAffairesTTC, a.ChiffreAffairesHT, a.Cout, a.Marge,
                              a.ChiffreAffairesHT == 0 ? null : (object)(double)(a.Marge / a.ChiffreAffairesHT));
        fart.AjouterTotalFormule("TOTAL", 3, 4, 5, 6);
        fart.Terminer();

        // Crédits
        var fc = xl.AjouterFeuille("Crédits clients", "Ventes à crédit du jour", sousTitre,
        [
            new("Client", 30), new("Téléphone", 16), new("Crédit du jour", 15, FormatColonne.Montant),
            new("Total dû", 15, FormatColonne.Montant), new("Plafond", 15, FormatColonne.Montant),
        ]);
        foreach (var c in j.Credits)
            fc.AjouterLigne(c.Client, c.Telephone, c.CreditDuJour, c.SoldeTotalDu, c.Plafond);
        fc.AjouterTotalFormule("TOTAL", 3);
        fc.Terminer();

        return Fichier(xl, $"FinDeJournee_{jour:yyyy-MM-dd}");
    }

    /// <summary>État du stock valorisé (coût moyen et prix de vente) d'un magasin.</summary>
    [HttpGet("stocks")]
    [Authorize(Policy = Policies.Commercial)]
    public async Task<IActionResult> Stocks([FromQuery] Guid? magasinId, [FromQuery] string? format = null)
    {
        var lignes = await _stock.GetStocksLotsAsync(magasinId);
        var produits = await _commerce.Produits.AsNoTracking()
            .Select(p => new { p.Id, p.Reference, p.PrixAchatMoyen, p.PrixVenteTTC, p.Unite, p.CategorieId })
            .ToDictionaryAsync(p => p.Id);
        var categories = await _commerce.Categories.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Libelle);
        var magasin = await NomMagasinAsync(magasinId);

        using var xl = await EditionAsync(format);
        var f = xl.AjouterFeuille("Stock", "État du stock valorisé", magasin,
        [
            new("Article", 32), new("Référence", 18), new("Catégorie", 18), new("Lot", 14), new("Péremption", 12, FormatColonne.Date),
            new("Quantité", 11, FormatColonne.Quantite), new("Unité", 8),
            new("Coût moyen", 13, FormatColonne.Montant), new("Valeur au coût", 15, FormatColonne.Montant),
            new("Prix de vente", 13, FormatColonne.Montant), new("Valeur de vente", 15, FormatColonne.Montant),
        ]);

        foreach (var l in lignes)
        {
            produits.TryGetValue(l.ProduitId, out var p);
            var categorie = p?.CategorieId is { } cid ? categories.GetValueOrDefault(cid) : null;
            var cout = Math.Round(p?.PrixAchatMoyen ?? 0, 2);
            var prix = p?.PrixVenteTTC ?? 0;

            f.AjouterLigne(l.DesignationProduit, p?.Reference, categorie,
                string.IsNullOrWhiteSpace(l.NumeroLot) ? null : l.NumeroLot, l.DateExpiration,
                l.QuantiteDisponible, LibelleUnite(p?.Unite), cout, Math.Round(l.QuantiteDisponible * cout, 0),
                prix, Math.Round(l.QuantiteDisponible * prix, 0));
        }

        f.AjouterTotalFormule("TOTAL", 9, 11);
        f.Terminer();

        return Fichier(xl, $"Stock_{DateTime.Today:yyyy-MM-dd}");
    }

    /// <summary>Feuille d'inventaire : théorique, compté, écart et valeur de l'écart.</summary>
    [HttpGet("inventaires/{inventaireId:guid}")]
    [Authorize(Policy = Policies.Commercial)]
    public async Task<IActionResult> Inventaire(Guid inventaireId, [FromQuery] string? format = null)
    {
        var inv = await _stock.GetInventaireDetailsAsync(inventaireId)
            ?? throw new KeyNotFoundException("Inventaire introuvable.");

        var couts = await _commerce.Produits.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.PrixAchatMoyen);

        using var xl = await EditionAsync(format);
        var f = xl.AjouterFeuille("Inventaire", $"Inventaire {inv.Numero}",
            $"{inv.Libelle} · ouvert le {inv.DateOuverture:dd/MM/yyyy} · {(inv.DateCloture is { } dc ? $"clôturé le {dc:dd/MM/yyyy}" : "en cours")}",
        [
            new("Article", 34), new("Lot", 14), new("Théorique", 12, FormatColonne.Quantite), new("Compté", 12, FormatColonne.Quantite),
            new("Écart", 12, FormatColonne.Quantite), new("Coût moyen", 13, FormatColonne.Montant), new("Valeur de l'écart", 15, FormatColonne.Montant),
        ]);

        foreach (var l in inv.Lignes)
        {
            var cout = Math.Round(couts.GetValueOrDefault(l.ProduitId), 2);
            f.AjouterLigne(l.ProduitLibelle, l.NumeroLot, l.QuantiteTheorique, l.QuantiteComptee, l.Ecart, cout,
                           l.Ecart is { } e ? Math.Round(e * cout, 0) : null);
        }

        f.AjouterTotalFormule("TOTAL", 7);
        f.AjouterNote("Écart négatif = manquant (perte, casse, vol) ; positif = excédent.");
        f.Terminer();

        return Fichier(xl, $"Inventaire_{inv.Numero}");
    }

    /// <summary>Fichier clients : coordonnées, crédit dû, points de fidélité.</summary>
    [HttpGet("clients")]
    [Authorize(Policy = Policies.Commercial)]
    public async Task<IActionResult> Clients([FromQuery] bool inclureInactifs = false, [FromQuery] string? format = null)
    {
        var query = _commerce.Clients.AsNoTracking();
        if (!inclureInactifs) query = query.Where(c => c.EstActif);
        var clients = await query.OrderBy(c => c.Nom).ToListAsync();

        using var xl = await EditionAsync(format);
        var f = xl.AjouterFeuille("Clients", "Fichier clients", $"{clients.Count} client(s)",
        [
            new("Code", 12), new("Nom", 30), new("Type", 12), new("Téléphone", 15), new("E-mail", 26), new("NIF", 16),
            new("Plafond crédit", 14, FormatColonne.Montant), new("Crédit dû", 14, FormatColonne.Montant),
            new("Points", 9, FormatColonne.Entier), new("Dernier achat", 12, FormatColonne.Date), new("Actif", 7),
        ]);

        foreach (var c in clients)
            f.AjouterLigne(c.Code, c.Nom, c.Type.ToString(), c.Telephone, c.Email, c.Nif,
                           c.PlafondCredit, c.SoldeCredit, c.PointsFidelite, c.DateDerniereVente, c.EstActif);

        f.AjouterTotalFormule("TOTAL", 8);
        f.Terminer();

        return Fichier(xl, $"Clients_{DateTime.Today:yyyy-MM-dd}");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // OUTILS
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Exercice demandé, sinon l'exercice de travail (ouvert de l'année, ou le plus récent ouvert).</summary>
    private async Task<ExerciceComptable> ExerciceAsync(Guid? exerciceId)
    {
        var exercice = exerciceId is { } id && id != Guid.Empty
            ? await _db.Exercices.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id)
            : await ExercicesController.ExerciceDeTravail(_db).FirstOrDefaultAsync()
              ?? await _db.Exercices.AsNoTracking().OrderByDescending(e => e.Annee).FirstOrDefaultAsync();

        return exercice ?? throw new KeyNotFoundException("Exercice comptable introuvable.");
    }

    /// <summary>Édition Excel (par défaut) ou PDF (?format=pdf), avec le nom et le logo de l'entreprise.</summary>
    private async Task<IEdition> EditionAsync(string? format)
    {
        var (nom, logo) = await LogoHelper.EnteteAsync(_db, _tenant.TenantId);
        return Editions.Creer(format, nom, logo);
    }

    private async Task<string> NomMagasinAsync(Guid? magasinId)
    {
        var query = _commerce.Magasins.AsNoTracking().Where(m => m.EstActif);
        var m = magasinId is { } id && id != Guid.Empty
            ? await query.FirstOrDefaultAsync(x => x.Id == id)
            : await query.OrderByDescending(x => x.EstPrincipal).FirstOrDefaultAsync();
        return m?.Nom ?? "";
    }

    private FileContentResult Fichier(IEdition edition, string nom)
    {
        var propre = new string(nom.Select(c => char.IsLetterOrDigit(c) || c is '_' or '-' ? c : '_').ToArray());
        return File(edition.Generer(), edition.TypeMime, $"{propre}.{edition.Extension}");
    }

    private static string Periode(ExerciceComptable e, DateTime? debut, DateTime? fin)
        => debut is null && fin is null
            ? $"Exercice {e.Annee} (du {e.DateDebut:dd/MM/yyyy} au {e.DateFin:dd/MM/yyyy})"
            : $"Exercice {e.Annee} · période du {(debut ?? e.DateDebut):dd/MM/yyyy} au {(fin ?? e.DateFin):dd/MM/yyyy}";

    private static string LibelleClasse(int classe) => classe switch
    {
        1 => "Comptes de ressources durables",
        2 => "Comptes d'actif immobilisé",
        3 => "Comptes de stocks",
        4 => "Comptes de tiers",
        5 => "Comptes de trésorerie",
        6 => "Comptes de charges des activités ordinaires",
        7 => "Comptes de produits des activités ordinaires",
        8 => "Comptes des autres charges et autres produits",
        9 => "Comptabilité analytique",
        _ => ""
    };

    private static string LibelleMode(ModePaiement m) => m switch
    {
        ModePaiement.Especes => "Espèces",
        ModePaiement.Carte => "Carte",
        ModePaiement.MobileMoney => "Mobile Money",
        ModePaiement.Credit => "Crédit client",
        ModePaiement.Points => "Points fidélité",
        _ => m.ToString()
    };

    private static string LibelleUnite(UniteMesure? u) => u switch
    {
        null => "",
        UniteMesure.Kilogramme => "kg",
        UniteMesure.Gramme => "g",
        UniteMesure.Litre => "L",
        UniteMesure.Metre => "m",
        UniteMesure.Heure => "h",
        _ => "unité"
    };
}
