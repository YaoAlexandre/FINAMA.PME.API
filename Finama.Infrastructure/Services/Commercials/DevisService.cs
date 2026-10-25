using Finama.Core.DTOs;
using Finama.Core.Entities;
using Finama.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Finama.Infrastructure.Services.Commercials;

public interface IDevisService
{
    Task<List<DevisResumeDto>> ListerAsync(ContexteVendeur ctx, StatutDevis? statut, string? recherche);
    Task<DevisDto> ObtenirAsync(Guid id, ContexteVendeur ctx);
    Task<DevisDto> CreerAsync(CreerDevisRequest request, ContexteVendeur ctx);
    Task<DevisDto> ModifierAsync(Guid id, CreerDevisRequest request, ContexteVendeur ctx);
    Task<DevisDto> EnvoyerAsync(Guid id, ContexteVendeur ctx);
    Task<DevisDto> AccepterAsync(Guid id, ContexteVendeur ctx);
    Task<DevisDto> RefuserAsync(Guid id, string? motif, ContexteVendeur ctx);
    Task<DevisDto> RemettreEnBrouillonAsync(Guid id, ContexteVendeur ctx);
    Task<DevisDto> DupliquerAsync(Guid id, ContexteVendeur ctx);
    Task<ConversionFactureDto> ConvertirEnFactureAsync(Guid id, ContexteVendeur ctx);
    Task SupprimerAsync(Guid id, ContexteVendeur ctx);
    Task<VentesTableauBordDto> TableauBordAsync(ContexteVendeur ctx);
}

/// <summary>
/// Règles métier des devis :
///  • cycle contrôlé (aucun retour possible d'un devis facturé : pas de double facture) ;
///  • expiration automatique des devis envoyés dont la date de validité est passée ;
///  • numérotation continue par entreprise (DEV-2026-0001), protégée contre les doublons ;
///  • conversion en facture de vente numérotée comme les autres (FA-2026-000001), liée au devis.
/// </summary>
public class DevisService : IDevisService
{
    private const int ValiditeParDefautJours = 30;
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;

