using System.Text.Json;
using Finama.Commerce.Application.Common;
using Finama.Commerce.Core.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Finama.Commerce.Infrastructure.Services;

public static class ChampsPersonnalises
{
    public static readonly string[] Types = { "texte", "nombre", "liste", "date" };
    private const int MaxChamps = 12;

    /// <summary>Valide la définition des champs et la sérialise (null si aucun champ).</summary>
    public static string? Serialiser(List<ChampPersonnaliseDto>? champs)
    {
        if (champs == null || champs.Count == 0) return null;

        if (champs.Count > MaxChamps)
            throw new BusinessRuleException($"Une catégorie ne peut pas avoir plus de {MaxChamps} champs personnalisés.");

        var noms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var propres = new List<ChampPersonnaliseDto>();

        foreach (var c in champs)
        {
            var nom = (c.Nom ?? "").Trim();
            if (nom.Length == 0 || nom.Length > 40)
                throw new BusinessRuleException("Chaque champ doit avoir un nom (40 caractères maximum).");
            if (!noms.Add(nom))
                throw new BusinessRuleException($"Le champ « {nom} » est défini deux fois.");

            var type = (c.Type ?? "").Trim().ToLowerInvariant();
            if (!Types.Contains(type))
                throw new BusinessRuleException($"Type de champ invalide pour « {nom} » (texte, nombre, liste ou date).");

            List<string>? options = null;
            if (type == "liste")
            {
                options = (c.Options ?? new()).Select(o => o.Trim()).Where(o => o.Length > 0).Distinct().ToList();
                if (options.Count == 0 || options.Count > 30 || options.Any(o => o.Length > 40))
                    throw new BusinessRuleException($"Le champ liste « {nom} » doit avoir entre 1 et 30 options (40 caractères max).");
            }

            propres.Add(new ChampPersonnaliseDto(nom, type, c.Obligatoire, options));
        }

        return JsonSerializer.Serialize(propres, JsonSerializerOptions.Web);
    }

    public static List<ChampPersonnaliseDto> Lire(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<List<ChampPersonnaliseDto>>(json, JsonSerializerOptions.Web) ?? new(); }
        catch { return new(); }
    }

    /// <summary>Champs applicables à une catégorie : ceux de ses ancêtres, surchargés par les siens (même nom).</summary>
    public static async Task<List<ChampPersonnaliseDto>> EffectifsAsync(this ICommerceDbContext db, Guid? categorieId, CancellationToken ct = default)
    {
        var chaine = new List<string?>();
        var courant = categorieId;
        var garde = 0;

        while (courant.HasValue && garde++ < 10)
        {
            var id = courant.Value;
            var cat = await db.Categories.AsNoTracking()
                .Where(c => c.Id == id)
                .Select(c => new { c.ParentId, c.ChampsJson })
                .FirstOrDefaultAsync(ct);
            if (cat == null) break;
            chaine.Add(cat.ChampsJson);
            courant = cat.ParentId;
        }

        chaine.Reverse(); // racine d'abord, la catégorie elle-même en dernier (elle l'emporte)
        var resultat = new Dictionary<string, ChampPersonnaliseDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var json in chaine)
            foreach (var champ in Lire(json))
                resultat[champ.Nom] = champ;

        return resultat.Values.ToList();
    }
}
