using Finama.Commerce.Application.Common;
using Finama.Commerce.Core.Domain;
using Finama.Core.Entities;
using Finama.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Finama.API.Services;

public sealed record ResultatLiaison(bool Comptabilise, string Message, string? Reference = null);
public sealed record ResultatRattrapage(int Factures, int Reglements, int Caisses, IReadOnlyList<string> Erreurs);

public interface ILiaisonComptableService
{
    Task<ParametresComptables> ParametresAsync(Guid tenantId, CancellationToken ct = default);
    Task<ResultatLiaison> ComptabiliserFactureAsync(Guid factureId, Guid utilisateurId, CancellationToken ct = default);
    Task<ResultatLiaison> ComptabiliserReglementAsync(Guid reglementId, Guid utilisateurId, CancellationToken ct = default);
    Task<ResultatLiaison> ComptabiliserCaisseAsync(Guid sessionId, Guid utilisateurId, CancellationToken ct = default);
    Task<ResultatRattrapage> RattraperAsync(Guid tenantId, Guid utilisateurId, DateTime? depuis, CancellationToken ct = default);
    Task<(Guid? Id, string Affichage)> VerifierCompteAsync(Guid tenantId, string numero, CancellationToken ct = default);
}

/// <summary>
/// Liaison automatique modules → comptabilité (SYSCOHADA), active seulement si l'entreprise l'a activée :
///  • facture de vente  → journal VT : 411 client au débit (TTC) / 70x ventes (HT) + 443 TVA collectée au crédit
///  • facture d'achat   → journal AC : 60x achats (HT) + 445 TVA déductible au débit / 401 fournisseur au crédit
///  • règlement         → journal BQ ou CA : trésorerie / tiers (sens selon vente ou achat)
///  • clôture de caisse → journal CA : espèces, carte, Mobile Money, crédit client, points / ventes HT + TVA
/// Une pièce n'est jamais comptabilisée deux fois (table PiecesComptabilisees) ; les avoirs sont passés en sens inverse.
/// </summary>
public class LiaisonComptableService : ILiaisonComptableService
{
    private readonly AppDbContext _db;
    private readonly ICommerceDbContext _commerce;
    private readonly ILogger<LiaisonComptableService> _log;

    public LiaisonComptableService(AppDbContext db, ICommerceDbContext commerce, ILogger<LiaisonComptableService> log)
    {
        _db = db;
        _commerce = commerce;
        _log = log;
    }

    private sealed record Ligne(Guid CompteId, Guid? TiersId, string? Libelle, decimal Debit, decimal Credit);

    // ───────────────────────────── Paramètres ─────────────────────────────

    public async Task<ParametresComptables> ParametresAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.Set<ParametresComptables>().IgnoreQueryFilters()
               .FirstOrDefaultAsync(p => p.TenantId == tenantId && !p.IsDeleted, ct)
           ?? new ParametresComptables { TenantId = tenantId };

    public async Task<(Guid? Id, string Affichage)> VerifierCompteAsync(Guid tenantId, string numero, CancellationToken ct = default)
    {
        var c = await TrouverCompteAsync(tenantId, numero, ct);
        return c is null ? (null, "introuvable") : (c.Id, $"{c.Numero} {c.Libelle}");
    }

    private async Task<CompteComptable?> TrouverCompteAsync(Guid tenantId, string numero, CancellationToken ct)
    {
        numero = (numero ?? "").Trim();
        if (numero.Length == 0) return null;
        var comptes = _db.CompteComptables.IgnoreQueryFilters().Where(c => c.TenantId == tenantId && !c.IsDeleted && c.EstActif);
        var exact = await comptes.FirstOrDefaultAsync(c => c.Numero == numero, ct);
        if (exact is not null) return exact;
        var prefixe = numero.TrimEnd('0');
        if (prefixe.Length < 2) return null;
        return await comptes.Where(c => c.Numero.StartsWith(prefixe))
            .OrderBy(c => c.Numero.Length).ThenBy(c => c.Numero).FirstOrDefaultAsync(ct);
    }

