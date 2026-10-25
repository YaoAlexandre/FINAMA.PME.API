using Finama.Commerce.Application.Common;
using Finama.Commerce.Core.Domain;
using Finama.Commerce.Core.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Finama.Commerce.Infrastructure.Services;

public interface IReferentielCommerceService
{
    Task<List<CategorieCommerceDto>> ListerCategoriesAsync(CancellationToken ct = default);
    Task<CategorieCommerceDto> CreerCategorieAsync(SaveCategorieRequest request, CancellationToken ct = default);
    Task<CategorieCommerceDto> ModifierCategorieAsync(Guid id, SaveCategorieRequest request, CancellationToken ct = default);
    Task SupprimerCategorieAsync(Guid id, CancellationToken ct = default);

    Task<List<EditeurCommerceDto>> ListerEditeursAsync(CancellationToken ct = default);
    Task<EditeurCommerceDto> CreerEditeurAsync(SaveEditeurRequest request, CancellationToken ct = default);
    Task<EditeurCommerceDto> ModifierEditeurAsync(Guid id, SaveEditeurRequest request, CancellationToken ct = default);
    Task<EditeurCommerceDto> DefinirEditeurActifAsync(Guid id, bool actif, CancellationToken ct = default);
}

public class ReferentielCommerceService : IReferentielCommerceService
{
    private readonly ICommerceDbContext _db;
    private readonly ICurrentUser _currentUser;