    public DevisService(AppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CONSULTATION
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<List<DevisResumeDto>> ListerAsync(ContexteVendeur ctx, StatutDevis? statut, string? recherche)
    {
        await ExpirerDevisDepassesAsync();

        var query = Visibles(ctx).AsNoTracking();
        if (statut.HasValue) query = query.Where(d => d.Statut == statut.Value);
        if (!string.IsNullOrWhiteSpace(recherche))
        {
            var r = recherche.Trim();
            query = query.Where(d => d.Numero.Contains(r) || d.Libelle.Contains(r) || d.Tiers.Nom.Contains(r));
        }

        var devis = await query
            .Include(d => d.Tiers)
            .Include(d => d.Lignes)
            .OrderByDescending(d => d.DateCreation)
            .Take(500)
            .AsSplitQuery()
            .ToListAsync();

        var noms = await NomsUtilisateursAsync(devis.Select(d => d.CreePar));
        return devis.Select(d => Resume(d, noms)).ToList();
    }

    public async Task<DevisDto> ObtenirAsync(Guid id, ContexteVendeur ctx)
    {
        await ExpirerDevisDepassesAsync();
        var devis = await ChargerAsync(id, ctx, suivi: false);
        var noms = await NomsUtilisateursAsync([devis.CreePar]);
        return Dto(devis, noms);
    }

    public async Task<VentesTableauBordDto> TableauBordAsync(ContexteVendeur ctx)
    {
        await ExpirerDevisDepassesAsync();

        var devis = await Visibles(ctx).AsNoTracking()
            .Include(d => d.Tiers)
            .Include(d => d.Lignes)
            .Where(d => d.DateCreation >= DateTime.UtcNow.AddMonths(-12) || d.Statut == StatutDevis.Envoye || d.Statut == StatutDevis.Brouillon)
            .AsSplitQuery()
            .ToListAsync();

        var noms = await NomsUtilisateursAsync(devis.Select(d => d.CreePar));
        var aujourdHui = DateTime.UtcNow.Date;
        var debutMois = new DateTime(aujourdHui.Year, aujourdHui.Month, 1);

        var envoyes = devis.Where(d => d.Statut == StatutDevis.Envoye).ToList();
        var acceptes = devis.Where(d => d.Statut == StatutDevis.Accepte).ToList();
        var decides = devis.Count(d => d.Statut is StatutDevis.Accepte or StatutDevis.Converti or StatutDevis.Refuse or StatutDevis.Expire);
        var gagnes = devis.Count(d => d.Statut is StatutDevis.Accepte or StatutDevis.Converti);
        var aExpirer = envoyes
            .Where(d => d.DateExpiration.HasValue && d.DateExpiration.Value.Date <= aujourdHui.AddDays(7))
            .OrderBy(d => d.DateExpiration)
            .ToList();

        return new VentesTableauBordDto(
            NbBrouillons: devis.Count(d => d.Statut == StatutDevis.Brouillon),
            NbEnvoyes: envoyes.Count,
            MontantEnAttente: envoyes.Sum(d => d.TotalTTC),
            NbAcceptesNonFactures: acceptes.Count,
            MontantAcceptesNonFactures: acceptes.Sum(d => d.TotalTTC),
            TauxTransformation: decides == 0 ? 0 : Math.Round((decimal)gagnes / decides, 3),
            CaDevisFacturesMois: devis.Where(d => d.Statut == StatutDevis.Converti && d.DateDecision >= debutMois).Sum(d => d.TotalHT),
            NbExpirentSous7Jours: aExpirer.Count,
            AExpirer: aExpirer.Take(5).Select(d => Resume(d, noms)).ToList(),
            Derniers: devis.OrderByDescending(d => d.DateCreation).Take(5).Select(d => Resume(d, noms)).ToList());
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CRÉATION / MODIFICATION
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<DevisDto> CreerAsync(CreerDevisRequest request, ContexteVendeur ctx)
    {
        await ValiderAsync(request);
        var tenantId = TenantId();

        await using var tx = await _db.Database.BeginTransactionAsync();

        var devis = new Devis
        {
            TenantId = tenantId,
            Numero = await ProchainNumeroDevisAsync(tenantId),
            CreePar = ctx.UtilisateurId,
            Statut = StatutDevis.Brouillon,
            DateCreation = DateTime.UtcNow,
        };
        Appliquer(devis, request);

        _db.Devis.Add(devis);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return await ObtenirAsync(devis.Id, ctx);
    }

    public async Task<DevisDto> ModifierAsync(Guid id, CreerDevisRequest request, ContexteVendeur ctx)
    {
        await ValiderAsync(request);
        var devis = await ChargerAsync(id, ctx, suivi: true);

        if (devis.Statut != StatutDevis.Brouillon)
            throw new InvalidOperationException("Seul un brouillon peut être modifié. Remettez le devis en brouillon, ou créez une nouvelle version.");

        _db.LignesDevis.RemoveRange(devis.Lignes);
        devis.Lignes.Clear();
        Appliquer(devis, request);

        await _db.SaveChangesAsync();
        return await ObtenirAsync(id, ctx);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CYCLE DE VIE
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<DevisDto> EnvoyerAsync(Guid id, ContexteVendeur ctx)
    {
        var devis = await ChargerAsync(id, ctx, suivi: true);
        Exiger(devis, "envoyer", StatutDevis.Brouillon);

        if (!devis.Lignes.Any(l => !l.IsDeleted))
            throw new InvalidOperationException("Ajoutez au moins une ligne avant d'envoyer le devis.");

        devis.Statut = StatutDevis.Envoye;
        devis.DateEnvoi = DateTime.UtcNow;
        devis.DateExpiration ??= DateTime.UtcNow.Date.AddDays(ValiditeParDefautJours);
        if (devis.DateExpiration.Value.Date < DateTime.UtcNow.Date)
            throw new InvalidOperationException("La date de validité est déjà passée : modifiez-la avant d'envoyer le devis.");

        await _db.SaveChangesAsync();
        return await ObtenirAsync(id, ctx);
    }

    public async Task<DevisDto> AccepterAsync(Guid id, ContexteVendeur ctx)
    {
        var devis = await ChargerAsync(id, ctx, suivi: true);
        Exiger(devis, "accepter", StatutDevis.Envoye, StatutDevis.Expire);

        devis.Statut = StatutDevis.Accepte;
        devis.DateDecision = DateTime.UtcNow;
        devis.MotifRefus = null;
        await _db.SaveChangesAsync();
        return await ObtenirAsync(id, ctx);
    }

    public async Task<DevisDto> RefuserAsync(Guid id, string? motif, ContexteVendeur ctx)
    {
        var devis = await ChargerAsync(id, ctx, suivi: true);
        Exiger(devis, "marquer comme refusé", StatutDevis.Envoye, StatutDevis.Expire, StatutDevis.Accepte);

        devis.Statut = StatutDevis.Refuse;
        devis.DateDecision = DateTime.UtcNow;
        devis.MotifRefus = string.IsNullOrWhiteSpace(motif) ? null : motif.Trim()[..Math.Min(motif.Trim().Length, 500)];
        await _db.SaveChangesAsync();
        return await ObtenirAsync(id, ctx);
    }

    public async Task<DevisDto> RemettreEnBrouillonAsync(Guid id, ContexteVendeur ctx)
    {
        var devis = await ChargerAsync(id, ctx, suivi: true);
        Exiger(devis, "remettre en brouillon", StatutDevis.Envoye, StatutDevis.Refuse, StatutDevis.Expire);

        devis.Statut = StatutDevis.Brouillon;
        devis.DateEnvoi = null;
        devis.DateDecision = null;
        devis.MotifRefus = null;
        await _db.SaveChangesAsync();
        return await ObtenirAsync(id, ctx);
    }

    /// <summary>Copie en nouveau brouillon (nouveau numéro). Version + 1 si on duplique pour retravailler une offre.</summary>
    public async Task<DevisDto> DupliquerAsync(Guid id, ContexteVendeur ctx)
    {
        var source = await ChargerAsync(id, ctx, suivi: false);
        var tenantId = source.TenantId!.Value;

        await using var tx = await _db.Database.BeginTransactionAsync();

        var copie = new Devis
        {
            TenantId = tenantId,
            Numero = await ProchainNumeroDevisAsync(tenantId),
            Libelle = source.Libelle,
            TiersId = source.TiersId,
            CreePar = ctx.UtilisateurId,
            Notes = source.Notes,
            Conditions = source.Conditions,
            RemiseGlobalePourcent = source.RemiseGlobalePourcent,
            DateCreation = DateTime.UtcNow,
            DateExpiration = DateTime.UtcNow.Date.AddDays(ValiditeParDefautJours),
            Statut = StatutDevis.Brouillon,
            DevisOrigineId = source.Id,
            Version = source.Version + 1,
            Lignes = source.Lignes.Where(l => !l.IsDeleted).OrderBy(l => l.Ordre).Select(l => new LigneDevis
            {
                Ordre = l.Ordre,
                ProduitId = l.ProduitId,
                Designation = l.Designation,
                Quantite = l.Quantite,
                PrixUnitaireHT = l.PrixUnitaireHT,
                RemisePourcent = l.RemisePourcent,
                TauxTVA = l.TauxTVA
            }).ToList()
        };

        _db.Devis.Add(copie);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return await ObtenirAsync(copie.Id, ctx);
    }

    /// <summary>
    /// Crée la facture de vente (brouillon) à partir d'un devis accepté. Une seule fois : le devis passe « Facturé ».
    /// Les remises sont intégrées au prix unitaire de chaque ligne de facture.
    /// </summary>
    public async Task<ConversionFactureDto> ConvertirEnFactureAsync(Guid id, ContexteVendeur ctx)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();

        var devis = await ChargerAsync(id, ctx, suivi: true);
        if (devis.Statut == StatutDevis.Converti)
            throw new InvalidOperationException($"Ce devis a déjà été facturé ({devis.FactureNumero}).");
        Exiger(devis, "facturer", StatutDevis.Accepte);

        var tenantId = devis.TenantId!.Value;
        var devise = await _db.Tenants.Where(t => t.Id == tenantId).Select(t => t.DeviseBase).FirstOrDefaultAsync() ?? "XOF";
        var aujourdHui = DateTime.UtcNow.Date;
        var coefGlobal = 1 - devis.RemiseGlobalePourcent / 100m;

        var lignes = devis.Lignes.Where(l => !l.IsDeleted).OrderBy(l => l.Ordre).Select(l =>
        {
            var remises = new List<string>();
            if (l.RemisePourcent > 0) remises.Add($"remise {l.RemisePourcent:0.##} %");
            if (devis.RemiseGlobalePourcent > 0) remises.Add($"remise globale {devis.RemiseGlobalePourcent:0.##} %");

            return new LigneFacture
            {
                TenantId = tenantId,
                Description = remises.Count == 0 ? l.Designation : $"{l.Designation} ({string.Join(", ", remises)})",
                Quantite = l.Quantite,
                PrixUnitaireHT = Math.Round(l.PrixUnitaireHT * (1 - l.RemisePourcent / 100m) * coefGlobal, 4),
                TauxTVA = l.TauxTVA
            };
        }).ToList();

        var facture = new Facture
        {
            TenantId = tenantId,
            Numero = await ProchainNumeroFactureAsync(tenantId, aujourdHui.Year),
            Type = TypeFacture.Vente,
            Statut = StatutFacture.Brouillon,
            TiersId = devis.TiersId,
            DateFacture = aujourdHui,
            DateEcheance = aujourdHui.AddDays(30),
            Devise = devise,
            Notes = $"Facture établie d'après le devis {devis.Numero}.",
            DevisId = devis.Id,
            Lignes = lignes,
            TotalHT = lignes.Sum(l => Math.Round(l.Quantite * l.PrixUnitaireHT, 2)),
            TotalTVA = lignes.Sum(l => Math.Round(Math.Round(l.Quantite * l.PrixUnitaireHT, 2) * l.TauxTVA / 100m, 2)),
        };
        facture.TotalTTC = facture.TotalHT + facture.TotalTVA;

        _db.Factures.Add(facture);
        devis.Statut = StatutDevis.Converti;
        devis.FactureId = facture.Id;
        devis.FactureNumero = facture.Numero;

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return new ConversionFactureDto(facture.Id, facture.Numero);
    }

    /// <summary>Suppression (corbeille) des seuls brouillons ; un devis envoyé reste tracé.</summary>
    public async Task SupprimerAsync(Guid id, ContexteVendeur ctx)
    {
        var devis = await ChargerAsync(id, ctx, suivi: true);
        if (devis.Statut != StatutDevis.Brouillon)
            throw new InvalidOperationException("Seul un brouillon peut être supprimé. Un devis envoyé peut être marqué « Refusé ».");

        devis.IsDeleted = true;
        foreach (var l in devis.Lignes) l.IsDeleted = true;
        await _db.SaveChangesAsync();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // OUTILS
    // ═══════════════════════════════════════════════════════════════════════

    private IQueryable<Devis> Visibles(ContexteVendeur ctx)
        => ctx.VoitTout ? _db.Devis : _db.Devis.Where(d => d.CreePar == ctx.UtilisateurId);

    private async Task<Devis> ChargerAsync(Guid id, ContexteVendeur ctx, bool suivi)
    {
        var query = Visibles(ctx).Include(d => d.Tiers).Include(d => d.Lignes).AsSplitQuery();
        if (!suivi) query = query.AsNoTracking();
        return await query.FirstOrDefaultAsync(d => d.Id == id)
               ?? throw new KeyNotFoundException("Devis introuvable.");
    }

    /// <summary>Envoyés dont la validité est dépassée → Expirés (requête unique, filtrée par entreprise).</summary>
    private Task ExpirerDevisDepassesAsync()
    {
        var aujourdHui = DateTime.UtcNow.Date;
        return _db.Devis
            .Where(d => d.Statut == StatutDevis.Envoye && d.DateExpiration != null && d.DateExpiration < aujourdHui)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.Statut, StatutDevis.Expire));
    }

    private static void Exiger(Devis devis, string action, params StatutDevis[] autorises)
    {
        if (!autorises.Contains(devis.Statut))
            throw new InvalidOperationException(
                $"Impossible de {action} un devis « {StatutsDevis.Libelle(devis.Statut)} ».");
    }

    private async Task ValiderAsync(CreerDevisRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Libelle))
            throw new ArgumentException("L'objet du devis est obligatoire.");
        if (r.Lignes is not { Count: > 0 })
            throw new ArgumentException("Le devis doit contenir au moins une ligne.");
        if (r.Lignes.Count > 200)
            throw new ArgumentException("200 lignes maximum par devis.");
        if (r.RemiseGlobalePourcent is < 0 or > 100)
            throw new ArgumentException("La remise globale doit être comprise entre 0 et 100 %.");
        if (r.DateExpiration is { } exp && exp.Date < DateTime.UtcNow.Date.AddDays(-1))
            throw new ArgumentException("La date de validité ne peut pas être déjà passée.");

