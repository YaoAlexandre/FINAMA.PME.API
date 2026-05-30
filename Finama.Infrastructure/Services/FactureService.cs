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
        _db         = db;
        _pdfService = pdfService;
    }

    public async Task<FactureDetailDto> CreerAsync(CreerFactureRequest request, Guid utilisateurId)
    {
        var tiers = await _db.Tiers.FirstOrDefaultAsync(t => t.Id == request.TiersId)
            ?? throw new KeyNotFoundException("Tiers introuvable.");

        var tenant = await _db.Tenants
            .Include(t => t.Pays)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tiers.TenantId)
            ?? throw new KeyNotFoundException("Tenant introuvable.");

        var typePrefix = request.Type == TypeFactureDto.Vente ? "FA" : "AC";
        var annee = request.DateFacture.Year;
        var pattern = $"{typePrefix}-{annee}-";
        var derniere = await _db.Factures
            .Where(f => f.Numero.StartsWith(pattern))
            .OrderByDescending(f => f.Numero)
            .Select(f => f.Numero)
            .FirstOrDefaultAsync();

        var seq = 1;
        if (derniere is not null && int.TryParse(derniere.Replace(pattern, ""), out var n))
            seq = n + 1;

        var lignes = request.Lignes.Select(l => new LigneFacture
        {
            TenantId         = tiers.TenantId,
            Description      = l.Description,
            Quantite         = l.Quantite,
            PrixUnitaireHT   = l.PrixUnitaireHT,
            TauxTVA          = l.TauxTVA,
            CompteProduitsId = l.CompteProduitsId,
        }).ToList();

        var totalHT  = lignes.Sum(l => l.MontantHT);
        var totalTVA = lignes.Sum(l => l.MontantTVA);

        var facture = new Facture
        {
            TenantId      = tiers.TenantId,
            Numero        = $"{pattern}{seq:D6}",
            Type          = request.Type == TypeFactureDto.Vente ? TypeFacture.Vente : TypeFacture.Achat,
            DateFacture   = request.DateFacture,
            DateEcheance  = request.DateEcheance,
            Statut        = StatutFacture.Emise,
            TiersId       = request.TiersId,
            Devise        = tenant.DeviseBase,
            TotalHT       = totalHT,
            TotalTVA      = totalTVA,
            TotalTTC      = totalHT + totalTVA,
            MontantRegle  = 0,
            Notes         = request.Notes,
            Lignes        = lignes,
        };

        _db.Factures.Add(facture);
        await _db.SaveChangesAsync();
        return await ObtenirAsync(facture.Id);
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
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // ─── SÉCURITÉ 1 : Si aucune facture, inutile de chercher le tenant ───
        if (!factures.Any())
        {
            return new PagedResult<FactureDetailDto>(
                Items: new List<FactureDetailDto>(),
                Page: page,
                PageSize: pageSize,
                TotalItems: total,
                TotalPages: (int)Math.Ceiling(total / (double)pageSize)
            );
        }

        // ─── SÉCURITÉ 2 : Extraire l'ID dans une variable locale isolée ───
        Guid targetTenantId = factures.First().TenantId;

        // EF Core sait maintenant traduire 'targetTenantId' comme un simple paramètre SQL standard (@__targetTenantId_0)
        var tenant = await _db.Tenants
            .Include(t => t.Pays)
            .IgnoreQueryFilters()
            .FirstAsync(t => t.Id == targetTenantId);

        return new PagedResult<FactureDetailDto>(
            Items: factures.Select(f => MapToDto(f, tenant)).ToList(),
            Page: page,
            PageSize: pageSize,
            TotalItems: total,
            TotalPages: (int)Math.Ceiling(total / (double)pageSize)
        );
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

    // Ajout des nouvelles propriétés pour le PDF
    EntrepriseBanqueNom: tenant.BanqueNom,
    EntrepriseBanqueBIC: tenant.BanqueBIC
);
}
