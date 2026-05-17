using Microsoft.EntityFrameworkCore;
using Finama.Core.DTOs;
using Finama.Core.Entities;
using Finama.Infrastructure.Data;

namespace Finama.Infrastructure.Services;

public interface ITiersService
{
    Task<TiersDto> CreerAsync(CreerTiersRequest request);
    Task<TiersDto> ObtenirAsync(Guid id);
    Task<PagedResult<TiersDto>> ListerAsync(FiltreTiersQuery filtre);
    Task<TiersDto> ModifierAsync(Guid id, ModifierTiersRequest request);
    Task SupprimerAsync(Guid id);
}

public class TiersService : ITiersService
{
    private readonly AppDbContext _db;

    public TiersService(AppDbContext db)
    {
        _db = db;
    }

    // ─── Créer ────────────────────────────────────────────────────────────────
    public async Task<TiersDto> CreerAsync(CreerTiersRequest request)
    {
        // Vérifier unicité email si fourni
        if (!string.IsNullOrEmpty(request.Email))
        {
            var emailExiste = await _db.Tiers
                .AnyAsync(t => t.Email == request.Email.ToLower());
            if (emailExiste)
                throw new InvalidOperationException($"Un tiers avec l'email '{request.Email}' existe déjà.");
        }

        // Vérifier que le compte comptable existe si fourni
        if (request.CompteComptableId.HasValue)
        {
            var compteExiste = await _db.CompteComptables
                .AnyAsync(c => c.Id == request.CompteComptableId.Value && c.EstActif);
            if (!compteExiste)
                throw new KeyNotFoundException("Compte comptable introuvable ou inactif.");
        }

        // Générer le code automatique selon le type
        var prefixe = request.Type switch
        {
            TypeTiersDto.Client            => "CLI",
            TypeTiersDto.Fournisseur       => "FRN",
            TypeTiersDto.ClientFournisseur => "TRS",
            _                              => "TRS"
        };

        var dernierCode = await _db.Tiers
            .Where(t => t.Code.StartsWith(prefixe))
            .OrderByDescending(t => t.Code)
            .Select(t => t.Code)
            .FirstOrDefaultAsync();

        var seq = 1;
        if (dernierCode is not null &&
            int.TryParse(dernierCode.Replace($"{prefixe}-", ""), out var n))
            seq = n + 1;

        var tiers = new Tiers
        {
            Code               = $"{prefixe}-{seq:D3}",
            Nom                = request.Nom,
            Type               = (TypeTiers)request.Type,
            NINEA              = request.NINEA,
            Adresse            = request.Adresse,
            Telephone          = request.Telephone,
            Email              = request.Email?.ToLower(),
            Devise             = request.Devise,
            CompteComptableId  = request.CompteComptableId,
            EstActif           = true,
            TenantId           = request.TenantId
        };

        _db.Tiers.Add(tiers);
        await _db.SaveChangesAsync();

        return await ObtenirAsync(tiers.Id);
    }

    // ─── Obtenir ──────────────────────────────────────────────────────────────
    public async Task<TiersDto> ObtenirAsync(Guid id)
    {
        var tiers = await _db.Tiers
            .Include(t => t.CompteComptable)
            .FirstOrDefaultAsync(t => t.Id == id)
            ?? throw new KeyNotFoundException("Tiers introuvable.");

        return MapToDto(tiers);
    }

    // ─── Lister ───────────────────────────────────────────────────────────────
    public async Task<PagedResult<TiersDto>> ListerAsync(FiltreTiersQuery filtre)
    {
        var query = _db.Tiers
            .Include(t => t.CompteComptable)
            .AsQueryable();

        // Filtre type
        if (!string.IsNullOrEmpty(filtre.Type) &&
            Enum.TryParse<TypeTiers>(filtre.Type, true, out var type))
            query = query.Where(t => t.Type == type);

        // Filtre actif
        if (filtre.EstActif.HasValue)
            query = query.Where(t => t.EstActif == filtre.EstActif.Value);

        // Recherche nom ou code
        if (!string.IsNullOrEmpty(filtre.Recherche))
        {
            var terme = filtre.Recherche.ToLower();
            query = query.Where(t =>
                t.Nom.ToLower().Contains(terme) ||
                t.Code.ToLower().Contains(terme) ||
                (t.Email != null && t.Email.Contains(terme)) ||
                (t.NINEA != null && t.NINEA.Contains(terme)));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(t => t.Nom)
            .Skip((filtre.Page - 1) * filtre.PageSize)
            .Take(filtre.PageSize)
            .ToListAsync();

        return new PagedResult<TiersDto>(
            Items:      items.Select(MapToDto).ToList(),
            Page:       filtre.Page,
            PageSize:   filtre.PageSize,
            TotalItems: total,
            TotalPages: (int)Math.Ceiling(total / (double)filtre.PageSize)
        );
    }

    // ─── Modifier ─────────────────────────────────────────────────────────────
    public async Task<TiersDto> ModifierAsync(Guid id, ModifierTiersRequest request)
    {
        var tiers = await _db.Tiers.FirstOrDefaultAsync(t => t.Id == id)
            ?? throw new KeyNotFoundException("Tiers introuvable.");

        // Vérifier unicité email si changé
        if (!string.IsNullOrEmpty(request.Email) &&
            request.Email.ToLower() != tiers.Email)
        {
            var emailExiste = await _db.Tiers
                .AnyAsync(t => t.Email == request.Email.ToLower() && t.Id != id);
            if (emailExiste)
                throw new InvalidOperationException($"Un autre tiers utilise déjà l'email '{request.Email}'.");
        }

        tiers.Nom       = request.Nom;
        tiers.NINEA     = request.NINEA;
        tiers.Adresse   = request.Adresse;
        tiers.Telephone = request.Telephone;
        tiers.Email     = request.Email?.ToLower();
        tiers.Devise    = request.Devise;
        tiers.EstActif  = request.EstActif;

        await _db.SaveChangesAsync();
        return await ObtenirAsync(id);
    }

    // ─── Supprimer (soft delete) ──────────────────────────────────────────────
    public async Task SupprimerAsync(Guid id)
    {
        var tiers = await _db.Tiers.FirstOrDefaultAsync(t => t.Id == id)
            ?? throw new KeyNotFoundException("Tiers introuvable.");

        // Vérifier qu'il n'a pas de factures ou d'écritures actives
        var aDesFactures = await _db.Factures.AnyAsync(f => f.TiersId == id);
        if (aDesFactures)
            throw new InvalidOperationException(
                "Impossible de supprimer ce tiers — il est lié à des factures existantes. Désactivez-le à la place.");

        tiers.IsDeleted = true;
        tiers.EstActif  = false;
        await _db.SaveChangesAsync();
    }

    // ─── Mapper ───────────────────────────────────────────────────────────────
    private static TiersDto MapToDto(Tiers t) => new(
        Id:             t.Id,
        Code:           t.Code,
        Nom:            t.Nom,
        Type:           t.Type.ToString(),
        NINEA:          t.NINEA,
        Adresse:        t.Adresse,
        Telephone:      t.Telephone,
        Email:          t.Email,
        Devise:         t.Devise,
        CompteNumero:   t.CompteComptable?.Numero,
        CompteLibelle:  t.CompteComptable?.Libelle,
        EstActif:       t.EstActif,
        CreatedAt:      t.CreatedAt
    );
}
