namespace Finama.Core.DTOs;

// ─── Requêtes ─────────────────────────────────────────────────────────────────

public record CreerEcritureRequest(
    DateTime DateEcriture,
    string Libelle,
    string Journal,                        // AC, VT, BQ, CA, OD
    Guid ExerciceId,
    Guid? FactureId,
    List<CreerLigneEcritureRequest> Lignes
);

public record CreerLigneEcritureRequest(
    Guid CompteId,
    Guid? TiersId,
    string? Libelle,
    decimal Debit,
    decimal Credit,
    string Devise = "STN",
    decimal? TauxChange = null
);

public record FiltreEcritureQuery(
    string? Journal = null,
    DateTime? DateDebut = null,
    DateTime? DateFin = null,
    string? Statut = null,
    int Page = 1,
    int PageSize = 20
);

// ─── Réponses ─────────────────────────────────────────────────────────────────

public record EcritureDto(
    Guid Id,
    string Reference,
    DateTime DateEcriture,
    string Libelle,
    string Journal,
    Guid ExerciceId,
    string Statut,
    decimal TotalDebit,
    decimal TotalCredit,
    bool EstEquilibree,
    string UtilisateurNom,
    DateTime CreatedAt,
    List<LigneEcritureDto> Lignes
);

public record LigneEcritureDto(
    Guid Id,
    string CompteNumero,
    string CompteLibelle,
    Guid CompteId,
    string? TiersNom,
    string? Libelle,
    decimal Debit,
    decimal Credit,
    string Devise
);

public record PagedResult<T>(
    List<T> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages
);
