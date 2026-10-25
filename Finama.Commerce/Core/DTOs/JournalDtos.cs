using Finama.Commerce.Core.Domain;

namespace Finama.Commerce.Core.DTOs;

/// <summary>Fin de journée d'un magasin : encaissements, tickets, articles vendus, crédits.</summary>
public record JournalJourDto(
    DateOnly Date,
    Guid MagasinId,
    string Magasin,
    int NbTickets,
    int NbAnnules,
    decimal TotalTTC,
    decimal TotalHT,
    decimal TotalTVA,
    decimal TotalRemises,
    decimal CoutTotal,
    decimal MargeBrute,
    IReadOnlyList<EncaissementModeDto> Encaissements,
    IReadOnlyList<SessionJourDto> Sessions,
    IReadOnlyList<TicketJourDto> Tickets,
    IReadOnlyList<ArticleVenduDto> Articles,
    IReadOnlyList<CreditJourDto> Credits);

public record EncaissementModeDto(ModePaiement Mode, decimal Montant, int NbOperations);

public record SessionJourDto(
    Guid SessionId,
    string Caisse,
    DateTime DateOuverture,
    DateTime? DateCloture,
    decimal FondDeCaisse,
    bool EstCloturee,
    IReadOnlyList<ClotureLigneJourDto> Lignes,
    decimal? EcartTotal,
    string? Commentaire);

public record ClotureLigneJourDto(ModePaiement Mode, decimal Theorique, decimal? Declare, decimal? Ecart);

public record TicketJourDto(
    Guid Id,
    string Numero,
    DateTime DateVente,
    string Caisse,
    string? Client,
    decimal TotalHT,
    decimal TotalTVA,
    decimal TotalRemise,
    decimal TotalTTC,
    decimal RenduMonnaie,
    bool EstAnnule,
    string? MotifAnnulation,
    IReadOnlyList<TicketLigneJourDto> Lignes,
    IReadOnlyList<TicketPaiementJourDto> Paiements);

public record TicketLigneJourDto(string Libelle, decimal Quantite, decimal PrixUnitaireTTC, decimal RemiseMontant, decimal MontantTTC);

public record TicketPaiementJourDto(ModePaiement Mode, decimal Montant, string? Reference);

public record ArticleVenduDto(Guid ProduitId, string Libelle, decimal Quantite, decimal ChiffreAffairesTTC, decimal ChiffreAffairesHT, decimal Cout, decimal Marge);

public record CreditJourDto(Guid ClientId, string Client, string? Telephone, decimal CreditDuJour, decimal SoldeTotalDu, decimal Plafond);
