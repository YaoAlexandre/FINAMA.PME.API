namespace Finama.Core.DTOs;

public record CreerTiersRequest(
    string Nom,
    TypeTiersDto Type,
    string? NINEA,
    string? Adresse,
    string? Telephone,
    string? Email,
    string? Devise,
    Guid? CompteComptableId,
    Guid TenantId
);

public record ModifierTiersRequest(
    string Nom,
    string? NINEA,
    string? Adresse,
    string? Telephone,
    string? Email,
    string? Devise,
    bool EstActif
);

public record TiersDto(
    Guid Id,
    string Code,
    string Nom,
    string Type,
    string? NINEA,
    string? Adresse,
    string? Telephone,
    string? Email,
    string? Devise,
    string? CompteNumero,
    string? CompteLibelle,
    bool EstActif,
    DateTime CreatedAt
);

public record FiltreTiersQuery(
    string? Type      = null,   // "Client", "Fournisseur"
    string? Recherche = null,   // nom ou code
    bool? EstActif    = true,
    int Page          = 1,
    int PageSize      = 20
);

public enum TypeTiersDto
{
    Client             = 0,
    Fournisseur        = 1,
    ClientFournisseur  = 2,
}
