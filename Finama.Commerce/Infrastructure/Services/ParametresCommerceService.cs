using Finama.Commerce.Application.Common;
using Finama.Commerce.Core.Domain;
using Finama.Commerce.Core.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Finama.Commerce.Infrastructure.Services;

public interface IParametresCommerceService
{
    Task<ParametresCommerceDto> ObtenirAsync(CancellationToken ct = default);
    Task<ParametresCommerceDto> SauvegarderAsync(ParametresCommerceDto dto, CancellationToken ct = default);
    List<ProfilActiviteDto> ListerProfils();
    Task<ParametresCommerceDto> AppliquerProfilAsync(string code, CancellationToken ct = default);
}

public class ParametresCommerceService : IParametresCommerceService
{
    private readonly ICommerceDbContext _db;
    private readonly ICurrentUser _currentUser;

    public ParametresCommerceService(ICommerceDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    // ───────────────────────── Profils d'activité ─────────────────────────
    // Ce ne sont QUE des modèles de départ : aucune règle métier n'en dépend. L'entreprise peut tout modifier ensuite.

    private record CategorieModele(string Libelle, ChampPersonnaliseDto[]? Champs = null, string[]? SousCategories = null);
    private record ProfilDefinition(string Code, string Libelle, string Description, bool Lots, bool Vrac, bool Livres, CategorieModele[] Categories);

    private static ChampPersonnaliseDto Texte(string nom, bool obligatoire = false) => new(nom, "texte", obligatoire, null);
    private static ChampPersonnaliseDto Nombre(string nom, bool obligatoire = false) => new(nom, "nombre", obligatoire, null);
    private static ChampPersonnaliseDto Liste(string nom, params string[] options) => new(nom, "liste", false, options.ToList());

    private static readonly ProfilDefinition[] Profils =
    {
        new("GENERAL", "Commerce général / Boutique",
            "Articles courants sans particularité. Vous créez vos propres catégories.",
            false, false, false, Array.Empty<CategorieModele>()),

        new("ALIMENTATION", "Alimentation, épicerie, supermarché",
            "Produits périssables avec date de péremption (sortie FEFO) et vente au poids ou au litre.",
            true, true, false, new[]
            {
                new CategorieModele("Épicerie"), new CategorieModele("Boissons"),
                new CategorieModele("Produits frais"), new CategorieModele("Hygiène & entretien")
            }),

        new("MODE", "Mode, prêt-à-porter, chaussures",
            "Articles déclinés par taille et couleur.",
            false, false, false, new[]
            {
                new CategorieModele("Vêtements", new[] { Liste("Taille", "XS", "S", "M", "L", "XL", "XXL"), Texte("Couleur"), Texte("Marque") },
                                    new[] { "Homme", "Femme", "Enfant" }),
                new CategorieModele("Chaussures", new[] { Nombre("Pointure"), Texte("Couleur"), Texte("Marque") }),
                new CategorieModele("Accessoires", new[] { Texte("Couleur"), Texte("Marque") })
            }),

        new("ELECTRONIQUE", "Électronique, téléphonie, informatique",
            "Articles avec marque, modèle et garantie.",
            false, false, false, new[]
            {
                new CategorieModele("Téléphones & tablettes", new[] { Texte("Marque"), Texte("Modèle"), Nombre("Garantie (mois)") }),
                new CategorieModele("Informatique", new[] { Texte("Marque"), Texte("Modèle"), Nombre("Garantie (mois)") }),
                new CategorieModele("Accessoires", new[] { Texte("Marque"), Nombre("Garantie (mois)") })
            }),

        new("QUINCAILLERIE", "Quincaillerie, matériaux, bricolage",
            "Articles vendus à l'unité, au mètre ou au poids.",
            false, true, false, new[]
            {
                new CategorieModele("Outillage", new[] { Texte("Marque") }),
                new CategorieModele("Plomberie", new[] { Texte("Dimensions"), Texte("Matière") }),
                new CategorieModele("Électricité", new[] { Texte("Dimensions"), Texte("Marque") }),
                new CategorieModele("Peinture", new[] { Texte("Couleur"), Texte("Marque") }),
                new CategorieModele("Matériaux de construction", new[] { Texte("Dimensions"), Texte("Matière") })
            }),

        new("PHARMACIE", "Pharmacie, parapharmacie, cosmétique",
            "Produits à date de péremption (lots), avec dosage et forme.",
            true, false, false, new[]
            {
                new CategorieModele("Médicaments", new[] { Texte("DCI"), Texte("Dosage"),
                    Liste("Forme", "Comprimé", "Gélule", "Sirop", "Pommade", "Injectable", "Autre") }),
                new CategorieModele("Parapharmacie", new[] { Texte("Marque") }),
                new CategorieModele("Cosmétiques", new[] { Texte("Marque"), Texte("Contenance") })
            }),

        new("LIBRAIRIE", "Librairie, papeterie, fournitures scolaires",
            "Livres (ISBN, auteur, éditeur) et fournitures.",
            false, false, true, new[]
            {
                new CategorieModele("Livres"), new CategorieModele("Papeterie"), new CategorieModele("Fournitures scolaires")
            }),

        new("SERVICES", "Prestations de service et pièces",
            "Services facturés sans stock (main-d'œuvre, réparation, coiffure...) et pièces ou fournitures éventuelles.",
            false, false, false, new[]
            {
                new CategorieModele("Prestations de service"), new CategorieModele("Pièces & fournitures")
            }),
    };

    public List<ProfilActiviteDto> ListerProfils() => Profils
        .Select(p => new ProfilActiviteDto(
            p.Code, p.Libelle, p.Description, p.Lots, p.Vrac, p.Livres,
            p.Categories.Select(c => c.Libelle).ToList()))
        .ToList();

    // ───────────────────────── Paramètres ─────────────────────────

    public async Task<ParametresCommerceDto> ObtenirAsync(CancellationToken ct = default)
        => Vers(await _db.ParametresOuDefautAsync(ct));

    public async Task<ParametresCommerceDto> SauvegarderAsync(ParametresCommerceDto dto, CancellationToken ct = default)
    {
        if (dto.TauxTvaDefaut < 0 || dto.TauxTvaDefaut > 100)
            throw new BusinessRuleException("Le taux de TVA par défaut doit être compris entre 0 et 100.");
        if (dto.RemiseMaxPourcent < 0 || dto.RemiseMaxPourcent > 100)
            throw new BusinessRuleException("La remise maximale doit être comprise entre 0 et 100 %.");
        if (dto.SeuilRemiseSansPinPourcent < 0 || dto.SeuilRemiseSansPinPourcent > dto.RemiseMaxPourcent)
            throw new BusinessRuleException("Le seuil de remise sans PIN doit être compris entre 0 et la remise maximale.");
        if (dto.MontantParPoint < 0 || dto.ValeurPointFcfa < 0)
            throw new BusinessRuleException("Les paramètres de fidélité ne peuvent pas être négatifs.");

        var prefixe = (dto.PrefixeTicket ?? "").Trim().ToUpperInvariant();
        if (prefixe.Length is < 1 or > 10 || !prefixe.All(char.IsLetterOrDigit))
            throw new BusinessRuleException("Le préfixe des tickets doit contenir 1 à 10 lettres ou chiffres.");

        var p = await ChargerOuCreerAsync(ct);
        p.UtiliseLots = dto.UtiliseLots;
        p.UtiliseVrac = dto.UtiliseVrac;
        p.UtiliseLivres = dto.UtiliseLivres;
        p.TauxTvaDefaut = dto.TauxTvaDefaut;
        p.MontantParPoint = dto.MontantParPoint;
        p.ValeurPointFcfa = dto.ValeurPointFcfa;
        p.SeuilRemiseSansPinPourcent = dto.SeuilRemiseSansPinPourcent;
        p.RemiseMaxPourcent = dto.RemiseMaxPourcent;
        p.AutoriserStockNegatif = dto.AutoriserStockNegatif;
        p.PrefixeTicket = prefixe;
        if (string.IsNullOrWhiteSpace(p.Activite)) p.Activite = string.IsNullOrWhiteSpace(dto.Activite) ? "GENERAL" : dto.Activite;

        await _db.SaveChangesAsync(ct);
        return Vers(p);
    }

    /// <summary>
    /// Règle les options selon le profil. Les catégories types ne sont créées que si l'entreprise n'a encore AUCUNE catégorie
    /// (on n'écrase jamais un catalogue existant) ; les options (lots, vrac, livres) sont toujours mises à jour.
    /// </summary>
    public async Task<ParametresCommerceDto> AppliquerProfilAsync(string code, CancellationToken ct = default)
    {
        var profil = Profils.FirstOrDefault(p => string.Equals(p.Code, code, StringComparison.OrdinalIgnoreCase))
            ?? throw new NotFoundException("Profil d'activité inconnu.");

        var p = await ChargerOuCreerAsync(ct);
        p.Activite = profil.Code;
        p.UtiliseLots = profil.Lots;
        p.UtiliseVrac = profil.Vrac;
        p.UtiliseLivres = profil.Livres;

        if (profil.Categories.Length > 0 && !await _db.Categories.AnyAsync(ct))
        {
            foreach (var modele in profil.Categories)
            {
                var racine = new Categorie
                {
                    TenantId = _currentUser.TenantId,
                    Libelle = modele.Libelle,
                    ChampsJson = ChampsPersonnalises.Serialiser(modele.Champs?.ToList())
                };
                _db.Categories.Add(racine);

                foreach (var sous in modele.SousCategories ?? Array.Empty<string>())
                    _db.Categories.Add(new Categorie { TenantId = _currentUser.TenantId, Libelle = sous, ParentId = racine.Id });
            }
        }

        await _db.SaveChangesAsync(ct);
        return Vers(p);
    }

    private async Task<ParametresCommerce> ChargerOuCreerAsync(CancellationToken ct)
    {
        var p = await _db.Parametres.FirstOrDefaultAsync(ct);
        if (p != null) return p;

        p = new ParametresCommerce { TenantId = _currentUser.TenantId };
        _db.Parametres.Add(p);
        return p;
    }

    private static ParametresCommerceDto Vers(ParametresCommerce p) => new(
        p.Activite, p.UtiliseLots, p.UtiliseVrac, p.UtiliseLivres, p.TauxTvaDefaut,
        p.MontantParPoint, p.ValeurPointFcfa, p.SeuilRemiseSansPinPourcent, p.RemiseMaxPourcent,
        p.AutoriserStockNegatif, p.PrefixeTicket);
}
