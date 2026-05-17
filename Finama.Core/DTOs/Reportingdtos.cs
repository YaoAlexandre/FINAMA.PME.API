namespace Finama.Core.DTOs;

// ─── Balance des comptes ──────────────────────────────────────────────────────

public record FiltreBalanceQuery(
    Guid ExerciceId,
    DateTime? DateDebut = null,
    DateTime? DateFin = null,
    string? ClasseCompte = null,   // "1", "2", ... "7"
    bool IncludeVides = false   // inclure comptes sans mouvement
);

public record BalanceDto(
    Guid ExerciceId,
    int Annee,
    DateTime DateDebut,
    DateTime DateFin,
    DateTime? FiltreDebut,
    DateTime? FiltreFin,
    List<LigneBalanceDto> Lignes,
    TotauxBalanceDto Totaux
);

public record LigneBalanceDto(
    Guid CompteId,
    string Numero,
    string Libelle,
    int Classe,
    decimal SoldeOuvertureDebit,
    decimal SoldeOuvertureCredit,
    decimal MouvementsDebit,
    decimal MouvementsCredit,
    decimal SoldeFinalDebit,
    decimal SoldeFinalCredit
);

public record TotauxBalanceDto(
    decimal TotalOuvertureDebit,
    decimal TotalOuvertureCredit,
    decimal TotalMouvementsDebit,
    decimal TotalMouvementsCredit,
    decimal TotalSoldeFinalDebit,
    decimal TotalSoldeFinalCredit
);

// ─── Grand livre ──────────────────────────────────────────────────────────────

public record FiltreGrandLivreQuery(
    Guid ExerciceId,
    Guid? CompteId = null,
    string? CompteNumero = null,
    DateTime? DateDebut = null,
    DateTime? DateFin = null,
    int Page = 1,
    int PageSize = 50
);

public record GrandLivreDto(
    Guid ExerciceId,
    int Annee,
    List<CompteGrandLivreDto> Comptes
);

public record CompteGrandLivreDto(
    Guid CompteId,
    string Numero,
    string Libelle,
    decimal SoldeOuverture,
    List<MouvementGrandLivreDto> Mouvements,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal SoldeFinal
);

public record MouvementGrandLivreDto(
    Guid EcritureId,
    string Reference,
    DateTime Date,
    string Journal,
    string Libelle,
    string? TiersNom,
    decimal Debit,
    decimal Credit,
    decimal SoldeCumule
);