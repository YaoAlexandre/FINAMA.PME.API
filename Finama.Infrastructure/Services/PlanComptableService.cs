using Microsoft.EntityFrameworkCore;
using Finama.Core.DTOs;
using Finama.Core.Entities;
using Finama.Infrastructure.Data;

namespace Finama.Infrastructure.Services;

public interface IPlanComptableService
{
    Task<PagedResult<CompteComptableDto>> ListerAsync(FiltreCompteQuery filtre);
    Task<CompteComptableDto> ObtenirAsync(Guid id);
    Task<CompteComptableDto> CreerAsync(CreerCompteRequest request);
    Task<CompteComptableDto> ModifierAsync(Guid id, ModifierCompteRequest request);
    Task ActiverDesactiverAsync(Guid id, bool estActif);
    Task<List<CompteComptableDto>> ListerPourSelectAsync(string? classe = null);
}

public class PlanComptableService : IPlanComptableService
{
    private readonly AppDbContext _db;

    public PlanComptableService(AppDbContext db)
    {
        _db = db;
    }

    // ─── Lister ───────────────────────────────────────────────────────────────
    public async Task<PagedResult<CompteComptableDto>> ListerAsync(FiltreCompteQuery filtre)
    {
        var query = _db.CompteComptables
            .Include(c => c.CompteParent)
            .Include(c => c.SousComptes)
            .AsQueryable();

        if (!string.IsNullOrEmpty(filtre.Classe)
            && int.TryParse(filtre.Classe, out var classeInt)
            && Enum.IsDefined(typeof(ClasseCompte), classeInt))
            query = query.Where(c => c.Classe == (ClasseCompte)classeInt);

        if (filtre.EstActif.HasValue)
            query = query.Where(c => c.EstActif == filtre.EstActif.Value);

        if (filtre.EstSysteme.HasValue)
            query = query.Where(c => c.EstSysteme == filtre.EstSysteme.Value);

        if (!string.IsNullOrEmpty(filtre.Recherche))
        {
            var terme = filtre.Recherche.ToLower();
            query = query.Where(c =>
                c.Numero.Contains(terme) ||
                c.Libelle.ToLower().Contains(terme));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(c => c.Numero)
            .Skip((filtre.Page - 1) * filtre.PageSize)
            .Take(filtre.PageSize)
            .ToListAsync();

        return new PagedResult<CompteComptableDto>(
            Items:      items.Select(MapToDto).ToList(),
            Page:       filtre.Page,
            PageSize:   filtre.PageSize,
            TotalItems: total,
            TotalPages: (int)Math.Ceiling(total / (double)filtre.PageSize)
        );
    }

    // ─── Obtenir ──────────────────────────────────────────────────────────────
    public async Task<CompteComptableDto> ObtenirAsync(Guid id)
    {
        var compte = await _db.CompteComptables
            .Include(c => c.CompteParent)
            .Include(c => c.SousComptes)
            .FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new KeyNotFoundException("Compte comptable introuvable.");

        return MapToDto(compte);
    }

    // ─── Créer ────────────────────────────────────────────────────────────────
    public async Task<CompteComptableDto> CreerAsync(CreerCompteRequest request)
    {
        // Vérifier unicité du numéro dans ce tenant
        var numeroExiste = await _db.CompteComptables
            .AnyAsync(c => c.Numero == request.Numero);
        if (numeroExiste)
            throw new InvalidOperationException(
                $"Le compte N° {request.Numero} existe déjà dans votre plan comptable.");

        // Vérifier que la classe correspond au numéro (règle OHADA)
        var premierChiffre = request.Numero[0] - '0';
        if (premierChiffre != request.Classe)
            throw new InvalidOperationException(
                $"Le numéro {request.Numero} doit commencer par {request.Classe} pour la classe {request.Classe}.");

        // Vérifier le compte parent si fourni
        if (request.CompteParentId.HasValue)
        {
            var parentExiste = await _db.CompteComptables
                .AnyAsync(c => c.Id == request.CompteParentId.Value && c.EstActif);
            if (!parentExiste)
                throw new KeyNotFoundException("Compte parent introuvable ou inactif.");
        }

        if (!Enum.TryParse<TypeCompte>(request.Type, true, out var typeCompte))
            throw new ArgumentException($"Type de compte invalide : {request.Type}. " +
                "Valeurs acceptées : Bilan, ResultatCharge, ResultatProduit, Tresorerie.");

        var compte = new CompteComptable
        {
            Numero          = request.Numero,
            Libelle         = request.Libelle,
            Classe          = (ClasseCompte)request.Classe,
            Type            = typeCompte,
            CompteParentId  = request.CompteParentId,
            EstSysteme      = false,   // comptes créés manuellement ne sont pas système
            EstActif        = true,
        };

        _db.CompteComptables.Add(compte);
        await _db.SaveChangesAsync();

        return await ObtenirAsync(compte.Id);
    }

    // ─── Modifier ─────────────────────────────────────────────────────────────
    public async Task<CompteComptableDto> ModifierAsync(Guid id, ModifierCompteRequest request)
    {
        var compte = await _db.CompteComptables
            .FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new KeyNotFoundException("Compte comptable introuvable.");

        // Les comptes système peuvent avoir le libellé modifié mais pas supprimés
        compte.Libelle        = request.Libelle;
        compte.EstActif       = request.EstActif;
        compte.CompteParentId = request.CompteParentId;

        await _db.SaveChangesAsync();
        return await ObtenirAsync(id);
    }

    // ─── Activer / Désactiver ─────────────────────────────────────────────────
    public async Task ActiverDesactiverAsync(Guid id, bool estActif)
    {
        var compte = await _db.CompteComptables
            .FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new KeyNotFoundException("Compte comptable introuvable.");

        // Vérifier qu'il n'a pas de mouvements si on désactive
        if (!estActif)
        {
            var aMouvements = await _db.LignesEcriture.AnyAsync(l => l.CompteId == id);
            if (aMouvements)
                throw new InvalidOperationException(
                    "Impossible de désactiver ce compte — il a des mouvements enregistrés.");
        }

        compte.EstActif = estActif;
        await _db.SaveChangesAsync();
    }

    // ─── Liste pour select (frontend) ─────────────────────────────────────────
    public async Task<List<CompteComptableDto>> ListerPourSelectAsync(string? classe = null)
    {
        var query = _db.CompteComptables
            .Include(c => c.SousComptes)
            .Where(c => c.EstActif)
            .AsQueryable();

        if (!string.IsNullOrEmpty(classe)
            && int.TryParse(classe, out var classeInt)
            && Enum.IsDefined(typeof(ClasseCompte), classeInt))
            query = query.Where(c => c.Classe == (ClasseCompte)classeInt);

        return await query
            .OrderBy(c => c.Numero)
            .Select(c => MapToDto(c))
            .ToListAsync();
    }

    // ─── Mapper ───────────────────────────────────────────────────────────────
    private static CompteComptableDto MapToDto(CompteComptable c) => new(
        Id:                 c.Id,
        Numero:             c.Numero,
        Libelle:            c.Libelle,
        Classe:             (int)c.Classe,
        LibelleClasse:      LibelleClasse((int)c.Classe),
        Type:               c.Type.ToString(),
        EstSysteme:         c.EstSysteme,
        EstActif:           c.EstActif,
        CompteParentId:     c.CompteParentId,
        CompteParentNumero: c.CompteParent?.Numero,
        NombreSousComptes:  c.SousComptes?.Count ?? 0
    );

    private static string LibelleClasse(int classe) => classe switch
    {
        1 => "Capitaux",
        2 => "Immobilisations",
        3 => "Stocks",
        4 => "Tiers",
        5 => "Trésorerie",
        6 => "Charges",
        7 => "Produits",
        _ => $"Classe {classe}"
    };
}
