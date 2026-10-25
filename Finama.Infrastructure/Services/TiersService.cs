using Microsoft.EntityFrameworkCore;
using Finama.Core.DTOs;
using Finama.Core.Entities;
using Finama.Core.Validation;
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

        // Contrôle des zones (téléphone E.164, e-mail, longueurs) → 400 avec le message exact
        var nom = ValidationContact.Nom(request.Nom);
        var telephone = ValidationContact.Telephone(request.Telephone);
        var email = ValidationContact.Email(request.Email);
        var adresse = ValidationContact.Texte(request.Adresse, ValidationContact.AdresseMax, "Adresse");
        var ninea = ValidationContact.Nif(request.NINEA, "NINEA / NIF");

        // 🔒 Unicité de l'email au sein de l'entreprise uniquement
        if (email != null)
        {
            var emailExiste = await _db.Tiers
                .AnyAsync(t => t.Email == email && t.TenantId == tenantId && !t.IsDeleted);
            if (emailExiste)
                throw new InvalidOperationException($"Un tiers avec l'email '{email}' existe déjà.");
        }

        // Par défaut : un sous-compte propre au tiers (411101, 401101…). Sinon : compte existant choisi, contrôlé.
        Guid compteComptableIdFinal = request.CompteComptableId is { } choisi && choisi != Guid.Empty
            ? await ControlerCompteTiersAsync(choisi, request.Type, tenantId, tiersId: null)
            : await GenererEtCreerSousCompteAutomatiqueAsync(nom, request.Type, tenantId);

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
            Nom = nom,
            Type = (TypeTiers)request.Type,
            NINEA = ninea,
            Adresse = adresse,
            Telephone = telephone,
            Email = email,
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

        var nom = ValidationContact.Nom(request.Nom);
        var telephone = ValidationContact.Telephone(request.Telephone);
        var email = ValidationContact.Email(request.Email);
        var adresse = ValidationContact.Texte(request.Adresse, ValidationContact.AdresseMax, "Adresse");
        var ninea = ValidationContact.Nif(request.NINEA, "NINEA / NIF");

        if (email != null && email != tiers.Email)
        {
            var emailExiste = await _db.Tiers
                .AnyAsync(t => t.Email == email && t.TenantId == tenantId && t.Id != id && !t.IsDeleted);
            if (emailExiste)
                throw new InvalidOperationException($"Un autre tiers utilise déjà l'email '{email}'.");
        }

        tiers.Nom = nom;
        tiers.NINEA = ninea;
        tiers.Adresse = adresse;
        tiers.Telephone = telephone;
        tiers.Email = email;
        tiers.Devise = request.Devise;
        tiers.EstActif = request.EstActif;

        // Changement de compte rattaché (les écritures déjà passées restent sur l'ancien compte)
        if (request.CompteComptableId is { } nouveau && nouveau != Guid.Empty && nouveau != tiers.CompteComptableId)
            tiers.CompteComptableId = await ControlerCompteTiersAsync(nouveau, (TypeTiersDto)tiers.Type, tenantId ?? Guid.Empty, tiersId: tiers.Id);

        // Le libellé du compte suit le nom du tiers UNIQUEMENT si c'est son sous-compte à lui :
        // jamais un compte du plan (411100 « Clients divers »…) ni un compte partagé avec un autre tiers.
        if (tiers.CompteComptableId is { } compteId)
        {
            var compte = await _db.CompteComptables.FindAsync(compteId);
            var partage = await _db.Tiers.AnyAsync(t => t.CompteComptableId == compteId && t.Id != tiers.Id && !t.IsDeleted);
            if (compte is { EstSysteme: false } && !partage)
                compte.Libelle = $"{(tiers.Type == TypeTiers.Fournisseur ? "Fournisseur" : "Client")} - {nom}";
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

    // ─── Compte choisi à la main : contrôles ──────────────────────────────────
    /// <summary>
    /// Le compte doit être un compte de tiers (classe 4) de la bonne famille (41 client, 40 fournisseur, 4 pour un tiers mixte),
    /// actif et sans sous-compte. Il ne peut être partagé que s'il s'agit d'un compte du plan (ex. 411100 « Clients divers »).
    /// </summary>
    private async Task<Guid> ControlerCompteTiersAsync(Guid compteId, TypeTiersDto type, Guid tenantId, Guid? tiersId)
    {
        var compte = await _db.CompteComptables
            .FirstOrDefaultAsync(c => c.Id == compteId && !c.IsDeleted && (c.EstSysteme || c.TenantId == tenantId))
            ?? throw new KeyNotFoundException("Compte comptable introuvable.");

        if (!compte.EstActif)
            throw new InvalidOperationException($"Le compte {compte.Numero} est désactivé.");

        var famille = type switch { TypeTiersDto.Client => "41", TypeTiersDto.Fournisseur => "40", _ => "4" };
        if (!compte.Numero.StartsWith(famille))
            throw new InvalidOperationException(type switch
            {
                TypeTiersDto.Client => $"Un client se rattache à un compte 41… (clients). Le compte {compte.Numero} ne convient pas.",
                TypeTiersDto.Fournisseur => $"Un fournisseur se rattache à un compte 40… (fournisseurs). Le compte {compte.Numero} ne convient pas.",
                _ => $"Un tiers se rattache à un compte de la classe 4. Le compte {compte.Numero} ne convient pas."
            });

        if (await _db.CompteComptables.AnyAsync(c => c.CompteParentId == compte.Id && !c.IsDeleted))
            throw new InvalidOperationException($"Le compte {compte.Numero} est un compte de regroupement : choisissez un de ses sous-comptes, ou laissez Finama en créer un.");

        if (!compte.EstSysteme)
        {
            var autre = await _db.Tiers.Where(t => t.CompteComptableId == compte.Id && t.Id != tiersId && !t.IsDeleted)
                .Select(t => t.Nom).FirstOrDefaultAsync();
            if (autre is not null)
                throw new InvalidOperationException($"Le compte {compte.Numero} est déjà celui de « {autre} ». Choisissez un autre compte ou laissez Finama créer un sous-compte.");
        }

        return compte.Id;
    }

    // ─── Moteur Interne de Génération de Sous-Comptes ────────────────────────
    /// <summary>
    /// Sous-compte propre au tiers, à 6 chiffres sous 411 ou 401 : 411101, 411102… (411100 « Clients divers » reste libre).
    /// Le numéro suivant le plus grand déjà pris est utilisé ; un numéro existant n'est jamais réattribué.
    /// </summary>
    private async Task<Guid> GenererEtCreerSousCompteAutomatiqueAsync(string nomTiers, TypeTiersDto typeTiers, Guid tenantId)
    {
        var racineNumero = typeTiers == TypeTiersDto.Fournisseur ? "401" : "411";
        var prefixeLibelle = typeTiers == TypeTiersDto.Fournisseur ? "Fournisseur" : "Client";

        var compteParent = await _db.CompteComptables
            .FirstOrDefaultAsync(c => c.Numero == racineNumero && !c.IsDeleted && (c.EstSysteme || c.TenantId == tenantId))
            ?? throw new InvalidOperationException($"Le compte collectif « {racineNumero} » est introuvable. Initialisez le plan comptable.");

        // Numéros à 6 chiffres déjà utilisés sous la racine (y compris supprimés : l'index unique les compte encore)
        var pris = await _db.CompteComptables.IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId && c.Numero.StartsWith(racineNumero) && c.Numero.Length == 6)
            .Select(c => c.Numero).ToListAsync();
        var sequences = pris.Select(n => int.TryParse(n[3..], out var x) ? x : 0).ToHashSet();
        var suivant = Math.Max(100, sequences.DefaultIfEmpty(100).Max()) + 1;   // 100 = compte « divers » du plan
        while (sequences.Contains(suivant)) suivant++;
        if (suivant > 999)
            throw new InvalidOperationException($"Plus de numéro libre sous {racineNumero} : créez le sous-compte à la main.");

        var nouveauSousCompte = new CompteComptable
        {
            Id = Guid.NewGuid(),
            Numero = $"{racineNumero}{suivant:D3}",
            Libelle = $"{prefixeLibelle} - {nomTiers}",
            Classe = ClasseCompte.Classe4,
            Type = TypeCompte.Bilan,
            CompteParentId = compteParent.Id,
            EstSysteme = false,
            EstActif = true,
            TenantId = tenantId
        };

        _db.CompteComptables.Add(nouveauSousCompte);
        // Enregistré avec le tiers (même SaveChanges) : tout ou rien
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
        CreatedAt: t.CreatedAt,
        CompteComptableId: t.CompteComptableId
    );
}