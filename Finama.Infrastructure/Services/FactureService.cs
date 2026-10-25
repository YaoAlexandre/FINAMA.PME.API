using Microsoft.EntityFrameworkCore;
using Finama.Core.DTOs;
using Finama.Core.Entities;
using Finama.Infrastructure.Data;

namespace Finama.Infrastructure.Services;

public interface IFactureService
{
    Task<FactureDetailDto> CreerAsync(CreerFactureRequest request, Guid utilisateurId);
    Task<FactureDetailDto> ObtenirAsync(Guid id);
    Task<PagedResult<FactureDetailDto>> ListerAsync(int page, int pageSize);
    Task<byte[]> GenererPdfAsync(Guid id);
}

public class FactureService : IFactureService
{
    private readonly AppDbContext _db;
    private readonly IFacturePdfService _pdfService;

    public FactureService(AppDbContext db, IFacturePdfService pdfService)
    {
        _db = db;
        _pdfService = pdfService;
    }

    public async Task<FactureDetailDto> CreerAsync(CreerFactureRequest request, Guid utilisateurId)
    {
        // ── Contrôles de saisie (messages clairs au lieu d'une erreur de base de données) ──
        if (request.Lignes is not { Count: > 0 })
            throw new ArgumentException("La facture doit contenir au moins une ligne.");
        foreach (var (l, i) in request.Lignes.Select((l, i) => (l, i + 1)))
        {
            if (string.IsNullOrWhiteSpace(l.Description)) throw new ArgumentException($"Ligne {i} : la désignation est obligatoire.");
            if (l.Quantite <= 0) throw new ArgumentException($"Ligne {i} : la quantité doit être supérieure à 0.");
            if (l.PrixUnitaireHT < 0) throw new ArgumentException($"Ligne {i} : le prix ne peut pas être négatif.");
            if (l.TauxTVA is < 0 or > 100) throw new ArgumentException($"Ligne {i} : taux de TVA invalide.");
        }
        if (request.DateEcheance.HasValue && request.DateEcheance.Value.Date < request.DateFacture.Date)
            throw new ArgumentException("L'échéance ne peut pas être antérieure à la date de la facture.");

        var tiers = await _db.Tiers.FirstOrDefaultAsync(t => t.Id == request.TiersId)
            ?? throw new KeyNotFoundException("Tiers introuvable.");

        if (!tiers.TenantId.HasValue)
            throw new InvalidOperationException("Le tiers sélectionné n'est rattaché à aucune entreprise.");

        var tenantId = tiers.TenantId.Value;

        var tenant = await _db.Tenants
            .Include(t => t.Pays)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId)
            ?? throw new KeyNotFoundException("Entreprise introuvable.");

        var typePrefix = request.Type == TypeFactureDto.Vente ? "FA" : "AC";
        var annee = request.DateFacture.Year;

        // Numérotation continue par entreprise, sous verrou (aucun doublon entre deux saisies simultanées)
        await using var tx = await _db.Database.BeginTransactionAsync();
        var numero = await ProchainNumeroLibreAsync(tenantId, typePrefix, annee);

        var lignes = request.Lignes.Select(l => new LigneFacture
        {
            TenantId = tenantId,
            Description = l.Description.Trim(),
            Quantite = l.Quantite,
            PrixUnitaireHT = l.PrixUnitaireHT,
            TauxTVA = l.TauxTVA,
            CompteProduitsId = l.CompteProduitsId,
        }).ToList();

        var totalHT = Math.Round(lignes.Sum(l => l.MontantHT), 2);
        var totalTVA = Math.Round(lignes.Sum(l => l.MontantTVA), 2);

        var facture = new Facture
        {
            TenantId = tenantId,
            Numero = numero,
            Type = request.Type == TypeFactureDto.Vente ? TypeFacture.Vente : TypeFacture.Achat,
            DateFacture = request.DateFacture,
            DateEcheance = request.DateEcheance,
            Statut = StatutFacture.Emise,
            TiersId = request.TiersId,
            Devise = tenant.DeviseBase,
            TotalHT = totalHT,
            TotalTVA = totalTVA,
            TotalTTC = totalHT + totalTVA,
            MontantRegle = 0,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            Lignes = lignes,
        };

