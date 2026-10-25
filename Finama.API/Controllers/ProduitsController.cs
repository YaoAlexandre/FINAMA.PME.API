using System.Globalization;
using System.Text.Json;
using Finama.Commerce.Application.Common;
using Finama.Commerce.Core.Domain;
using Finama.Commerce.Infrastructure.Persistence;
using Finama.Commerce.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Finama.Api.Controllers.Commerce;

[Authorize]
[ApiController]
[Route("api/commerce/produits")]
public class ProduitsController : ControllerBase
{
    private readonly CommerceDbContext _context;

    public ProduitsController(CommerceDbContext context) => _context = context;

    // 1. Catalogue avec stock du magasin demandé (ou du magasin principal). Aucun secteur d'activité n'est supposé.
    [HttpGet]
    public async Task<IActionResult> GetProduits([FromQuery] string? recherche, [FromQuery] Guid? magasinId, CancellationToken ct)
    {
        var mag = await _context.MagasinParDefautAsync(magasinId, ct) ?? Guid.Empty;

        var query = _context.Produits.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(recherche))
        {
            var r = recherche.Trim().ToLower();
            query = query.Where(p => p.Libelle.ToLower().Contains(r) ||
                                     p.Reference.ToLower().Contains(r) ||
                                     p.CodesBarres.Any(cb => cb.Code.ToLower().Contains(r)) ||
                                     (p.Livre != null && p.Livre.Isbn.ToLower().Contains(r)));
        }

        var lignes = await query
            .OrderBy(p => p.Libelle)
            .Select(p => new
            {
                p.Id,
                p.Reference,
                p.Libelle,
                p.Description,
                Type = (int)p.Type,
                Unite = (int)p.Unite,
                p.CategorieId,
                CategorieNom = p.Categorie != null ? p.Categorie.Libelle : null,
                p.PrixVenteTTC,
                p.TauxTva,
                PrixAchat = p.DernierPrixAchat ?? p.PrixAchatMoyen,
                StockActuel = _context.StocksProduits
                    .Where(s => s.ProduitId == p.Id && s.MagasinId == mag)
                    .Select(s => s.Quantite)
                    .FirstOrDefault(),
                p.SeuilReappro,
                p.SuiviLots,
                p.EstActif,
                GereStock = p.Type != TypeProduit.Service,
                CodeBarres = p.CodesBarres
                    .OrderByDescending(cb => cb.EstPrincipal)
                    .Select(cb => cb.Code)
                    .FirstOrDefault(),
                Isbn = p.Livre != null ? p.Livre.Isbn : null,
                Auteur = p.Livre != null ? p.Livre.Auteur : null,
                EditeurId = p.Livre != null ? p.Livre.EditeurId : null,
                EditeurNom = p.Livre != null && p.Livre.Editeur != null ? p.Livre.Editeur.Nom : null,
                p.AttributsJson
            })
            .ToListAsync(ct);

        var resultat = lignes.Select(x => new
        {
            x.Id,
            x.Reference,
            x.Libelle,
            x.Description,
            x.Type,
            x.Unite,
            x.CategorieId,
            x.CategorieNom,
            x.PrixVenteTTC,
            x.TauxTva,
            x.PrixAchat,
            x.StockActuel,
            x.SeuilReappro,
            x.SuiviLots,
            x.EstActif,
            x.GereStock,
            x.CodeBarres,
            x.Isbn,
            x.Auteur,
            x.EditeurId,
            Editeur = x.EditeurNom,
            Attributs = LireAttributs(x.AttributsJson)
        }).ToList();

