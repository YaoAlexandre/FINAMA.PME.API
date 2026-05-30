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
    private readonly ITenantContext _tenantContext; // 🌟 Isolation Multi-tenant

    public TiersService(AppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    // ─── Créer (Avec Génération Automatique de Sous-Compte) ───────────────────
    public async Task<TiersDto> CreerAsync(CreerTiersRequest request)
    {
        var tenantId = _tenantContext.TenantId ?? Guid.Empty;

        // 🔒 Sécurité : Vérifier l'unicité de l'email au sein du même Tenant uniquement
        if (!string.IsNullOrEmpty(request.Email))
        {
            var emailExiste = await _db.Tiers
                .AnyAsync(t => t.Email == request.Email.ToLower() && t.TenantId == tenantId && !t.IsDeleted);
            if (emailExiste)
                throw new InvalidOperationException($"Un tiers avec l'email '{request.Email}' existe déjà.");
        }

        Guid? compteComptableIdFinal = request.CompteComptableId;

        // 🌟 APPROCHE A : Si aucun compte comptable n'est fourni, on le génère automatiquement selon le SYSCOHADA
        if (!compteComptableIdFinal.HasValue)
        {
            compteComptableIdFinal = await GenererEtCreerSousCompteAutomatiqueAsync(request.Nom, request.Type, tenantId);
        }
        else
        {
            // Vérifier que le compte comptable fourni existe et appartient au système ou au tenant
            var compteExiste = await _db.CompteComptables
                .AnyAsync(c => c.Id == compteComptableIdFinal.Value && c.EstActif && (c.EstSysteme || c.TenantId == tenantId));
            if (!compteExiste)
                throw new KeyNotFoundException("Compte comptable introuvable, inactif ou non autorisé.");
        }

        // Générer le code interne du Tiers (CLI-XXX, FRN-XXX, etc.) cloisonné par Tenant
        var prefixe = request.Type switch
        {
            TypeTiersDto.Client => "CLI",
            TypeTiersDto.Fournisseur => "FRN",
            TypeTiersDto.ClientFournisseur => "TRS",
            _ => "TRS"
        };

        var dernierCode = await _db.Tiers
            .Where(t => t.TenantId == tenantId && t.Code.StartsWith(prefixe))
            .OrderByDescending(t => t.Code)
            .Select(t => t.Code)
            .FirstOrDefaultAsync();

        var seq = 1;
        if (dernierCode is not null &&
            int.TryParse(dernierCode.Replace($"{prefixe}-", ""), out var n))
            seq = n + 1;

        var tiers = new Tiers
        {
            Id = Guid.NewGuid(),
            Code = $"{prefixe}-{seq:D3}",
            Nom = request.Nom,
            Type = (TypeTiers)request.Type,
            NINEA = request.NINEA,
            Adresse = request.Adresse,
            Telephone = request.Telephone,
            Email = request.Email?.ToLower(),
            Devise = request.Devise,
            CompteComptableId = compteComptableIdFinal, // L'ID du sous-compte généré ou fourni
            EstActif = true,
            TenantId = tenantId
        };

        _db.Tiers.Add(tiers);
        await _db.SaveChangesAsync();

        return await ObtenirAsync(tiers.Id);
    }

    // ─── Obtenir ──────────────────────────────────────────────────────────────
    public async Task<TiersDto> ObtenirAsync(Guid id)
    {
        var tenantId = _tenantContext.TenantId;

        var tiers = await _db.Tiers
            .Include(t => t.CompteComptable)
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId && !t.IsDeleted)
            ?? throw new KeyNotFoundException("Tiers introuvable.");

        return MapToDto(tiers);
    }

    // ─── Lister ───────────────────────────────────────────────────────────────
    public async Task<PagedResult<TiersDto>> ListerAsync(FiltreTiersQuery filtre)
    {
        var tenantId = _tenantContext.TenantId;

        var query = _db.Tiers
            .Include(t => t.CompteComptable)
            .Where(t => t.TenantId == tenantId && !t.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrEmpty(filtre.Type) &&
            Enum.TryParse<TypeTiers>(filtre.Type, true, out var type))
            query = query.Where(t => t.Type == type);

        if (filtre.EstActif.HasValue)
            query = query.Where(t => t.EstActif == filtre.EstActif.Value);

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
            Items: items.Select(MapToDto).ToList(),
            Page: filtre.Page,
            PageSize: filtre.PageSize,
            TotalItems: total,
            TotalPages: (int)Math.Ceiling(total / (double)filtre.PageSize)
        );
    }

    // ─── Modifier ─────────────────────────────────────────────────────────────
    public async Task<TiersDto> ModifierAsync(Guid id, ModifierTiersRequest request)
    {
        var tenantId = _tenantContext.TenantId;

        var tiers = await _db.Tiers
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId && !t.IsDeleted)
            ?? throw new KeyNotFoundException("Tiers introuvable.");

        if (!string.IsNullOrEmpty(request.Email) && request.Email.ToLower() != tiers.Email)
        {
            var emailExiste = await _db.Tiers
                .AnyAsync(t => t.Email == request.Email.ToLower() && t.TenantId == tenantId && t.Id != id && !t.IsDeleted);
            if (emailExiste)
                throw new InvalidOperationException($"Un autre tiers utilise déjà l'email '{request.Email}'.");
        }

        tiers.Nom = request.Nom;
        tiers.NINEA = request.NINEA;
        tiers.Adresse = request.Adresse;
        tiers.Telephone = request.Telephone;
        tiers.Email = request.Email?.ToLower();
        tiers.Devise = request.Devise;
        tiers.EstActif = request.EstActif;

        // Note : Si le libellé du tiers change, on met également à jour le libellé du compte associé pour garder la cohérence
        if (tiers.CompteComptableId.HasValue)
        {
            var compte = await _db.CompteComptables.FindAsync(tiers.CompteComptableId.Value);
            if (compte != null)
            {
                compte.Libelle = $"{(tiers.Type == TypeTiers.Client ? "Client" : "Fournisseur")} - {request.Nom}";
            }
        }

        await _db.SaveChangesAsync();
        return await ObtenirAsync(id);
    }

    // ─── Supprimer (Soft Delete) ──────────────────────────────────────────────
    public async Task SupprimerAsync(Guid id)
    {
        var tenantId = _tenantContext.TenantId ?? Guid.Empty;

        var tiers = await _db.Tiers
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId && !t.IsDeleted)
            ?? throw new KeyNotFoundException("Tiers introuvable.");

        var aDesFactures = await _db.Factures.AnyAsync(f => f.TiersId == id && f.TenantId == tenantId);
        if (aDesFactures)
            throw new InvalidOperationException(
                "Impossible de supprimer ce tiers — il est lié à des factures existantes. Désactivez-le à la place.");

        tiers.IsDeleted = true;
        tiers.EstActif = false;

        // Optionnel : Désactiver également son sous-compte comptable s'il n'a aucun mouvement
        if (tiers.CompteComptableId.HasValue)
        {
            var aMouvements = await _db.LignesEcriture.AnyAsync(l => l.CompteId == tiers.CompteComptableId.Value);
            if (!aMouvements)
            {
                var compte = await _db.CompteComptables.FindAsync(tiers.CompteComptableId.Value);
                if (compte != null) compte.EstActif = false;
            }
        }

        await _db.SaveChangesAsync();
    }

    // ─── Moteur Interne de Génération de Sous-Comptes ────────────────────────
    private async Task<Guid> GenererEtCreerSousCompteAutomatiqueAsync(string nomTiers, TypeTiersDto typeTiers, Guid tenantId)
    {
        // 1. Déterminer la racine SYSCOHADA et le Type de Compte selon la nature du Tiers
        string racineNumero = typeTiers == TypeTiersDto.Client ? "411" : "401";
        string prefixeLibelle = typeTiers == TypeTiersDto.Client ? "Client" : "Fournisseur";

        // Trouver le compte parent racine (411 ou 401) dans la base
        var compteParent = await _db.CompteComptables
            .FirstOrDefaultAsync(c => c.Numero == racineNumero && (c.EstSysteme || c.TenantId == tenantId));

        if (compteParent == null)
            throw new InvalidOperationException($"Le compte collectif racine '{racineNumero}' est introuvable. Veuillez initialiser le plan comptable.");

        // 2. Chercher le dernier numéro de sous-compte attribué pour ce tenant (ex: 411001)
        var dernierSousCompte = await _db.CompteComptables
            .Where(c => c.TenantId == tenantId && c.Numero.StartsWith(racineNumero) && c.Numero != racineNumero)
            .OrderByDescending(c => c.Numero)
            .Select(c => c.Numero)
            .FirstOrDefaultAsync();

        int prochaineSequence = 1;
        if (dernierSousCompte != null)
        {
            // Extraction des 3 derniers chiffres séquentiels (ex: 411005 -> 005)
            string partieSequence = dernierSousCompte.Substring(racineNumero.Length);
            if (int.TryParse(partieSequence, out var indexActuel))
            {
                prochaineSequence = indexActuel + 1;
            }
        }

        // 3. Fabriquer le nouveau numéro au format standardisé (Ex: 411 + 001 = 411001)
        string nouveauNumeroCompte = $"{racineNumero}{prochaineSequence:D3}";

        var nouveauSousCompte = new CompteComptable
        {
            Id = Guid.NewGuid(),
            Numero = nouveauNumeroCompte,
            Libelle = $"{prefixeLibelle} - {nomTiers}",
            Classe = ClasseCompte.Classe4, // Classe 4
            Type = TypeCompte.Bilan,   // Les tiers appartiennent au Bilan
            CompteParentId = compteParent.Id,
            EstSysteme = false,
            EstActif = true,
            TenantId = tenantId
        };

        _db.CompteComptables.Add(nouveauSousCompte);
        // Note : On ne fait pas SaveChangesAsync() ici, il sera persisté en même temps que le Tiers (Atomicité de transaction)

        return nouveauSousCompte.Id;
    }

    // ─── Mapper ───────────────────────────────────────────────────────────────
    private static TiersDto MapToDto(Tiers t) => new(
        Id: t.Id,
        Code: t.Code,
        Nom: t.Nom,
        Type: t.Type.ToString(),
        NINEA: t.NINEA,
        Adresse: t.Adresse,
        Telephone: t.Telephone,
        Email: t.Email,
        Devise: t.Devise,
        CompteNumero: t.CompteComptable?.Numero,
        CompteLibelle: t.CompteComptable?.Libelle,
        EstActif: t.EstActif,
        CreatedAt: t.CreatedAt
    );
}