    public ReferentielCommerceService(ICommerceDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    // ───────────────────────────── CATÉGORIES ─────────────────────────────

    public async Task<List<CategorieCommerceDto>> ListerCategoriesAsync(CancellationToken ct = default)
    {
        var lignes = await _db.Categories.AsNoTracking()
            .OrderBy(c => c.Libelle)
            .Select(c => new
            {
                c.Id, c.Libelle, c.ParentId, c.ChampsJson,
                NbProduits = _db.Produits.Count(p => p.CategorieId == c.Id && p.EstActif)
            })
            .ToListAsync(ct);

        return lignes
            .Select(c => new CategorieCommerceDto(c.Id, c.Libelle, c.ParentId, c.NbProduits, ChampsPersonnalises.Lire(c.ChampsJson)))
            .ToList();
    }

    public async Task<CategorieCommerceDto> CreerCategorieAsync(SaveCategorieRequest request, CancellationToken ct = default)
    {
        var libelle = NettoyerObligatoire(request.Libelle, "Le libellé de la catégorie");

        if (request.ParentId.HasValue && !await _db.Categories.AnyAsync(c => c.Id == request.ParentId.Value, ct))
            throw new NotFoundException("Catégorie parente introuvable.");

        if (await _db.Categories.AnyAsync(c => c.ParentId == request.ParentId && c.Libelle == libelle, ct))
            throw new BusinessRuleException($"La catégorie « {libelle} » existe déjà à cet emplacement.");

        var categorie = new Categorie
        {
            TenantId = _currentUser.TenantId,
            Libelle = libelle,
            ParentId = request.ParentId,
            ChampsJson = ChampsPersonnalises.Serialiser(request.Champs)
        };
        _db.Categories.Add(categorie);
        await _db.SaveChangesAsync(ct);

        return new CategorieCommerceDto(categorie.Id, categorie.Libelle, categorie.ParentId, 0, ChampsPersonnalises.Lire(categorie.ChampsJson));
    }

    public async Task<CategorieCommerceDto> ModifierCategorieAsync(Guid id, SaveCategorieRequest request, CancellationToken ct = default)
    {
        var libelle = NettoyerObligatoire(request.Libelle, "Le libellé de la catégorie");

        var categorie = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Catégorie introuvable.");

        if (request.ParentId.HasValue)
        {
            if (request.ParentId.Value == id)
                throw new BusinessRuleException("Une catégorie ne peut pas être sa propre parente.");

            var toutes = await _db.Categories.AsNoTracking().Select(c => new { c.Id, c.ParentId }).ToListAsync(ct);
            if (!toutes.Any(c => c.Id == request.ParentId.Value))
                throw new NotFoundException("Catégorie parente introuvable.");

            // Interdit de déplacer une catégorie sous l'un de ses propres descendants (boucle).
            Guid? courant = request.ParentId;
            var garde = 0;
            while (courant.HasValue && garde++ < 100)
            {
                if (courant.Value == id)
                    throw new BusinessRuleException("Une catégorie ne peut pas être déplacée sous l'un de ses propres descendants.");
                var id2 = courant.Value;
                courant = toutes.FirstOrDefault(c => c.Id == id2)?.ParentId;
            }
        }

        if (await _db.Categories.AnyAsync(c => c.Id != id && c.ParentId == request.ParentId && c.Libelle == libelle, ct))
            throw new BusinessRuleException($"La catégorie « {libelle} » existe déjà à cet emplacement.");

        categorie.Libelle = libelle;
        categorie.ParentId = request.ParentId;
        categorie.ChampsJson = ChampsPersonnalises.Serialiser(request.Champs);
        await _db.SaveChangesAsync(ct);

        var nb = await _db.Produits.CountAsync(p => p.CategorieId == id && p.EstActif, ct);
        return new CategorieCommerceDto(categorie.Id, categorie.Libelle, categorie.ParentId, nb, ChampsPersonnalises.Lire(categorie.ChampsJson));
    }

    public async Task SupprimerCategorieAsync(Guid id, CancellationToken ct = default)
    {
        var categorie = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Catégorie introuvable.");

        if (await _db.Categories.AnyAsync(c => c.ParentId == id, ct))
            throw new BusinessRuleException("Cette catégorie contient des sous-catégories : supprimez-les ou déplacez-les d'abord.");

        if (await _db.Produits.AnyAsync(p => p.CategorieId == id, ct))
            throw new BusinessRuleException("Des produits utilisent cette catégorie : réaffectez-les avant de la supprimer.");

        _db.Categories.Remove(categorie);
        await _db.SaveChangesAsync(ct);
    }

    // ───────────────────────────── ÉDITEURS ─────────────────────────────

    public async Task<List<EditeurCommerceDto>> ListerEditeursAsync(CancellationToken ct = default)
    {
        return await _db.Editeurs.AsNoTracking()
            .OrderBy(e => e.Nom)
            .Select(e => new EditeurCommerceDto(
                e.Id, e.Nom, e.Contact, e.EstActif,
                _db.ProduitsLivres.Count(l => l.EditeurId == e.Id)))
            .ToListAsync(ct);
    }

    public async Task<EditeurCommerceDto> CreerEditeurAsync(SaveEditeurRequest request, CancellationToken ct = default)
    {
        var nom = NettoyerObligatoire(request.Nom, "Le nom de l'éditeur");

        if (await _db.Editeurs.AnyAsync(e => e.Nom == nom, ct))
            throw new BusinessRuleException($"L'éditeur « {nom} » existe déjà.");

        var editeur = new Editeur
        {
            TenantId = _currentUser.TenantId,
            Nom = nom,
            Contact = string.IsNullOrWhiteSpace(request.Contact) ? null : request.Contact.Trim(),
            EstActif = true
        };
        _db.Editeurs.Add(editeur);
        await _db.SaveChangesAsync(ct);

        return new EditeurCommerceDto(editeur.Id, editeur.Nom, editeur.Contact, editeur.EstActif, 0);
    }

    public async Task<EditeurCommerceDto> ModifierEditeurAsync(Guid id, SaveEditeurRequest request, CancellationToken ct = default)
    {
        var nom = NettoyerObligatoire(request.Nom, "Le nom de l'éditeur");

        var editeur = await _db.Editeurs.FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new NotFoundException("Éditeur introuvable.");

        if (await _db.Editeurs.AnyAsync(e => e.Id != id && e.Nom == nom, ct))
            throw new BusinessRuleException($"L'éditeur « {nom} » existe déjà.");

        editeur.Nom = nom;
        editeur.Contact = string.IsNullOrWhiteSpace(request.Contact) ? null : request.Contact.Trim();
        await _db.SaveChangesAsync(ct);

        var nb = await _db.ProduitsLivres.CountAsync(l => l.EditeurId == id, ct);
        return new EditeurCommerceDto(editeur.Id, editeur.Nom, editeur.Contact, editeur.EstActif, nb);
    }

    public async Task<EditeurCommerceDto> DefinirEditeurActifAsync(Guid id, bool actif, CancellationToken ct = default)
    {
        var editeur = await _db.Editeurs.FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new NotFoundException("Éditeur introuvable.");

        editeur.EstActif = actif;
        await _db.SaveChangesAsync(ct);

        var nb = await _db.ProduitsLivres.CountAsync(l => l.EditeurId == id, ct);
        return new EditeurCommerceDto(editeur.Id, editeur.Nom, editeur.Contact, editeur.EstActif, nb);
    }

    private static string NettoyerObligatoire(string? valeur, string libelleChamp)
    {
        if (string.IsNullOrWhiteSpace(valeur))
            throw new BusinessRuleException($"{libelleChamp} est obligatoire.");

        var v = valeur.Trim();
        if (v.Length > 150)
            throw new BusinessRuleException($"{libelleChamp} est trop long (150 caractères maximum).");
        return v;
    }
}
