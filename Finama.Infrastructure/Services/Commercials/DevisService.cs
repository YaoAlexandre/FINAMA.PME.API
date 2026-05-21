using Finama.Core.DTOs;
using Finama.Core.Entities;
using Finama.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Finama.Infrastructure.Services.Commercials;

public interface IDevisService
{
    Task<List<DevisDto>> GetMesDevisAsync(Guid userId);
    Task<DevisDto> GetByIdAsync(Guid id, Guid userId);
    Task<DevisDto> CreerAsync(CreerDevisRequest request, Guid userId);
    Task<DevisDto> MettreAJourStatutAsync(Guid id, MettreAJourStatutDevisRequest request, Guid userId);
    Task<Guid> ConvertirEnFactureAsync(Guid id, Guid userId);
    Task SupprimerAsync(Guid id, Guid userId);
}

public class DevisService : IDevisService
{
    private readonly AppDbContext _db;

    public DevisService(AppDbContext db) => _db = db;

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
        // Génération du numéro auto : DEV-2026-0001
        var annee = DateTime.UtcNow.Year;
        var count = await _db.Devis.CountAsync() + 1;
        var numero = $"DEV-{annee}-{count:D4}";

        var devis = new Devis
        {
            Numero = numero,
            Libelle = request.Libelle,
            TiersId = request.TiersId,
            DateExpiration = request.DateExpiration,
            Notes = request.Notes,
            CreePar = userId,
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

        return await GetByIdAsync(devis.Id, userId);
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

        var annee = DateTime.UtcNow.Year;
        var count = await _db.Factures.CountAsync() + 1;

        var facture = new Facture
        {
            TenantId = devis.TenantId,
            Numero = $"FAC-{annee}-{count:D4}",
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
        d.Id, d.Numero, d.Libelle, d.DateCreation, d.DateExpiration,
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
        d.TotalHT, d.TotalTVA, d.TotalTTC,
        d.Lignes.Select(l => new LigneDevisDto(
            l.Id, l.Designation, l.Quantite, l.PrixUnitaireHT,
            l.TauxTVA, l.MontantHT, l.MontantTVA, l.MontantTTC
        )).ToList()
    );
}
