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
    Task<string> GenererProchainNumeroSousCompteAsync(Guid parentId);
    Task<List<CompteComptableDto>> ListerSousComptesAsync(Guid parentId);
}

public class PlanComptableService : IPlanComptableService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext; // 🌟 Injection du contexte de Tenant pour l'isolation SaaS

    public PlanComptableService(AppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    // ─── Lister ───────────────────────────────────────────────────────────────
    public async Task<PagedResult<CompteComptableDto>> ListerAsync(FiltreCompteQuery filtre)
    {
        var tenantId = _tenantContext.TenantId ?? Guid.Empty;

        var query = _db.CompteComptables
            .Include(c => c.CompteParent)
            .Include(c => c.SousComptes)
            // 🔒 Sécurité : On affiche le plan standard (système) OU les comptes propres à ce tenant
            .Where(c => c.EstSysteme || c.TenantId == tenantId)
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
            Items: items.Select(MapToDto).ToList(),
            Page: filtre.Page,
            PageSize: filtre.PageSize,
            TotalItems: total,
            TotalPages: (int)Math.Ceiling(total / (double)filtre.PageSize)
        );
    }

    // ─── Obtenir ──────────────────────────────────────────────────────────────
    public async Task<CompteComptableDto> ObtenirAsync(Guid id)
    {
        var tenantId = _tenantContext.TenantId;

        var compte = await _db.CompteComptables
            .Include(c => c.CompteParent)
            .Include(c => c.SousComptes)
            // 🔒 Sécurité : Empêche un tenant d'aller chercher un compte d'un autre tenant via son Guid
            .FirstOrDefaultAsync(c => c.Id == id && (c.EstSysteme || c.TenantId == tenantId))
            ?? throw new KeyNotFoundException("Compte comptable introuvable.");

        return MapToDto(compte);
    }

    // ─── Créer ────────────────────────────────────────────────────────────────
    public async Task<CompteComptableDto> CreerAsync(CreerCompteRequest request)
    {
        var tenantId = _tenantContext.TenantId ?? Guid.Empty;

        // 🔒 Sécurité : L'unicité du numéro se vérifie uniquement pour ce tenant ou dans le système général
        var numeroExiste = await _db.CompteComptables
            .AnyAsync(c => c.Numero == request.Numero && (c.TenantId == tenantId || c.EstSysteme));
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
                .AnyAsync(c => c.Id == request.CompteParentId.Value && c.EstActif && (c.EstSysteme || c.TenantId == tenantId));
            if (!parentExiste)
                throw new KeyNotFoundException("Compte parent introuvable ou inactif.");
        }

        if (!Enum.TryParse<TypeCompte>(request.Type, true, out var typeCompte))
            throw new ArgumentException($"Type de compte invalide : {request.Type}. " +
                "Valeurs acceptées : Bilan, ResultatCharge, ResultatProduit, Tresorerie.");

        var compte = new CompteComptable
        {
            Id = Guid.NewGuid(),
            Numero = request.Numero,
            Libelle = request.Libelle,
            Classe = (ClasseCompte)request.Classe,
            Type = typeCompte,
            CompteParentId = request.CompteParentId,
            EstSysteme = false,   // Un compte créé manuellement ou par l'application n'est jamais système
            EstActif = true,
            TenantId = tenantId // 🌟 Liaison obligatoire au tenant actuel
        };

        _db.CompteComptables.Add(compte);
        await _db.SaveChangesAsync();

        return await ObtenirAsync(compte.Id);
    }

    // ─── Modifier ─────────────────────────────────────────────────────────────
    public async Task<CompteComptableDto> ModifierAsync(Guid id, ModifierCompteRequest request)
    {
        var tenantId = _tenantContext.TenantId;

        var compte = await _db.CompteComptables
            .FirstOrDefaultAsync(c => c.Id == id && (c.EstSysteme || c.TenantId == tenantId))
            ?? throw new KeyNotFoundException("Compte comptable introuvable.");

        // Les comptes système peuvent avoir le libellé modifié mais pas leur structure racine
        compte.Libelle = request.Libelle;
        compte.EstActif = request.EstActif;
        compte.CompteParentId = request.CompteParentId;

        await _db.SaveChangesAsync();
        return await ObtenirAsync(id);
    }

    // ─── Activer / Désactiver ─────────────────────────────────────────────────
    public async Task ActiverDesactiverAsync(Guid id, bool estActif)
    {
        var tenantId = _tenantContext.TenantId;

        var compte = await _db.CompteComptables
            .FirstOrDefaultAsync(c => c.Id == id && (c.EstSysteme || c.TenantId == tenantId))
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

    // ─── Liste pour select (Optimisée pour la saisie d'écritures) ─────────────
    public async Task<List<CompteComptableDto>> ListerPourSelectAsync(string? classe = null)
    {
        var tenantId = _tenantContext.TenantId;

        // 🌟 RÈGLE COMPTABLE CRITIQUE : On ne doit pouvoir imputer des écritures que sur des comptes terminaux 
        // (comptes feuilles / d'exécution). On exclut donc les comptes qui servent de parents.
        var query = _db.CompteComptables
            .Where(c => c.EstActif && (c.EstSysteme || c.TenantId == tenantId))
            // Un compte est sélectionnable s'il n'a aucun enfant rattaché
            .Where(c => !_db.CompteComptables.Any(enfant => enfant.CompteParentId == c.Id))
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

    // ─── Générer le prochain numéro disponible (Utile pour le frontend Blazor) ───
    public async Task<string> GenererProchainNumeroSousCompteAsync(Guid parentId)
    {
        var tenantId = _tenantContext.TenantId;

        // 1. Récupérer le compte parent
        var parent = await _db.CompteComptables
            .FirstOrDefaultAsync(c => c.Id == parentId && (c.EstSysteme || c.TenantId == tenantId))
            ?? throw new KeyNotFoundException("Compte parent introuvable.");

        // 2. Trouver le dernier enfant créé pour ce parent et ce tenant
        var dernierEnfant = await _db.CompteComptables
            .Where(c => c.CompteParentId == parentId && c.TenantId == tenantId)
            .OrderByDescending(c => c.Numero)
            .Select(c => c.Numero)
            .FirstOrDefaultAsync();

        if (dernierEnfant == null)
        {
            // Si aucun enfant n'existe, on ajoute un suffixe "1" ou "01" au numéro du parent
            // Règle générale : Si la racine fait 3 chiffres (ex: 411), le premier sous-compte sera 4111 ou 41101
            return $"{parent.Numero}1";
        }

        // 3. Incrémenter le dernier numéro trouvé
        // On essaie d'extraire la partie incrémentale
        if (long.TryParse(dernierEnfant, out var dernierNumero))
        {
            return (dernierNumero + 1).ToString();
        }

        return $"{parent.Numero}X"; // Fallback de sécurité
    }

    // ─── Lister les sous-comptes d'un parent spécifique ──────────────────────────
    public async Task<List<CompteComptableDto>> ListerSousComptesAsync(Guid parentId)
    {
        var tenantId = _tenantContext.TenantId;

        return await _db.CompteComptables
            .Include(c => c.SousComptes)
            .Where(c => c.CompteParentId == parentId && (c.EstSysteme || c.TenantId == tenantId))
            .OrderBy(c => c.Numero)
            .Select(c => MapToDto(c))
            .ToListAsync();
    }

    // ─── Mapper ───────────────────────────────────────────────────────────────
    private static CompteComptableDto MapToDto(CompteComptable c) => new(
        Id: c.Id,
        Numero: c.Numero,
        Libelle: c.Libelle,
        Classe: (int)c.Classe,
        LibelleClasse: LibelleClasse((int)c.Classe),
        Type: c.Type.ToString(),
        EstSysteme: c.EstSysteme,
        EstActif: c.EstActif,
        CompteParentId: c.CompteParentId,
        CompteParentNumero: c.CompteParent?.Numero,
        NombreSousComptes: c.SousComptes?.Count ?? 0
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
        8 => "Spéciaux (H.A.O.)",       // 🌟 Corrigé : Hors Activités Ordinaires
        9 => "Engagements Hors Bilan",  // 🌟 Corrigé : Engagements
        _ => $"Classe {classe}"
    };
}