        _db.Factures.Add(facture);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return await ObtenirAsync(facture.Id);
    }

    /// <summary>
    /// FA-2026-000001 : compteur par entreprise et par année. Si le compteur est en retard sur les factures
    /// existantes (factures saisies avant sa mise en place, base de test reprise…), les numéros déjà pris sont sautés.
    /// </summary>
    private async Task<string> ProchainNumeroLibreAsync(Guid tenantId, string typePrefix, int annee)
    {
        while (true)
        {
            var seq = await _db.ProchainNumeroAsync(tenantId, $"FAC-{typePrefix}", annee);
            var numero = $"{typePrefix}-{annee}-{seq:D6}";
            var pris = await _db.Factures.IgnoreQueryFilters().AnyAsync(f => f.TenantId == tenantId && f.Numero == numero);
            if (!pris) return numero;
        }
    }

    public async Task<FactureDetailDto> ObtenirAsync(Guid id)
    {
        var facture = await _db.Factures
            .Include(f => f.Lignes)
            .Include(f => f.Tiers)
            .FirstOrDefaultAsync(f => f.Id == id)
            ?? throw new KeyNotFoundException("Facture introuvable.");

        var tenant = await _db.Tenants
            .Include(t => t.Pays)
            .IgnoreQueryFilters()
            .FirstAsync(t => t.Id == facture.TenantId);

        return MapToDto(facture, tenant);
    }

    public async Task<PagedResult<FactureDetailDto>> ListerAsync(int page, int pageSize)
    {
        var total = await _db.Factures.CountAsync();
        var factures = await _db.Factures
            .Include(f => f.Lignes)
            .Include(f => f.Tiers)
            .OrderByDescending(f => f.DateFacture)
            .ThenByDescending(f => f.Numero)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        if (!factures.Any())
        {
            return new PagedResult<FactureDetailDto>(
                Items: new List<FactureDetailDto>(),
                Page: page,
                PageSize: pageSize,
                TotalItems: total,
                TotalPages: (int)Math.Ceiling(total / (double)pageSize));
        }

        var tenant = await _db.Tenants
            .Include(t => t.Pays)
            .IgnoreQueryFilters()
            .FirstAsync(t => t.Id == factures.First().TenantId);

        return new PagedResult<FactureDetailDto>(
            Items: factures.Select(f => MapToDto(f, tenant)).ToList(),
            Page: page,
            PageSize: pageSize,
            TotalItems: total,
            TotalPages: (int)Math.Ceiling(total / (double)pageSize));
    }

    public async Task<byte[]> GenererPdfAsync(Guid id)
    {
        var dto = await ObtenirAsync(id);
        return _pdfService.Generer(dto);
    }

    private static FactureDetailDto MapToDto(Facture f, Tenant tenant) => new(
        Id: f.Id, Numero: f.Numero, Type: f.Type.ToString(), Statut: f.Statut.ToString(),
        DateFacture: f.DateFacture, DateEcheance: f.DateEcheance, Devise: f.Devise,
        EntrepriseNom: tenant.Nom, EntrepriseAdresse: tenant.Adresse,
        EntrepriseTelephone: tenant.Telephone, EntrepriseEmail: tenant.Email,
        EntrepriseNumeroFiscal: tenant.NumeroFiscal, EntreprisePays: tenant.Pays.Nom,
        EntrepriseDeviseSymbole: tenant.Pays.DeviseSymbole,
        TiersNom: f.Tiers.Nom, TiersAdresse: f.Tiers.Adresse,
        TiersTelephone: f.Tiers.Telephone, TiersEmail: f.Tiers.Email,
        TiersNumeroFiscal: f.Tiers.NINEA, TiersCode: f.Tiers.Code,
        Lignes: f.Lignes.Select(l => new LigneFactureDetailDto(
            l.Description, l.Quantite, l.PrixUnitaireHT, l.TauxTVA,
            l.MontantHT, l.MontantTVA, l.MontantTTC)).ToList(),
        TotalHT: f.TotalHT, TotalTVA: f.TotalTVA, TotalTTC: f.TotalTTC,
        MontantRegle: f.MontantRegle, Solde: f.Solde, Notes: f.Notes,
        EntrepriseBanqueNom: tenant.BanqueNom,
        EntrepriseBanqueBIC: tenant.BanqueBIC
    );
}