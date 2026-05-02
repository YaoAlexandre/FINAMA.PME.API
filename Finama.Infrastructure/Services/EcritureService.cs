using Microsoft.EntityFrameworkCore;
using Finama.Core.DTOs;
using Finama.Core.Entities;
using Finama.Infrastructure.Data;

namespace Finama.Infrastructure.Services;

public interface IEcritureService
{
    Task<EcritureDto> CreerAsync(CreerEcritureRequest request, Guid utilisateurId);
    Task<PagedResult<EcritureDto>> ListerAsync(FiltreEcritureQuery filtre);
    Task<EcritureDto> ObtenirAsync(Guid id);
    Task<EcritureDto> ValiderAsync(Guid id, Guid utilisateurId);
    Task SupprimerBrouillonAsync(Guid id, Guid utilisateurId);
}

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
        // Vérifier que l'exercice existe et n'est pas clôturé
        var exercice = await _db.Exercices
            .FirstOrDefaultAsync(e => e.Id == request.ExerciceId)
            ?? throw new KeyNotFoundException("Exercice comptable introuvable.");

        if (exercice.EstCloture)
            throw new InvalidOperationException("Impossible de saisir dans un exercice clôturé.");

        // Vérifier que la date est dans l'exercice
        if (request.DateEcriture < exercice.DateDebut || request.DateEcriture > exercice.DateFin)
            throw new InvalidOperationException(
                $"La date doit être dans l'exercice {exercice.Annee} " +
                $"({exercice.DateDebut:dd/MM/yyyy} — {exercice.DateFin:dd/MM/yyyy}).");

        // Vérifier que tous les comptes existent et sont actifs
        var compteIds = request.Lignes.Select(l => l.CompteId).Distinct().ToList();
        var comptes   = await _db.CompteComptables
            .Where(c => compteIds.Contains(c.Id) && c.EstActif)
            .ToListAsync();

        if (comptes.Count != compteIds.Count)
            throw new KeyNotFoundException("Un ou plusieurs comptes comptables sont introuvables ou inactifs.");

        // Générer la référence automatique (ex: VT-2024-000042)
        var reference = await GenererReferenceAsync(request.Journal, exercice.Annee);

        var ecriture = new EcritureComptable
        {
            Reference      = reference,
            DateEcriture   = request.DateEcriture,
            Libelle        = request.Libelle,
            Journal        = request.Journal.ToUpper(),
            Statut         = StatutEcriture.Brouillon,
            ExerciceId     = request.ExerciceId,
            FactureId      = request.FactureId,
            UtilisateurId  = utilisateurId,
            Lignes         = request.Lignes.Select(l => new LigneEcriture
            {
                CompteId            = l.CompteId,
                TiersId             = l.TiersId,
                Libelle             = l.Libelle,
                Debit               = l.Debit,
                Credit              = l.Credit,
                Devise              = l.Devise.ToUpper(),
                TauxChange          = l.TauxChange,
                MontantDeviseBase   = l.TauxChange.HasValue
                    ? Math.Round((l.Debit > 0 ? l.Debit : l.Credit) * l.TauxChange.Value, 2)
                    : null,
            }).ToList(),
        };

        _db.Ecritures.Add(ecriture);
        await _db.SaveChangesAsync();

        return await ObtenirAsync(ecriture.Id);
    }

    // ─── Lister (paginé) ──────────────────────────────────────────────────────
    public async Task<PagedResult<EcritureDto>> ListerAsync(FiltreEcritureQuery filtre)
    {
        var query = _db.Ecritures
            .Include(e => e.Lignes)
                .ThenInclude(l => l.Compte)
            .Include(e => e.Lignes)
                .ThenInclude(l => l.Tiers)
            .Include(e => e.Utilisateur)
            .AsQueryable();

        // Filtres
        if (!string.IsNullOrEmpty(filtre.Journal))
            query = query.Where(e => e.Journal == filtre.Journal.ToUpper());

        if (filtre.DateDebut.HasValue)
            query = query.Where(e => e.DateEcriture >= filtre.DateDebut.Value);

        if (filtre.DateFin.HasValue)
            query = query.Where(e => e.DateEcriture <= filtre.DateFin.Value);

        if (!string.IsNullOrEmpty(filtre.Statut) &&
            Enum.TryParse<StatutEcriture>(filtre.Statut, true, out var statut))
            query = query.Where(e => e.Statut == statut);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(e => e.DateEcriture)
            .ThenByDescending(e => e.CreatedAt)
            .Skip((filtre.Page - 1) * filtre.PageSize)
            .Take(filtre.PageSize)
            .ToListAsync();

        return new PagedResult<EcritureDto>(
            Items:      items.Select(MapToDto).ToList(),
            Page:       filtre.Page,
            PageSize:   filtre.PageSize,
            TotalItems: total,
            TotalPages: (int)Math.Ceiling(total / (double)filtre.PageSize)
        );
    }

    // ─── Obtenir ──────────────────────────────────────────────────────────────
    public async Task<EcritureDto> ObtenirAsync(Guid id)
    {
        var ecriture = await _db.Ecritures
            .Include(e => e.Lignes)
                .ThenInclude(l => l.Compte)
            .Include(e => e.Lignes)
                .ThenInclude(l => l.Tiers)
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
            .FirstOrDefaultAsync(e => e.Id == id)
            ?? throw new KeyNotFoundException("Écriture comptable introuvable.");

        if (ecriture.Statut != StatutEcriture.Brouillon)
            throw new InvalidOperationException(
                $"Seules les écritures en brouillon peuvent être validées. Statut actuel : {ecriture.Statut}.");

        if (!ecriture.EstEquilibree)
        {
            var debit  = ecriture.Lignes.Sum(l => l.Debit);
            var credit = ecriture.Lignes.Sum(l => l.Credit);
            throw new InvalidOperationException(
                $"L'écriture n'est pas équilibrée. Débit : {debit:N2} / Crédit : {credit:N2}.");
        }

        ecriture.Statut = StatutEcriture.Validee;
        await _db.SaveChangesAsync();

        return await ObtenirAsync(id);
    }

    // ─── Supprimer brouillon ──────────────────────────────────────────────────
    public async Task SupprimerBrouillonAsync(Guid id, Guid utilisateurId)
    {
        var ecriture = await _db.Ecritures
            .FirstOrDefaultAsync(e => e.Id == id)
            ?? throw new KeyNotFoundException("Écriture comptable introuvable.");

        if (ecriture.Statut != StatutEcriture.Brouillon)
            throw new InvalidOperationException("Seuls les brouillons peuvent être supprimés.");

        ecriture.IsDeleted = true; // soft delete
        await _db.SaveChangesAsync();
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Génère une référence unique séquentielle par journal et par année.
    /// Format : VT-2024-000042
    /// </summary>
    private async Task<string> GenererReferenceAsync(string journal, int annee)
    {
        var prefixe = journal.ToUpper();
        var pattern = $"{prefixe}-{annee}-";

        var dernierNumero = await _db.Ecritures
            .IgnoreQueryFilters()
            .Where(e => e.Reference.StartsWith(pattern))
            .OrderByDescending(e => e.Reference)
            .Select(e => e.Reference)
            .FirstOrDefaultAsync();

        var sequence = 1;
        if (dernierNumero is not null)
        {
            var partie = dernierNumero.Replace(pattern, "");
            if (int.TryParse(partie, out var num))
                sequence = num + 1;
        }

        return $"{pattern}{sequence:D6}";
    }

    private static EcritureDto MapToDto(EcritureComptable e) => new(
        Id:            e.Id,
        Reference:     e.Reference,
        DateEcriture:  e.DateEcriture,
        Libelle:       e.Libelle,
        Journal:       e.Journal,
        Statut:        e.Statut.ToString(),
        TotalDebit:    e.Lignes.Sum(l => l.Debit),
        TotalCredit:   e.Lignes.Sum(l => l.Credit),
        EstEquilibree: e.EstEquilibree,
        UtilisateurNom: $"{e.Utilisateur?.Prenom} {e.Utilisateur?.Nom}".Trim(),
        CreatedAt:     e.CreatedAt,
        Lignes:        e.Lignes.Select(l => new LigneEcritureDto(
            Id:             l.Id,
            CompteNumero:   l.Compte?.Numero  ?? "",
            CompteLibelle:  l.Compte?.Libelle ?? "",
            TiersNom:       l.Tiers?.Nom,
            Libelle:        l.Libelle,
            Debit:          l.Debit,
            Credit:         l.Credit,
            Devise:         l.Devise
        )).ToList()
    );
}
