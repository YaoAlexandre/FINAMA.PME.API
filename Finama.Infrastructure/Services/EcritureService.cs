using Finama.Core.DTOs;
using Finama.Core.Entities;
using Finama.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Finama.Infrastructure.Services;

public interface IEcritureService
{
    Task<EcritureDto> CreerAsync(CreerEcritureRequest request, Guid utilisateurId);
    Task<PagedResult<EcritureDto>> ListerAsync(FiltreEcritureQuery filtre);
    Task<EcritureDto> ObtenirAsync(Guid id);
    Task<EcritureDto> ValiderAsync(Guid id, Guid utilisateurId);
    Task SupprimerBrouillonAsync(Guid id, Guid utilisateurId);
    Task<EcritureDto> ModifierAsync(Guid id, CreerEcritureRequest request, Guid utilisateurId);
}

/// <summary>
/// Règle monétaire : Debit/Credit sont TOUJOURS stockés en devise de base du tenant
/// (balance, grand livre et clôture restent justes). Une ligne en devise étrangère
/// garde son code devise et son taux ; le montant d'origine = Debit (ou Credit) / TauxChange.
/// </summary>
public class EcritureService : IEcritureService
{
    private readonly AppDbContext _db;

    public EcritureService(AppDbContext db)
    {
        _db = db;
    }

    // ─── Créer ────────────────────────────────────────────────────────────────
    public async Task<EcritureDto> CreerAsync(CreerEcritureRequest request, Guid utilisateurId)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();

        var exercice = await ChargerExerciceOuvertAsync(request.ExerciceId, request.DateEcriture);
        var tenantId = exercice.TenantId!.Value;
        var deviseBase = await DeviseBaseAsync(tenantId);
        var journal = request.Journal.Trim().ToUpperInvariant();

        await VerifierReferencesAsync(request);
        var lignes = ConstruireLignes(request.Lignes, tenantId, deviseBase);
        VerifierEquilibre(lignes);

        var ecriture = new EcritureComptable
        {
            Reference = await GenererReferenceAsync(tenantId, journal, exercice.Annee),
            DateEcriture = request.DateEcriture.Date,
            Libelle = request.Libelle.Trim(),
            Journal = journal,
            Statut = StatutEcriture.Brouillon,
            ExerciceId = exercice.Id,
            FactureId = request.FactureId,
            UtilisateurId = utilisateurId,
            TenantId = tenantId,
            Lignes = lignes,
        };

