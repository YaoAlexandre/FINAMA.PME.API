using Finama.Commerce.Core.Domain;

namespace Finama.Commerce.Application.Catalogue;

public record CodeBarresDto(string Code, bool EstPrincipal);

public record LivreDto(string Isbn, string Auteur, Guid? EditeurId, string? Collection, int? AnneeEdition,
                       string? Rayon, string? Etagere, bool RetournableEditeur = true);

/// <summary>Contrat commun à la création et à la modification (règles de validation partagées).</summary>
public interface IDonneesProduit
{
    string Libelle { get; }
    TypeProduit Type { get; }
    UniteMesure Unite { get; }
    decimal PrixVenteTTC { get; }
    decimal TauxTva { get; }
    decimal? PrixAchat { get; }
    bool SuiviLots { get; }
    int JoursAlertePeremption { get; }
    decimal SeuilReappro { get; }
    decimal QuantiteReappro { get; }
    IReadOnlyList<CodeBarresDto> CodesBarres { get; }
    LivreDto? Livre { get; }
}

public record CreerProduitRequest(
    string Reference, string Libelle, string? Description, TypeProduit Type, UniteMesure Unite, Guid? CategorieId,
    decimal PrixVenteTTC, decimal TauxTva, decimal? PrixAchat, bool SuiviLots, int JoursAlertePeremption,
    decimal SeuilReappro, decimal QuantiteReappro, IReadOnlyList<CodeBarresDto> CodesBarres, LivreDto? Livre) : IDonneesProduit;

public record ModifierProduitRequest(
    string Libelle, string? Description, TypeProduit Type, UniteMesure Unite, Guid? CategorieId,
    decimal PrixVenteTTC, decimal TauxTva, decimal? PrixAchat, bool SuiviLots, int JoursAlertePeremption,
    decimal SeuilReappro, decimal QuantiteReappro, bool EstActif, IReadOnlyList<CodeBarresDto> CodesBarres, LivreDto? Livre) : IDonneesProduit;

public record ProduitDto(
    Guid Id, string Reference, string Libelle, string? Description, TypeProduit Type, UniteMesure Unite, Guid? CategorieId,
    decimal PrixVenteTTC, decimal TauxTva, decimal PrixAchatMoyen, bool SuiviLots, int JoursAlertePeremption,
    decimal SeuilReappro, decimal QuantiteReappro, bool EstActif, decimal StockTotal,
    IReadOnlyList<CodeBarresDto> CodesBarres, LivreDto? Livre);

public record ProduitListeDto(
    Guid Id, string Reference, string Libelle, TypeProduit Type, UniteMesure Unite, decimal PrixVenteTTC,
    decimal StockTotal, bool EstActif, string? CodePrincipal, string? Auteur);

public record RechercheProduitsQuery(string? Q, TypeProduit? Type, Guid? CategorieId, bool? Actif, int Page = 1, int PageSize = 20);

/// <summary>Réponse d'un scan en caisse : tout ce qu'il faut pour ajouter la ligne sans second appel.</summary>
public record ProduitCaisseDto(
    Guid Id, string Reference, string Libelle, TypeProduit Type, UniteMesure Unite, decimal PrixVenteTTC, decimal TauxTva,
    bool SuiviLots, decimal StockDisponible, string? CodeBarres, decimal? QuantiteEtiquette);