    private async Task<Guid> CompteAsync(Guid tenantId, string numero, string role, CancellationToken ct)
        => (await TrouverCompteAsync(tenantId, numero, ct))?.Id
           ?? throw new InvalidOperationException($"Compte « {role} » ({numero}) introuvable dans le plan comptable : créez-le ou changez-le dans Paramètres → Liaison comptable.");

    // ───────────────────────────── Factures ─────────────────────────────

    public async Task<ResultatLiaison> ComptabiliserFactureAsync(Guid factureId, Guid utilisateurId, CancellationToken ct = default)
    {
        var f = await _db.Factures.IgnoreQueryFilters().Include(x => x.Lignes).Include(x => x.Tiers)
            .FirstOrDefaultAsync(x => x.Id == factureId && !x.IsDeleted, ct);
        if (f?.TenantId is not { } tenantId) return new(false, "Facture introuvable.");
        var p = await ParametresAsync(tenantId, ct);
        if (!p.LiaisonActive) return new(false, "Liaison comptable désactivée.");
        if (f.Statut is StatutFacture.Brouillon or StatutFacture.Annulee) return new(false, "Facture en brouillon ou annulée : non comptabilisée.");
        if (await DejaFaitAsync(tenantId, "FACTURE", f.Id, ct)) return new(false, "Facture déjà comptabilisée.");

        var vente = f.Type is TypeFacture.Vente or TypeFacture.AvoirVente;
        var avoir = f.Type is TypeFacture.AvoirVente or TypeFacture.AvoirAchat;
        var compteTiers = f.Tiers.CompteComptableId
                          ?? await CompteAsync(tenantId, vente ? p.CompteClients : p.CompteFournisseurs, vente ? "Clients" : "Fournisseurs", ct);
        var compteDefaut = await CompteAsync(tenantId, vente ? p.CompteVentes : p.CompteAchats, vente ? "Ventes" : "Achats", ct);
        var compteTva = await CompteAsync(tenantId, vente ? p.CompteTvaCollectee : p.CompteTvaDeductible, vente ? "TVA collectée" : "TVA déductible", ct);

        var lignes = new List<Ligne>();
        var ht = f.Lignes.Where(l => !l.IsDeleted)
            .GroupBy(l => l.CompteProduitsId ?? compteDefaut)
            .Select(g => (Compte: g.Key, Montant: Math.Round(g.Sum(l => l.MontantHT), 2)))
            .Where(x => x.Montant != 0).ToList();
        var ttc = Math.Round(f.TotalTTC, 2);
        var tva = ttc - ht.Sum(x => x.Montant);     // l'arrondi va sur la TVA : l'écriture tombe toujours juste
        var libelle = $"Facture {f.Numero} — {f.Tiers.Nom}";

        if (vente)
        {
            lignes.Add(new(compteTiers, f.TiersId, libelle, ttc, 0));
            lignes.AddRange(ht.Select(x => new Ligne(x.Compte, null, libelle, 0, x.Montant)));
            if (tva != 0) lignes.Add(new(compteTva, null, $"TVA {f.Numero}", 0, tva));
        }
        else
        {
            lignes.AddRange(ht.Select(x => new Ligne(x.Compte, null, libelle, x.Montant, 0)));
            if (tva != 0) lignes.Add(new(compteTva, null, $"TVA {f.Numero}", tva, 0));
            lignes.Add(new(compteTiers, f.TiersId, libelle, 0, ttc));
        }
        if (avoir) lignes = lignes.Select(l => l with { Debit = l.Credit, Credit = l.Debit }).ToList();

        var reference = await EnregistrerAsync(tenantId, f.DateFacture, vente ? "VT" : "AC", libelle, lignes, f.Id, "FACTURE", f.Id, utilisateurId, p.ValiderAutomatiquement, ct);
        return new(true, $"Écriture {reference} passée.", reference);
    }

    // ───────────────────────────── Règlements ─────────────────────────────

