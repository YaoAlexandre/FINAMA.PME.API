namespace Finama.Core.DTOs;

public record CompteComptableDto(
    Guid Id,
    string Numero,
    string Libelle,
    int Classe,
    string LibelleClasse,
    string Type,
    bool EstSysteme,
    bool EstActif,
    Guid? CompteParentId,
    string? CompteParentNumero,
    int NombreSousComptes
);

public record CreerCompteRequest(
    string Numero,
    string Libelle,
    int Classe,
    string Type,
    Guid? CompteParentId
);

public record ModifierCompteRequest(
    string Libelle,
    bool EstActif,
    Guid? CompteParentId
);

public record FiltreCompteQuery(
    string? Classe      = null,   // "1", "2" ... "7"
    string? Recherche   = null,   // numéro ou libellé
    bool? EstActif      = null,
    bool? EstSysteme    = null,
    int Page            = 1,
    int PageSize        = 50
);