        _db.Ecritures.Add(ecriture);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return await ObtenirAsync(ecriture.Id);
    }

    // ─── Lister (paginé, borné) ───────────────────────────────────────────────
    public async Task<PagedResult<EcritureDto>> ListerAsync(FiltreEcritureQuery filtre)
    {
        var page = Math.Max(1, filtre.Page);
        var pageSize = Math.Clamp(filtre.PageSize, 1, 100);

        var query = _db.Ecritures.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(filtre.Journal))
        {
            var journal = filtre.Journal.ToUpperInvariant();
            query = query.Where(e => e.Journal == journal);
        }

        if (filtre.DateDebut.HasValue)
            query = query.Where(e => e.DateEcriture >= filtre.DateDebut.Value);

        if (filtre.DateFin.HasValue)
            query = query.Where(e => e.DateEcriture <= filtre.DateFin.Value);

        if (!string.IsNullOrEmpty(filtre.Statut) && Enum.TryParse<StatutEcriture>(filtre.Statut, true, out var statut))
            query = query.Where(e => e.Statut == statut);

        var total = await query.CountAsync();

        var items = await query
            .Include(e => e.Lignes).ThenInclude(l => l.Compte)
            .Include(e => e.Lignes).ThenInclude(l => l.Tiers)
            .Include(e => e.Utilisateur)
            .OrderByDescending(e => e.DateEcriture)
            .ThenByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsSplitQuery()
            .ToListAsync();

        return new PagedResult<EcritureDto>(
            Items: items.Select(MapToDto).ToList(),
            Page: page,
            PageSize: pageSize,
            TotalItems: total,
            TotalPages: (int)Math.Ceiling(total / (double)pageSize));
    }

    // ─── Obtenir ──────────────────────────────────────────────────────────────
    public async Task<EcritureDto> ObtenirAsync(Guid id)
    {
        var ecriture = await _db.Ecritures
            .AsNoTracking()
            .Include(e => e.Lignes).ThenInclude(l => l.Compte)
            .Include(e => e.Lignes).ThenInclude(l => l.Tiers)
            .Include(e => e.Utilisateur)
            .FirstOrDefaultAsync(e => e.Id == id)
            ?? throw new KeyNotFoundException("Écriture comptable introuvable.");

        return MapToDto(ecriture);
    }

    // ─── Valider ──────────────────────────────────────────────────────────────
    public async Task<EcritureDto> ValiderAsync(Guid id, Guid utilisateurId)
    {
        var ecriture = await _db.Ecritures
            .Include(e => e.Lignes)
            .Include(e => e.Exercice)
            .FirstOrDefaultAsync(e => e.Id == id)
            ?? throw new KeyNotFoundException("Écriture comptable introuvable.");

        if (ecriture.Statut != StatutEcriture.Brouillon)
            throw new InvalidOperationException($"Seules les écritures en brouillon peuvent être validées. Statut actuel : {ecriture.Statut}.");

        if (ecriture.Exercice.EstCloture)
            throw new InvalidOperationException("Impossible de valider : l'exercice est clôturé.");

        if (ecriture.Lignes.Count(l => !l.IsDeleted) < 2)
            throw new InvalidOperationException("Une écriture comporte au moins deux lignes.");

        VerifierEquilibre(ecriture.Lignes.Where(l => !l.IsDeleted).ToList());

        ecriture.Statut = StatutEcriture.Validee;
        await _db.SaveChangesAsync();

        return await ObtenirAsync(id);
    }

    // ─── Supprimer brouillon (soft delete) ────────────────────────────────────
    public async Task SupprimerBrouillonAsync(Guid id, Guid utilisateurId)
    {
        var ecriture = await _db.Ecritures.FirstOrDefaultAsync(e => e.Id == id)
            ?? throw new KeyNotFoundException("Écriture comptable introuvable.");

        if (ecriture.Statut != StatutEcriture.Brouillon)
            throw new InvalidOperationException("Seuls les brouillons peuvent être supprimés.");

        ecriture.IsDeleted = true;
        await _db.SaveChangesAsync();
    }

    // ─── Modifier un brouillon ────────────────────────────────────────────────
    public async Task<EcritureDto> ModifierAsync(Guid id, CreerEcritureRequest request, Guid utilisateurId)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();

        var ecriture = await _db.Ecritures
            .Include(e => e.Lignes)
            .FirstOrDefaultAsync(e => e.Id == id)
            ?? throw new KeyNotFoundException("Écriture comptable introuvable.");

        if (ecriture.Statut != StatutEcriture.Brouillon)
            throw new InvalidOperationException("Action interdite : seuls les brouillons peuvent être modifiés.");

        // L'exercice cible (et la date) sont revérifiés : on ne déplace pas un brouillon dans un exercice clos.
        var exercice = await ChargerExerciceOuvertAsync(request.ExerciceId, request.DateEcriture);
        var tenantId = ecriture.TenantId!.Value;
        var deviseBase = await DeviseBaseAsync(tenantId);
        var journal = request.Journal.Trim().ToUpperInvariant();

        await VerifierReferencesAsync(request);
        var nouvellesLignes = ConstruireLignes(request.Lignes, tenantId, deviseBase);
        VerifierEquilibre(nouvellesLignes);

        // Changement de journal ou d'année : nouvelle référence dans la bonne séquence
        if (ecriture.Journal != journal || ecriture.ExerciceId != exercice.Id)
            ecriture.Reference = await GenererReferenceAsync(tenantId, journal, exercice.Annee);

        ecriture.DateEcriture = request.DateEcriture.Date;
        ecriture.Journal = journal;
        ecriture.Libelle = request.Libelle.Trim();
        ecriture.ExerciceId = exercice.Id;
        ecriture.FactureId = request.FactureId;
        ecriture.UtilisateurId = utilisateurId;

        _db.LignesEcriture.RemoveRange(ecriture.Lignes.ToList());
        foreach (var l in nouvellesLignes)
        {
            l.EcritureId = ecriture.Id;
            _db.LignesEcriture.Add(l);
        }

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return await ObtenirAsync(ecriture.Id);
    }

    // ─── Helpers métier ───────────────────────────────────────────────────────

    private async Task<ExerciceComptable> ChargerExerciceOuvertAsync(Guid exerciceId, DateTime date)
    {
        var exercice = await _db.Exercices.FirstOrDefaultAsync(e => e.Id == exerciceId)
            ?? throw new KeyNotFoundException("Exercice comptable introuvable.");

        if (exercice.EstCloture)
            throw new InvalidOperationException("Impossible de saisir dans un exercice clôturé.");

        if (date.Date < exercice.DateDebut.Date || date.Date > exercice.DateFin.Date)
            throw new InvalidOperationException(
                $"La date doit être comprise dans l'exercice {exercice.Annee} " +
                $"({exercice.DateDebut:dd/MM/yyyy} — {exercice.DateFin:dd/MM/yyyy}).");

        return exercice;
    }

    private async Task<string> DeviseBaseAsync(Guid tenantId)
    {
        var devise = await _db.Tenants.Where(t => t.Id == tenantId).Select(t => t.DeviseBase).FirstOrDefaultAsync();
        return string.IsNullOrWhiteSpace(devise) ? "XOF" : devise.ToUpperInvariant();
    }

    /// <summary>Comptes, tiers et facture doivent appartenir au tenant courant (filtre global EF).</summary>
    private async Task VerifierReferencesAsync(CreerEcritureRequest request)
    {
        var compteIds = request.Lignes.Select(l => l.CompteId).Distinct().ToList();
        var comptesValides = await _db.CompteComptables.CountAsync(c => compteIds.Contains(c.Id) && c.EstActif);
        if (comptesValides != compteIds.Count)
            throw new KeyNotFoundException("Un ou plusieurs comptes comptables sont introuvables ou inactifs.");

        var tiersIds = request.Lignes.Where(l => l.TiersId.HasValue).Select(l => l.TiersId!.Value).Distinct().ToList();
        if (tiersIds.Count > 0 && await _db.Tiers.CountAsync(t => tiersIds.Contains(t.Id)) != tiersIds.Count)
            throw new KeyNotFoundException("Un ou plusieurs tiers sont introuvables.");

        if (request.FactureId is { } factureId && !await _db.Factures.AnyAsync(f => f.Id == factureId))
            throw new KeyNotFoundException("Facture introuvable.");
    }

    /// <summary>
    /// Sans taux : la ligne est en devise de base (quel que soit le code envoyé par le client).
    /// Avec taux et devise étrangère : le montant saisi est converti en devise de base.
    /// </summary>
    private static List<LigneEcriture> ConstruireLignes(IEnumerable<CreerLigneEcritureRequest> source, Guid tenantId, string deviseBase)
    {
        return source.Select(l =>
        {
            var devise = string.IsNullOrWhiteSpace(l.Devise) ? deviseBase : l.Devise.Trim().ToUpperInvariant();
            var estEtrangere = l.TauxChange is > 0 && devise != deviseBase;

            decimal EnBase(decimal montant) => estEtrangere
                ? Math.Round(montant * l.TauxChange!.Value, 2, MidpointRounding.AwayFromZero)
                : montant;

            var debit = EnBase(l.Debit);
            var credit = EnBase(l.Credit);

            return new LigneEcriture
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CompteId = l.CompteId,
                TiersId = l.TiersId,
                Libelle = string.IsNullOrWhiteSpace(l.Libelle) ? null : l.Libelle.Trim(),
                Debit = debit,
                Credit = credit,
                Devise = estEtrangere ? devise : deviseBase,
                TauxChange = estEtrangere ? l.TauxChange : null,
                MontantDeviseBase = debit > 0 ? debit : credit,
            };
        }).ToList();
    }

    /// <summary>Partie double stricte en devise de base (aucune tolérance : le grand livre doit tomber juste).</summary>
    private static void VerifierEquilibre(IReadOnlyCollection<LigneEcriture> lignes)
    {
        var debit = lignes.Sum(l => l.Debit);
        var credit = lignes.Sum(l => l.Credit);

        if (debit != credit)
            throw new InvalidOperationException(
                $"L'écriture n'est pas équilibrée en devise de base. Débit : {debit:N2} / Crédit : {credit:N2} (écart {Math.Abs(debit - credit):N2}).");

        if (debit == 0)
            throw new InvalidOperationException("Une écriture ne peut pas avoir un total nul.");
    }

    /// <summary>
    /// Numérotation continue PAR TENANT, journal et année (ex : VT-2026-000042),
    /// sous verrou SQL : deux saisies simultanées n'obtiennent jamais le même numéro.
    /// Doit être appelée dans une transaction.
    /// </summary>
    private async Task<string> GenererReferenceAsync(Guid tenantId, string journal, int annee)
    {
        var numero = await _db.ProchainNumeroAsync(tenantId, $"ECR-{journal}", annee);
        return $"{journal}-{annee}-{numero:D6}";
    }

    private static EcritureDto MapToDto(EcritureComptable e)
    {
        var lignes = e.Lignes.Where(l => !l.IsDeleted).ToList();

        return new EcritureDto(
            Id: e.Id,
            Reference: e.Reference,
            DateEcriture: e.DateEcriture,
            Libelle: e.Libelle,
            Journal: e.Journal,
            ExerciceId: e.ExerciceId,
            Statut: e.Statut.ToString(),
            TotalDebit: lignes.Sum(l => l.Debit),
            TotalCredit: lignes.Sum(l => l.Credit),
            EstEquilibree: lignes.Sum(l => l.Debit) == lignes.Sum(l => l.Credit),
            UtilisateurNom: $"{e.Utilisateur?.Prenom} {e.Utilisateur?.Nom}".Trim(),
            CreatedAt: e.CreatedAt,
            Lignes: lignes.Select(l => new LigneEcritureDto(
                Id: l.Id,
                CompteNumero: l.Compte?.Numero ?? "",
                CompteLibelle: l.Compte?.Libelle ?? "",
                CompteId: l.CompteId,
                TiersNom: l.Tiers?.Nom,
                Libelle: l.Libelle,
                Debit: l.Debit,
                Credit: l.Credit,
                Devise: l.Devise)).ToList());
    }
}
