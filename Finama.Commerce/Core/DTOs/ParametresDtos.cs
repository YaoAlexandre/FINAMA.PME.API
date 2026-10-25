namespace Finama.Commerce.Core.DTOs;

public record ParametresCommerceDto(
    string? Activite,
    bool UtiliseLots, bool UtiliseVrac, bool UtiliseLivres,
    decimal TauxTvaDefaut,
    decimal MontantParPoint, decimal ValeurPointFcfa,
    decimal SeuilRemiseSansPinPourcent, decimal RemiseMaxPourcent,
    bool AutoriserStockNegatif, string PrefixeTicket);

/// <summary>Modèle de démarrage proposé à la PME : il règle les options et crée des catégories types (modifiables).</summary>
public record ProfilActiviteDto(
    string Code, string Libelle, string Description,
    bool UtiliseLots, bool UtiliseVrac, bool UtiliseLivres,
    List<string> CategoriesTypes);

public record AppliquerProfilRequest(string Code);