    public async Task<ResultatLiaison> ComptabiliserReglementAsync(Guid reglementId, Guid utilisateurId, CancellationToken ct = default)
    {
        var r = await _db.Set<ReglementFacture>().IgnoreQueryFilters()
            .Include(x => x.Facture).ThenInclude(f => f.Tiers)
            .FirstOrDefaultAsync(x => x.Id == reglementId && !x.IsDeleted, ct);
        if (r?.TenantId is not { } tenantId) return new(false, "Règlement introuvable.");
        var p = await ParametresAsync(tenantId, ct);
        if (!p.LiaisonActive) return new(false, "Liaison comptable désactivée.");
        if (await DejaFaitAsync(tenantId, "REGLEMENT", r.Id, ct)) return new(false, "Règlement déjà comptabilisé.");

        var f = r.Facture;
        var vente = f.Type is TypeFacture.Vente or TypeFacture.AvoirVente;
        var compteTiers = f.Tiers.CompteComptableId
                          ?? await CompteAsync(tenantId, vente ? p.CompteClients : p.CompteFournisseurs, vente ? "Clients" : "Fournisseurs", ct);
        var (numeroTreso, role, journal) = r.Mode switch
        {
            ModeReglement.Caisse => (p.CompteCaisse, "Caisse", "CA"),
            ModeReglement.MobileMoney => (p.CompteMobileMoney, "Mobile Money", "BQ"),
            _ => (p.CompteBanque, "Banque", "BQ")
        };
        var compteTreso = await CompteAsync(tenantId, numeroTreso, role, ct);
        var libelle = $"Règlement facture {f.Numero} — {f.Tiers.Nom}{(string.IsNullOrWhiteSpace(r.Reference) ? "" : $" ({r.Reference})")}";
        var m = Math.Round(r.Montant, 2);

        var lignes = vente
            ? new List<Ligne> { new(compteTreso, null, libelle, m, 0), new(compteTiers, f.TiersId, libelle, 0, m) }
            : new List<Ligne> { new(compteTiers, f.TiersId, libelle, m, 0), new(compteTreso, null, libelle, 0, m) };

        var reference = await EnregistrerAsync(tenantId, r.DateReglement, journal, libelle, lignes, f.Id, "REGLEMENT", r.Id, utilisateurId, p.ValiderAutomatiquement, ct,
            ecritureId => r.EcritureId = ecritureId);
        return new(true, $"Écriture {reference} passée.", reference);
    }

    // ───────────────────────────── Clôture de caisse ─────────────────────────────

    public async Task<ResultatLiaison> ComptabiliserCaisseAsync(Guid sessionId, Guid utilisateurId, CancellationToken ct = default)
    {
        var session = await _commerce.Sessions.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null) return new(false, "Session de caisse introuvable.");
        var tenantId = session.TenantId!.Value;
        var p = await ParametresAsync(tenantId, ct);
        if (!p.LiaisonActive) return new(false, "Liaison comptable désactivée.");
        if (session.Statut != StatutSession.Cloturee) return new(false, "La caisse n'est pas encore clôturée.");
        if (await DejaFaitAsync(tenantId, "CAISSE", session.Id, ct)) return new(false, "Caisse déjà comptabilisée.");

        var ventes = await _commerce.Ventes.IgnoreQueryFilters().AsNoTracking().Include(v => v.Paiements)
            .Where(v => v.SessionCaisseId == session.Id && v.Statut == StatutVente.Validee).ToListAsync(ct);
        if (ventes.Count == 0) return new(false, "Aucune vente dans cette caisse : rien à comptabiliser.");

        var paiements = ventes.SelectMany(v => v.Paiements).GroupBy(x => x.Mode).ToDictionary(g => g.Key, g => Math.Round(g.Sum(x => x.Montant), 2));
        var totalHT = Math.Round(ventes.Sum(v => v.TotalHT), 2);
        var totalEncaisse = paiements.Values.Sum();
        var tva = totalEncaisse - totalHT;
        var caisseLibelle = await _commerce.Caisses.IgnoreQueryFilters().Where(c => c.Id == session.CaisseId).Select(c => c.Libelle).FirstOrDefaultAsync(ct) ?? "Caisse";
        var date = session.DateCloture ?? DateTime.UtcNow;
        var libelle = $"Ventes {caisseLibelle} du {date.ToLocalTime():dd/MM/yyyy} ({ventes.Count} tickets)";