        foreach (var (l, i) in r.Lignes.Select((l, i) => (l, i + 1)))
        {
            if (string.IsNullOrWhiteSpace(l.Designation)) throw new ArgumentException($"Ligne {i} : la désignation est obligatoire.");
            if (l.Quantite <= 0) throw new ArgumentException($"Ligne {i} : la quantité doit être supérieure à 0.");
            if (l.PrixUnitaireHT < 0) throw new ArgumentException($"Ligne {i} : le prix ne peut pas être négatif.");
            if (l.TauxTVA is < 0 or > 100) throw new ArgumentException($"Ligne {i} : taux de TVA invalide.");
            if (l.RemisePourcent is < 0 or > 100) throw new ArgumentException($"Ligne {i} : la remise doit être comprise entre 0 et 100 %.");
        }

        // Le client doit exister dans l'entreprise (filtre multi-tenant) et être un client
        var tiers = await _db.Tiers.AsNoTracking().FirstOrDefaultAsync(t => t.Id == r.TiersId)
                    ?? throw new KeyNotFoundException("Client introuvable.");
        if (tiers.Type == TypeTiers.Fournisseur)
            throw new ArgumentException($"« {tiers.Nom} » est un fournisseur : choisissez un client.");
        if (!tiers.EstActif)
            throw new ArgumentException($"Le client « {tiers.Nom} » est désactivé.");
    }

    private static void Appliquer(Devis devis, CreerDevisRequest r)
    {
        devis.Libelle = r.Libelle.Trim();
        devis.TiersId = r.TiersId;
        devis.DateExpiration = r.DateExpiration?.Date ?? devis.DateExpiration ?? DateTime.UtcNow.Date.AddDays(ValiditeParDefautJours);
        devis.Notes = string.IsNullOrWhiteSpace(r.Notes) ? null : r.Notes.Trim();
        devis.Conditions = string.IsNullOrWhiteSpace(r.Conditions) ? null : r.Conditions.Trim();
        devis.RemiseGlobalePourcent = r.RemiseGlobalePourcent;

        var ordre = 0;
        foreach (var l in r.Lignes)
        {
            devis.Lignes.Add(new LigneDevis
            {
                Ordre = ++ordre,
                ProduitId = l.ProduitId,
                Designation = l.Designation.Trim(),
                Quantite = l.Quantite,
                PrixUnitaireHT = l.PrixUnitaireHT,
                RemisePourcent = l.RemisePourcent,
                TauxTVA = l.TauxTVA
            });
        }
    }

    /// <summary>Entreprise du jeton (en mode support : celle du client consulté).</summary>
    private Guid TenantId()
        => _tenant.TenantId ?? throw new UnauthorizedAccessException("Aucune entreprise associée à cette session.");

    /// <summary>DEV-2026-0001 : séquence par entreprise et par année ; saute un numéro déjà utilisé (anciens devis).</summary>
    private async Task<string> ProchainNumeroDevisAsync(Guid tenantId)
    {
        var annee = DateTime.UtcNow.Year;
        while (true)
        {
            var n = await _db.ProchainNumeroAsync(tenantId, "DEV", annee);
            var numero = $"DEV-{annee}-{n:D4}";
            if (!await _db.Devis.IgnoreQueryFilters().AnyAsync(d => d.TenantId == tenantId && d.Numero == numero))
                return numero;
        }
    }

    /// <summary>Même séquence et même format que FactureService (FA-2026-000001).</summary>
    private async Task<string> ProchainNumeroFactureAsync(Guid tenantId, int annee)
    {
        while (true)
        {
            var n = await _db.ProchainNumeroAsync(tenantId, "FAC-FA", annee);
            var numero = $"FA-{annee}-{n:D6}";
            if (!await _db.Factures.IgnoreQueryFilters().AnyAsync(f => f.TenantId == tenantId && f.Numero == numero))
                return numero;
        }
    }

    private async Task<Dictionary<Guid, string>> NomsUtilisateursAsync(IEnumerable<Guid> ids)
    {
        var liste = ids.Distinct().ToList();
        return await _db.Utilisateurs.IgnoreQueryFilters().AsNoTracking()
            .Where(u => liste.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => $"{u.Prenom} {u.Nom}".Trim());
    }

    private static DevisResumeDto Resume(Devis d, Dictionary<Guid, string> noms) => new(
        d.Id, d.Numero, d.Libelle, d.DateCreation, d.DateExpiration, d.Statut, StatutsDevis.Libelle(d.Statut),
        d.Tiers?.Nom ?? "", d.TotalHT, d.TotalTTC, noms.GetValueOrDefault(d.CreePar, ""), d.FactureNumero);

    private static DevisDto Dto(Devis d, Dictionary<Guid, string> noms) => new(
        d.Id, d.Numero, d.Libelle, d.DateCreation, d.DateExpiration, d.Statut, StatutsDevis.Libelle(d.Statut),
        d.TiersId, d.Tiers?.Nom ?? "", d.Tiers?.Email, d.Tiers?.Telephone,
        d.SousTotalHT, d.RemiseGlobalePourcent, d.RemiseGlobale, d.TotalHT, d.TotalTVA, d.TotalTTC,
        d.Notes, d.Conditions, d.DateEnvoi, d.DateDecision, d.MotifRefus, d.FactureId, d.FactureNumero, d.Version,
        noms.GetValueOrDefault(d.CreePar, ""),
        d.Lignes.Where(l => !l.IsDeleted).OrderBy(l => l.Ordre).Select(l => new LigneDevisDto(
            l.Id, l.Ordre, l.ProduitId, l.Designation, l.Quantite, l.PrixUnitaireHT, l.RemisePourcent, l.TauxTVA,
            l.MontantHT, l.MontantTVA, l.MontantTTC)).ToList());
}
