namespace Finama.Core.DTOs;

public record TableauBordDto(
    // ─── Période ──────────────────────────────────────────────────────────────
    int Annee,
    Guid ExerciceId,
    DateTime DateDebut,
    DateTime DateFin,
    string Devise,
    string DeviseSymbole,

    // ─── KPIs principaux ──────────────────────────────────────────────────────
    decimal ChiffreAffaires,
    decimal ChiffreAffairesMoisPrecedent,
    decimal EvolutionCA,               // % d'évolution vs mois précédent

    decimal TotalCharges,
    decimal ResultatNet,
    decimal Tresorerie,

    // ─── Factures ─────────────────────────────────────────────────────────────
    int NombreFacturesEmises,
    int NombreFacturesEnAttente,
    decimal MontantFacturesEnAttente,  // total TTC non réglé

    // ─── Tiers ────────────────────────────────────────────────────────────────
    int NombreClients,
    int NombreFournisseurs,

    // ─── Écritures ────────────────────────────────────────────────────────────
    int NombreEcrituresNonValidees,

    // ─── Graphique CA mensuel (12 derniers mois) ──────────────────────────────
    List<PointCaMensuelDto> CaMensuel,

    // ─── Top 5 clients par CA ─────────────────────────────────────────────────
    List<TopClientDto> TopClients,

    // ─── Dernières factures ───────────────────────────────────────────────────
    List<DerniereFactureDto> DernieresFactures
);

public record PointCaMensuelDto(
    int Mois,
    string LibelleMois,
    decimal ChiffreAffaires,
    decimal Charges
);

public record TopClientDto(
    Guid TiersId,
    string Nom,
    string Code,
    decimal TotalFacture,
    int NombreFactures
);

public record DerniereFactureDto(
    Guid Id,
    string Numero,
    string TiersNom,
    DateTime DateFacture,
    DateTime? DateEcheance,
    decimal TotalTTC,
    decimal Solde,
    string Statut,
    bool EstEnRetard
);
