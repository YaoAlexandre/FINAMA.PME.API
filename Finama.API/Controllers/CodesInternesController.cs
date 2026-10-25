using Finama.Commerce.Application.Common;
using Finama.Commerce.Core.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Finama.API.Controllers;

/// <summary>
/// Codes-barres internes pour les articles sans code fabricant (vrac, produits locaux, fait maison).
/// Format EAN-13 « magasin » : préfixe 20 (plage GS1 réservée à l'usage interne) + compteur + clé.
/// Lisible par toutes les douchettes et par la caméra ; unique dans l'entreprise (index TenantId + Code).
/// </summary>
[ApiController]
[Route("api/commerce/produits/codes-internes")]
[Authorize(Policy = Policies.Commercial)]
public class CodesInternesController : ControllerBase
{
    private const string Prefixe = "20";
    private readonly ICommerceDbContext _db;

    public CodesInternesController(ICommerceDbContext db)
    {
        _db = db;
    }

    /// <summary>Attribue un code interne aux articles demandés qui n'ont aucun code-barres. Les autres sont laissés tels quels.</summary>
    [HttpPost]
    public async Task<IActionResult> Generer([FromBody] GenererCodesRequest request, CancellationToken ct)
    {
        if (request.ProduitIds is not { Count: > 0 })
            return BadRequest(new { message = "Sélectionnez au moins un article." });
        if (request.ProduitIds.Count > 500)
            return BadRequest(new { message = "500 articles maximum par opération." });

        var produits = await _db.Produits
            .Include(p => p.CodesBarres)
            .Where(p => request.ProduitIds.Contains(p.Id))
            .ToListAsync(ct);

        var sansCode = produits.Where(p => !p.CodesBarres.Any()).ToList();
        if (sansCode.Count == 0)
            return Ok(new { message = "Tous les articles sélectionnés ont déjà un code-barres.", nbGeneres = 0, codes = Array.Empty<object>() });

        // Dernier compteur interne utilisé dans l'entreprise (filtre multi-tenant automatique)
        var existants = await _db.CodesBarres
            .Where(c => c.Code.StartsWith(Prefixe) && c.Code.Length == 13)
            .Select(c => c.Code)
            .ToListAsync(ct);

        var compteur = existants
            .Select(c => long.TryParse(c.AsSpan(2, 10), out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();

        var generes = new List<object>();
        foreach (var p in sansCode.OrderBy(p => p.Libelle))
        {
            compteur++;
            var corps = $"{Prefixe}{compteur:D10}";
            var code = corps + Cle(corps);

            p.CodesBarres.Add(new CodeBarresProduit { ProduitId = p.Id, Code = code, EstPrincipal = true });
            generes.Add(new { produitId = p.Id, libelle = p.Libelle, code });
        }

        await _db.SaveChangesAsync(ct);   // conflit improbable (2 générations simultanées) → 409 via ExceptionMiddleware, il suffit de relancer

        return Ok(new
        {
            message = $"{generes.Count} code(s) généré(s).",
            nbGeneres = generes.Count,
            codes = generes
        });
    }

    private static char Cle(string douzeChiffres)
    {
        var somme = 0;
        for (var i = 0; i < 12; i++)
            somme += (douzeChiffres[i] - '0') * (i % 2 == 0 ? 1 : 3);
        return (char)('0' + (10 - somme % 10) % 10);
    }

    public record GenererCodesRequest(List<Guid> ProduitIds);
}
