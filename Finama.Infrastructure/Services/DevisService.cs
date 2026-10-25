using Finama.Core.DTOs;
using Finama.Core.Entities;
using Finama.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Finama.Infrastructure.Services.Commercials;

public interface IDevisService
{
    Task<List<DevisDto>> GetMesDevisAsync(Guid userId);
    Task<DevisDto> GetByIdAsync(Guid id, Guid userId);
    Task<DevisDto> CreerAsync(CreerDevisRequest request, Guid userId);
    Task<DevisDto> MettreAJourStatutAsync(Guid id, MettreAJourStatutDevisRequest request, Guid userId);
    Task<Guid> ConvertirEnFactureAsync(Guid id, Guid userId);
    Task SupprimerAsync(Guid id, Guid userId);

    Task<DevisDto> ModifierAsync(Guid id, CreerDevisRequest request, Guid userId);
}

public class DevisService : IDevisService
{
    private readonly AppDbContext _db;
    private readonly IEcritureService _comptaService;
    private readonly ILogger<DevisService> _logger;

    public DevisService(AppDbContext db, IEcritureService comptaService, ILogger<DevisService> logger)
    {
        _db = db;
        _comptaService = comptaService;
        _logger = logger;
    }

    public async Task<List<DevisDto>> GetMesDevisAsync(Guid userId)
    {
        return await _db.Devis
            .Include(d => d.Tiers)
            .Include(d => d.Lignes)
            .Where(d => d.CreePar == userId)
            .OrderByDescending(d => d.DateCreation)
            .Select(d => ToDto(d))
            .ToListAsync();
    }

    public async Task<DevisDto> GetByIdAsync(Guid id, Guid userId)
    {
        var devis = await _db.Devis
            .Include(d => d.Tiers)
            .Include(d => d.Lignes)
            .FirstOrDefaultAsync(d => d.Id == id && d.CreePar == userId)
            ?? throw new KeyNotFoundException("Devis introuvable.");

        return ToDto(devis);
    }
    public async Task<DevisDto> CreerAsync(CreerDevisRequest request, Guid userId)
    {
        // Utilisation d'une transaction pour garantir la cohérence
        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {

            // (supposons que vous avez un service pour obtenir l'info du user/tenant)
            var tenantId = await _db.Utilisateurs
                .Where(u => u.Id == userId)
                .Select(u => u.TenantId)
                .FirstOrDefaultAsync();

            var annee = DateTime.UtcNow.Year;

            // 1. Trouver le plus grand numéro existant pour ce tenant et cette année
            var dernierDevis = await _db.Devis
                .Where(d => d.TenantId == tenantId && d.Numero.StartsWith($"DEV-{annee}-"))
                .OrderByDescending(d => d.Numero)
                .FirstOrDefaultAsync();

            // 2. Extraire le compteur ou démarrer à 0
            int dernierCompteur = 0;
            if (dernierDevis != null)
            {
                var parts = dernierDevis.Numero.Split('-');
                int.TryParse(parts.Last(), out dernierCompteur);
            }

            var numero = $"DEV-{annee}-{dernierCompteur + 1:D4}";

            var devis = new Devis
            {
                Numero = numero,
                Libelle = request.Libelle,
                TiersId = request.TiersId,
                DateExpiration = request.DateExpiration,
                Notes = request.Notes,
                CreePar = userId, 
                TenantId = tenantId,
                Statut = StatutDevis.Brouillon,
                Lignes = request.Lignes.Select(l => new LigneDevis
                {
                    Designation = l.Designation,
                    Quantite = l.Quantite,
                    PrixUnitaireHT = l.PrixUnitaireHT,
                    TauxTVA = l.TauxTVA
                }).ToList()
            };

            _db.Devis.Add(devis);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return await GetByIdAsync(devis.Id, userId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Erreur lors de la création du devis pour l'utilisateur {UserId}", userId);
            throw new Exception("Une erreur est survenue lors de la création du devis.", ex);
        }
    }

    // Dans DevisService
    public async Task<DevisDto> ModifierAsync(Guid id, CreerDevisRequest request, Guid userId)
    {
        var devis = await _db.Devis
            .Include(d => d.Lignes)
            .FirstOrDefaultAsync(d => d.Id == id && d.CreePar == userId)
            ?? throw new KeyNotFoundException("Devis introuvable.");

        if (devis.Statut != StatutDevis.Brouillon)
            throw new InvalidOperationException("Seul un brouillon peut être modifié.");

        devis.Libelle = request.Libelle;
        devis.TiersId = request.TiersId;
        devis.DateExpiration = request.DateExpiration;
        devis.Notes = request.Notes;

        // Remplace les lignes existantes
        _db.LignesDevis.RemoveRange(devis.Lignes);
        devis.Lignes = request.Lignes.Select(l => new LigneDevis
        {
            Designation = l.Designation,
            Quantite = l.Quantite,
            PrixUnitaireHT = l.PrixUnitaireHT,
            TauxTVA = l.TauxTVA
        }).ToList();

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id, userId);
    }