        return Ok(resultat);
    }

    // 2. Création
    [HttpPost]
    [Authorize(Policy = "Commercial")] // vérifie que cette policy inclut bien AdminTenant
    public async Task<IActionResult> CreerProduit([FromBody] ApiProduitCreateDto dto, CancellationToken ct)
    {
        var erreur = await ValiderAsync(dto, ct);
        if (erreur != null) return UnprocessableEntity(new { message = erreur });

        var reference = string.IsNullOrWhiteSpace(dto.Reference) ? $"REF-{DateTime.UtcNow.Ticks}" : dto.Reference.Trim();
        if (await _context.Produits.AnyAsync(p => p.Reference == reference, ct))
            return UnprocessableEntity(new { message = $"La référence « {reference} » existe déjà." });

        var code = string.IsNullOrWhiteSpace(dto.CodeBarres) ? null : dto.CodeBarres.Trim();
        if (code != null && await _context.CodesBarres.AnyAsync(c => c.Code == code, ct))
            return UnprocessableEntity(new { message = $"Le code-barres « {code} » est déjà utilisé par un autre article." });

        var parametres = await _context.ParametresOuDefautAsync(ct);
        var type = (TypeProduit)dto.Type;

        var produit = new Produit
        {
            Reference = reference,
            Libelle = dto.Libelle.Trim(),
            Description = dto.Description,
            Type = type,
            Unite = dto.Unite.HasValue ? (UniteMesure)dto.Unite.Value : UniteParDefaut(type),
            CategorieId = dto.CategorieId,
            PrixVenteTTC = dto.PrixVenteTTC,
            TauxTva = dto.TauxTva ?? parametres.TauxTvaDefaut,
            DernierPrixAchat = dto.PrixAchat,
            PrixAchatMoyen = dto.PrixAchat,
            SeuilReappro = type == TypeProduit.Service ? 0 : dto.SeuilReappro,
            SuiviLots = type != TypeProduit.Service && dto.SuiviLots,
            AttributsJson = ConstruireAttributs(dto.Attributs),
            EstActif = true
        };

        if (code != null)
            produit.CodesBarres.Add(new CodeBarresProduit { Code = code, EstPrincipal = true });

        if (type == TypeProduit.Livre)
        {
            produit.Livre = new ProduitLivre
            {
                Isbn = string.IsNullOrWhiteSpace(dto.Isbn) ? "" : Gs1.NormaliserIsbn(dto.Isbn),
                Auteur = dto.Auteur ?? "",
                EditeurId = dto.EditeurId
            };
        }

        _context.Produits.Add(produit);
        await _context.SaveChangesAsync(ct);

        return Ok(new { message = "Produit créé avec succès.", id = produit.Id });
    }

    // 3. Modification
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Commercial")]
    public async Task<IActionResult> ModifierProduit(Guid id, [FromBody] ApiProduitCreateDto dto, CancellationToken ct)
    {
        var erreur = await ValiderAsync(dto, ct);
        if (erreur != null) return UnprocessableEntity(new { message = erreur });

        var produit = await _context.Produits
            .Include(p => p.CodesBarres)
            .Include(p => p.Livre)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (produit == null) return NotFound(new { message = "Produit introuvable." });

        if (!string.IsNullOrWhiteSpace(dto.Reference) && dto.Reference.Trim() != produit.Reference)
        {
            var ref2 = dto.Reference.Trim();
            if (await _context.Produits.AnyAsync(p => p.Reference == ref2 && p.Id != id, ct))
                return UnprocessableEntity(new { message = $"La référence « {ref2} » existe déjà." });
            produit.Reference = ref2;
        }

        var type = (TypeProduit)dto.Type;
        produit.Libelle = dto.Libelle.Trim();
        produit.Description = dto.Description;
        produit.Type = type;
        produit.Unite = dto.Unite.HasValue ? (UniteMesure)dto.Unite.Value : produit.Unite;
        produit.CategorieId = dto.CategorieId;
        produit.PrixVenteTTC = dto.PrixVenteTTC;
        if (dto.TauxTva.HasValue) produit.TauxTva = dto.TauxTva.Value;
        produit.DernierPrixAchat = dto.PrixAchat;
        produit.SeuilReappro = type == TypeProduit.Service ? 0 : dto.SeuilReappro;
        produit.SuiviLots = type != TypeProduit.Service && dto.SuiviLots;
        produit.AttributsJson = ConstruireAttributs(dto.Attributs);

        var code = string.IsNullOrWhiteSpace(dto.CodeBarres) ? null : dto.CodeBarres.Trim();
        if (code != null)
        {
            if (await _context.CodesBarres.AnyAsync(c => c.Code == code && c.ProduitId != id, ct))
                return UnprocessableEntity(new { message = $"Le code-barres « {code} » est déjà utilisé par un autre article." });

            var principal = produit.CodesBarres.FirstOrDefault(cb => cb.EstPrincipal) ?? produit.CodesBarres.FirstOrDefault();
            if (principal != null) principal.Code = code;
            else produit.CodesBarres.Add(new CodeBarresProduit { Code = code, EstPrincipal = true });
        }

        if (type == TypeProduit.Livre)
        {
            produit.Livre ??= new ProduitLivre { ProduitId = produit.Id };
            if (!string.IsNullOrWhiteSpace(dto.Isbn)) produit.Livre.Isbn = Gs1.NormaliserIsbn(dto.Isbn);
            produit.Livre.Auteur = dto.Auteur ?? produit.Livre.Auteur;
            produit.Livre.EditeurId = dto.EditeurId;
        }

        await _context.SaveChangesAsync(ct);
        return Ok(new { message = "Produit mis à jour avec succès." });
    }

    // 4. Suppression LOGIQUE : les tickets, mouvements et inventaires gardent leur référence.
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Commercial")]
    public async Task<IActionResult> SupprimerProduit(Guid id, CancellationToken ct)
    {
        var produit = await _context.Produits.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (produit == null) return NotFound(new { message = "Produit introuvable." });

        produit.EstActif = false;
        await _context.SaveChangesAsync(ct);
        return Ok(new { message = "Produit désactivé." });
    }

    // ───────────── validation ─────────────

    private async Task<string?> ValiderAsync(ApiProduitCreateDto d, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(d.Libelle)) return "La désignation est obligatoire.";
        if (d.PrixVenteTTC <= 0) return "Le prix de vente doit être supérieur à 0.";
        if (d.PrixAchat < 0 || d.SeuilReappro < 0) return "Prix d'achat et seuil ne peuvent pas être négatifs.";
        if (d.TauxTva.HasValue && (d.TauxTva.Value < 0 || d.TauxTva.Value > 100)) return "Taux de TVA invalide.";
        if (!Enum.IsDefined(typeof(TypeProduit), d.Type)) return "Type d'article invalide.";
        if (d.Unite.HasValue && !Enum.IsDefined(typeof(UniteMesure), d.Unite.Value)) return "Unité de mesure invalide.";

        var type = (TypeProduit)d.Type;
        if (type == TypeProduit.Livre && !string.IsNullOrWhiteSpace(d.Isbn) && !Gs1.Isbn13Valide(d.Isbn))
            return "ISBN-13 invalide.";

        if (d.CategorieId.HasValue && !await _context.Categories.AnyAsync(c => c.Id == d.CategorieId.Value, ct))
            return "Catégorie introuvable.";
        if (d.EditeurId.HasValue && !await _context.Editeurs.AnyAsync(e => e.Id == d.EditeurId.Value, ct))
            return "Éditeur introuvable.";

        // Les champs personnalisés de la catégorie (et de ses parents) sont contrôlés : obligatoire, nombre, option de liste, date.
        var champs = await _context.EffectifsAsync(d.CategorieId, ct);
        foreach (var champ in champs)
        {
            string? valeur = null;
            if (d.Attributs != null && d.Attributs.TryGetValue(champ.Nom, out var brut)) valeur = brut?.Trim();

            if (string.IsNullOrEmpty(valeur))
            {
                if (champ.Obligatoire) return $"Le champ « {champ.Nom} » est obligatoire pour cette catégorie.";
                continue;
            }

            if (champ.Type == "nombre" && !decimal.TryParse(valeur.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out _))
                return $"Le champ « {champ.Nom} » doit être un nombre.";

            if (champ.Type == "date" && !DateTime.TryParse(valeur, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                return $"Le champ « {champ.Nom} » doit être une date valide.";

            if (champ.Type == "liste" && champ.Options != null && !champ.Options.Contains(valeur, StringComparer.OrdinalIgnoreCase))
                return $"« {valeur} » n'est pas une option valide pour « {champ.Nom} ».";
        }

        return null;
    }

    private static UniteMesure UniteParDefaut(TypeProduit type) => type switch
    {
        TypeProduit.Vrac => UniteMesure.Kilogramme,
        TypeProduit.Service => UniteMesure.Heure,
        _ => UniteMesure.Piece
    };

    private static string? ConstruireAttributs(Dictionary<string, string>? attributs)
    {
        if (attributs == null) return null;

        var propres = attributs
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Key) && !string.IsNullOrWhiteSpace(kv.Value))
            .ToDictionary(kv => kv.Key.Trim(), kv => kv.Value.Trim());

        return propres.Count == 0 ? null : JsonSerializer.Serialize(propres);
    }

    private static Dictionary<string, string> LireAttributs(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new(); }
        catch { return new(); }
    }
}

// DTO interne au contrôleur API (évite les dépendances croisées avec le projet Web)
public class ApiProduitCreateDto
{
    public string Reference { get; set; } = "";
    public string Libelle { get; set; } = "";
    public string? Description { get; set; }
    public int Type { get; set; }                       // 0 Standard, 1 Vrac, 2 Livre, 3 Service
    public int? Unite { get; set; }                     // 0 Pièce, 1 Kg, 2 g, 3 L, 4 m, 5 Heure
    public Guid? CategorieId { get; set; }
    public decimal PrixAchat { get; set; }
    public decimal PrixVenteTTC { get; set; }
    public decimal? TauxTva { get; set; }               // null = TVA par défaut de l'entreprise
    public decimal SeuilReappro { get; set; }
    public bool SuiviLots { get; set; }
    public string? CodeBarres { get; set; }
    public string? Isbn { get; set; }
    public string? Auteur { get; set; }
    public Guid? EditeurId { get; set; }
    public Dictionary<string, string>? Attributs { get; set; }   // valeurs des champs personnalisés de la catégorie
}