        var lignes = new List<Ligne>();
        async Task Debit(ModePaiement mode, string numero, string role)
        {
            if (paiements.TryGetValue(mode, out var montant) && montant != 0)
                lignes.Add(new(await CompteAsync(tenantId, numero, role, ct), null, $"{libelle} — {role}", montant, 0));
        }
        await Debit(ModePaiement.Especes, p.CompteCaisse, "Espèces");
        await Debit(ModePaiement.Carte, p.CompteBanque, "Carte");
        await Debit(ModePaiement.MobileMoney, p.CompteMobileMoney, "Mobile Money");
        await Debit(ModePaiement.Credit, p.CompteClientsCaisse, "Crédit clients");
        await Debit(ModePaiement.Points, p.CompteRemisesFidelite, "Points de fidélité");
        lignes.Add(new(await CompteAsync(tenantId, p.CompteVentes, "Ventes", ct), null, libelle, 0, totalHT));
        if (tva != 0) lignes.Add(new(await CompteAsync(tenantId, p.CompteTvaCollectee, "TVA collectée", ct), null, $"TVA {libelle}", 0, tva));

        var reference = await EnregistrerAsync(tenantId, date, "CA", libelle, lignes, null, "CAISSE", session.Id, utilisateurId, p.ValiderAutomatiquement, ct);
        return new(true, $"Écriture {reference} passée.", reference);
    }

    // ───────────────────────────── Rattrapage ─────────────────────────────

    public async Task<ResultatRattrapage> RattraperAsync(Guid tenantId, Guid utilisateurId, DateTime? depuis, CancellationToken ct = default)
    {
        var erreurs = new List<string>();
        int nbF = 0, nbR = 0, nbC = 0;
        var faites = _db.Set<PieceComptabilisee>().IgnoreQueryFilters().Where(x => x.TenantId == tenantId);

        var factures = await _db.Factures.IgnoreQueryFilters()
            .Where(f => f.TenantId == tenantId && !f.IsDeleted && f.Statut != StatutFacture.Brouillon && f.Statut != StatutFacture.Annulee
                        && (depuis == null || f.DateFacture >= depuis)
                        && !faites.Any(x => x.Source == "FACTURE" && x.SourceId == f.Id))
            .OrderBy(f => f.DateFacture).Select(f => new { f.Id, f.Numero }).ToListAsync(ct);
        foreach (var f in factures)
            try { if ((await ComptabiliserFactureAsync(f.Id, utilisateurId, ct)).Comptabilise) nbF++; }
            catch (Exception ex) { erreurs.Add($"Facture {f.Numero} : {ex.Message}"); _db.ChangeTracker.Clear(); }

        var reglements = await _db.Set<ReglementFacture>().IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId && !r.IsDeleted && (depuis == null || r.DateReglement >= depuis)
                        && !faites.Any(x => x.Source == "REGLEMENT" && x.SourceId == r.Id))
            .OrderBy(r => r.DateReglement).Select(r => r.Id).ToListAsync(ct);
        foreach (var id in reglements)
            try { if ((await ComptabiliserReglementAsync(id, utilisateurId, ct)).Comptabilise) nbR++; }
            catch (Exception ex) { erreurs.Add($"Règlement : {ex.Message}"); _db.ChangeTracker.Clear(); }

        var dejaCaisse = await faites.Where(x => x.Source == "CAISSE").Select(x => x.SourceId).ToListAsync(ct);
        var sessions = await _commerce.Sessions.IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId && s.Statut == StatutSession.Cloturee && (depuis == null || s.DateCloture >= depuis))
            .OrderBy(s => s.DateCloture).Select(s => s.Id).ToListAsync(ct);
        foreach (var id in sessions.Where(s => !dejaCaisse.Contains(s)))
            try { if ((await ComptabiliserCaisseAsync(id, utilisateurId, ct)).Comptabilise) nbC++; }
            catch (Exception ex) { erreurs.Add($"Caisse : {ex.Message}"); _db.ChangeTracker.Clear(); }

        return new(nbF, nbR, nbC, erreurs);
    }

    // ───────────────────────────── Outils ─────────────────────────────

    private Task<bool> DejaFaitAsync(Guid tenantId, string source, Guid sourceId, CancellationToken ct)
        => _db.Set<PieceComptabilisee>().IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId && x.Source == source && x.SourceId == sourceId, ct);

    /// <summary>Crée l'écriture (numérotée par journal et par année, validée ou en brouillon) et la trace de la pièce, en une transaction.</summary>
    private async Task<string> EnregistrerAsync(Guid tenantId, DateTime date, string journal, string libelle, List<Ligne> lignes,
        Guid? factureId, string source, Guid sourceId, Guid utilisateurId, bool valider, CancellationToken ct, Action<Guid>? lier = null)
    {
        lignes = lignes.Where(l => l.Debit != 0 || l.Credit != 0).ToList();
        var debit = lignes.Sum(l => l.Debit);
        var credit = lignes.Sum(l => l.Credit);
        if (debit != credit || debit == 0)
            throw new InvalidOperationException($"Écriture automatique déséquilibrée ({debit:N2} / {credit:N2}) : non enregistrée.");

        var exercice = await _db.Exercices.IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && !e.IsDeleted && !e.EstCloture
                                      && e.DateDebut.Date <= date.Date && e.DateFin.Date >= date.Date, ct)
            ?? throw new InvalidOperationException($"Aucun exercice ouvert au {date:dd/MM/yyyy} : ouvrez l'exercice puis relancez la comptabilisation.");
        var devise = await _db.Tenants.IgnoreQueryFilters().Where(t => t.Id == tenantId).Select(t => t.DeviseBase).FirstOrDefaultAsync(ct);
        devise = string.IsNullOrWhiteSpace(devise) ? "XOF" : devise.ToUpperInvariant();

        var strategie = _db.Database.CreateExecutionStrategy();
        return await strategie.ExecuteAsync(async () =>
        {
            await using var tx = _db.Database.CurrentTransaction is null ? await _db.Database.BeginTransactionAsync(ct) : null;
            var numero = await _db.ProchainNumeroAsync(tenantId, $"ECR-{journal}", exercice.Annee);
            var ecriture = new EcritureComptable
            {
                TenantId = tenantId,
                Reference = $"{journal}-{exercice.Annee}-{numero:D6}",
                DateEcriture = date.Date,
                Libelle = libelle.Length > 250 ? libelle[..250] : libelle,
                Journal = journal,
                Statut = valider ? StatutEcriture.Validee : StatutEcriture.Brouillon,
                ExerciceId = exercice.Id,
                FactureId = factureId,
                UtilisateurId = utilisateurId,
                Lignes = lignes.Select(l => new LigneEcriture
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, CompteId = l.CompteId, TiersId = l.TiersId,
                    Libelle = l.Libelle is { Length: > 250 } x ? x[..250] : l.Libelle,
                    Debit = l.Debit, Credit = l.Credit, Devise = devise, MontantDeviseBase = l.Debit > 0 ? l.Debit : l.Credit
                }).ToList()
            };
            _db.Ecritures.Add(ecriture);
            _db.Set<PieceComptabilisee>().Add(new PieceComptabilisee { TenantId = tenantId, Source = source, SourceId = sourceId, EcritureId = ecriture.Id });
            lier?.Invoke(ecriture.Id);
            await _db.SaveChangesAsync(ct);
            if (tx is not null) await tx.CommitAsync(ct);
            _log.LogInformation("Liaison comptable : {Source} {SourceId} → {Reference}", source, sourceId, ecriture.Reference);
            return ecriture.Reference;
        });
    }
}