    public async Task<DevisDto> MettreAJourStatutAsync(Guid id, MettreAJourStatutDevisRequest request, Guid userId)
    {
        var devis = await _db.Devis
            .FirstOrDefaultAsync(d => d.Id == id && d.CreePar == userId)
            ?? throw new KeyNotFoundException("Devis introuvable.");

        devis.Statut = request.Statut;
        await _db.SaveChangesAsync();

        return await GetByIdAsync(id, userId);
    }

    public async Task<Guid> ConvertirEnFactureAsync(Guid id, Guid userId)
    {
        var devis = await _db.Devis
            .Include(d => d.Lignes)
            .FirstOrDefaultAsync(d => d.Id == id && d.CreePar == userId)
            ?? throw new KeyNotFoundException("Devis introuvable.");

        if (devis.Statut != StatutDevis.Accepte)
            throw new InvalidOperationException("Seul un devis accepté peut être converti en facture.");

        var tenantId = devis.TenantId ?? throw new InvalidOperationException("Devis non rattaché à un tenant.");
        var aujourdHui = DateTime.Today;
        var deviseBase = await _db.Tenants.Where(t => t.Id == tenantId).Select(t => t.DeviseBase).FirstOrDefaultAsync();

        // Même séquence et même format que FactureService (FA-2026-000123) : plus de doublon "FAC-…-Count+1"
        await using var tx = await _db.Database.BeginTransactionAsync();
        var seq = await _db.ProchainNumeroAsync(tenantId, "FAC-FA", aujourdHui.Year);

        var facture = new Facture
        {
            TenantId = tenantId,
            Numero = $"FA-{aujourdHui.Year}-{seq:D6}",
            DateFacture = aujourdHui,
            DateEcheance = aujourdHui.AddDays(30),
            Devise = string.IsNullOrWhiteSpace(deviseBase) ? "XOF" : deviseBase,
            TiersId = devis.TiersId,
            Type = TypeFacture.Vente,
            Statut = StatutFacture.Brouillon,
            Lignes = devis.Lignes.Select(l => new LigneFacture
            {
                TenantId = devis.TenantId,          // ← TenantEntity
                Description = l.Designation,        // ← bon nom
                Quantite = l.Quantite,
                PrixUnitaireHT = l.PrixUnitaireHT,
                TauxTVA = l.TauxTVA
            }).ToList()
        };

        // Recalcul des totaux
        facture.TotalHT = facture.Lignes.Sum(l => l.Quantite * l.PrixUnitaireHT);
        facture.TotalTVA = facture.Lignes.Sum(l => l.Quantite * l.PrixUnitaireHT * l.TauxTVA / 100);
        facture.TotalTTC = facture.TotalHT + facture.TotalTVA;

        _db.Factures.Add(facture);

        devis.Statut = StatutDevis.Converti;
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return facture.Id;
    }

    public async Task SupprimerAsync(Guid id, Guid userId)
    {
        var devis = await _db.Devis
            .FirstOrDefaultAsync(d => d.Id == id && d.CreePar == userId)
            ?? throw new KeyNotFoundException("Devis introuvable.");

        if (devis.Statut != StatutDevis.Brouillon)
            throw new InvalidOperationException("Seul un brouillon peut être supprimé.");

        _db.Devis.Remove(devis);
        await _db.SaveChangesAsync();
    }

    private static DevisDto ToDto(Devis d) => new(
    d.Id,
    d.Numero,
    d.Libelle,
    d.DateCreation,
    d.DateExpiration,
    d.Statut,
    d.Statut switch
    {
        StatutDevis.Brouillon => "Brouillon",
        StatutDevis.Envoye => "Envoyé",
        StatutDevis.Accepte => "Accepté",
        StatutDevis.Refuse => "Refusé",
        StatutDevis.Expire => "Expiré",
        StatutDevis.Converti => "Converti",
        _ => "Inconnu"
    },
    d.TiersId,
    d.Tiers?.Nom ?? "",
    d.Tiers?.Email,
    d.Tiers?.Telephone,
    d.SousTotalHT,
    d.RemiseGlobalePourcent,
    d.RemiseGlobale,
    d.TotalHT,
    d.TotalTVA,
    d.TotalTTC,
    d.Notes,
    d.Conditions,
    d.DateEnvoi,
    d.DateDecision,
    d.MotifRefus,
    d.FactureId,
    d.Facture?.Numero,
    d.Version,
    d.UtilisateurCreateur?.NomComplet ?? "", // Ou le champ correspondant à l'utilisateur créateur
    d.Lignes.Select((l, index) => new LigneDevisDto(
        l.Id,
        index + 1,                            // Ordre
        l.ProduitId,
        l.Designation,
        l.Quantite,
        l.PrixUnitaireHT,
        l.RemisePourcent,
        l.TauxTVA,
        l.MontantHT,
        l.MontantTVA,
        l.MontantTTC
    )).ToList()
);
}
