using Finama.Commerce.Application.Common;
using Finama.Commerce.Core.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Finama.API.Controllers;

/// <summary>Photos (facultatives) des articles : envoi, suppression, liste pour la caisse, affichage public en cache.</summary>
[ApiController]
public class ImagesProduitsController : ControllerBase
{
    private const int TailleMax = 1_500_000;   // l'image arrive déjà réduite par le navigateur
    private readonly ICommerceDbContext _db;

    public ImagesProduitsController(ICommerceDbContext db) => _db = db;

    public sealed record ImageArticle(Guid ProduitId, string Url);

    /// <summary>Toutes les images de l'entreprise, en un seul appel (caisse, liste des articles).</summary>
    [HttpGet("api/commerce/produits/images")]
    [Authorize(Policy = Policies.Commercial)]
    public async Task<IActionResult> Lister(CancellationToken ct)
    {
        var liste = await _db.ImagesProduits.AsNoTracking()
            .Select(i => new { i.ProduitId, i.JetonPublic })
            .ToListAsync(ct);
        return Ok(liste.Select(i => new ImageArticle(i.ProduitId, UrlPublique(i.JetonPublic))));
    }

    [HttpPut("api/commerce/produits/{produitId:guid}/image")]
    [Authorize(Policy = Policies.Stock)]
    [RequestSizeLimit(2_000_000)]
    public async Task<IActionResult> Envoyer(Guid produitId, IFormFile fichier, CancellationToken ct)
    {
        var produit = await _db.Produits.FirstOrDefaultAsync(p => p.Id == produitId, ct);
        if (produit is null) return NotFound(new { message = "Article introuvable." });
        if (fichier is null || fichier.Length == 0) return BadRequest(new { message = "Aucune image reçue." });
        if (fichier.Length > TailleMax) return BadRequest(new { message = "Image trop lourde (1,5 Mo maximum)." });

        await using var flux = new MemoryStream();
        await fichier.CopyToAsync(flux, ct);
        var octets = flux.ToArray();
        var mime = TypeReel(octets);
        if (mime is null) return BadRequest(new { message = "Format non accepté : utilisez une photo JPEG, PNG ou WebP." });

        var image = await _db.ImagesProduits.FirstOrDefaultAsync(i => i.ProduitId == produitId, ct);
        if (image is null)
        {
            image = new ImageProduit { TenantId = produit.TenantId, ProduitId = produitId };
            _db.ImagesProduits.Add(image);
        }
        image.Contenu = octets;
        image.TypeMime = mime;
        image.Taille = octets.Length;
        image.JetonPublic = Guid.NewGuid();   // nouvelle adresse : l'ancienne photo en cache n'est plus utilisée
        await _db.SaveChangesAsync(ct);

        return Ok(new ImageArticle(produitId, UrlPublique(image.JetonPublic)));
    }

    [HttpDelete("api/commerce/produits/{produitId:guid}/image")]
    [Authorize(Policy = Policies.Stock)]
    public async Task<IActionResult> Supprimer(Guid produitId, CancellationToken ct)
    {
        var image = await _db.ImagesProduits.FirstOrDefaultAsync(i => i.ProduitId == produitId, ct);
        if (image is not null)
        {
            _db.ImagesProduits.Remove(image);
            await _db.SaveChangesAsync(ct);
        }
        return NoContent();
    }

    /// <summary>Image publique (adresse imprévisible), mise en cache un an : le jeton change à chaque nouvelle photo.</summary>
    [HttpGet("api/public/images-produits/{jeton:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> Afficher(Guid jeton, CancellationToken ct)
    {
        var image = await _db.ImagesProduits.IgnoreQueryFilters().AsNoTracking()
            .Where(i => i.JetonPublic == jeton && !i.IsDeleted)
            .Select(i => new { i.Contenu, i.TypeMime })
            .FirstOrDefaultAsync(ct);
        if (image is null) return NotFound();
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        return File(image.Contenu, image.TypeMime);
    }

    private string UrlPublique(Guid jeton) => $"{Request.Scheme}://{Request.Host}/api/public/images-produits/{jeton}";

    /// <summary>Type réel d'après les premiers octets (on ne se fie pas à l'extension ni au type annoncé).</summary>
    private static string? TypeReel(byte[] o)
    {
        if (o.Length > 3 && o[0] == 0xFF && o[1] == 0xD8 && o[2] == 0xFF) return "image/jpeg";
        if (o.Length > 8 && o[0] == 0x89 && o[1] == 0x50 && o[2] == 0x4E && o[3] == 0x47) return "image/png";
        if (o.Length > 12 && o[0] == 0x52 && o[1] == 0x49 && o[2] == 0x46 && o[3] == 0x46
            && o[8] == 0x57 && o[9] == 0x45 && o[10] == 0x42 && o[11] == 0x50) return "image/webp";
        return null;
    }
}
