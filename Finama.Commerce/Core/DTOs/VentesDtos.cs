using Finama.Commerce.Core.Domain;

namespace Finama.Commerce.Core.DTOs;

public record OuvrirSessionRequest(Guid CaisseId, decimal FondDeCaisse);
public record SessionDto(Guid Id, Guid CaisseId, Guid MagasinId, DateTime DateOuverture, decimal FondDeCaisse, StatutSession Statut);

public record DeclarationDto(ModePaiement Mode, decimal Montant);
/// <summary>Réponse volontairement sans aucun chiffre : le caissier ne voit jamais le théorique ni l'écart.</summary>
public record ClotureRecueDto(Guid SessionId, DateTime DateCloture, string Message);
public record ValiderClotureRequest(string? Commentaire);

public record RapportZLigneDto(ModePaiement Mode, decimal Theorique, decimal? Declare, decimal? Ecart);
public record RapportZDto(Guid SessionId, string Caisse, Guid CaissierId, DateTime DateOuverture, DateTime? DateCloture, StatutSession Statut,
                          decimal FondDeCaisse, int NbTickets, int NbTicketsAnnules, decimal TotalVentesTTC, decimal TotalRemises,
                          IReadOnlyList<RapportZLigneDto> Lignes, decimal? EcartTotal, bool EcartSignale,
                          Guid? ValideParId, DateTime? DateValidation, string? Commentaire);

/// <summary>Un superviseur choisi à l'écran + son PIN : le verrouillage après échecs se fait par superviseur.</summary>
public record AutorisationSuperviseurDto(Guid SuperviseurId, string Pin);
public record VenteLigneRequest(Guid ProduitId, decimal Quantite, decimal? RemisePourcent);
public record PaiementRequest(ModePaiement Mode, decimal Montant, string? Operateur, string? Reference);
public record CreerVenteRequest(Guid ClientRequestId, Guid SessionCaisseId, Guid? ClientId, IReadOnlyList<VenteLigneRequest> Lignes,
                                IReadOnlyList<PaiementRequest> Paiements, AutorisationSuperviseurDto? Autorisation);
//public record AnnulerVenteRequest(Guid SuperviseurId, string Pin, string Motif);

public record TicketLigneDto(Guid ProduitId, string Libelle, string? CodeBarres, decimal Quantite, decimal PrixUnitaireTTC,
                             decimal RemisePourcent, decimal RemiseMontant, decimal TauxTva, decimal MontantTTC);
public record TicketPaiementDto(ModePaiement Mode, decimal Montant, decimal MontantRecu, string? Operateur, string? Reference);
public record TicketDto(Guid Id, string NumeroTicket, DateTime DateVente, StatutVente Statut, decimal TotalHT, decimal TotalTVA,
                        decimal TotalRemise, decimal TotalTTC, decimal MontantPaye, decimal RenduMonnaie,
                        IReadOnlyList<TicketLigneDto> Lignes, IReadOnlyList<TicketPaiementDto> Paiements);

public record VentesQuery(Guid? SessionCaisseId, DateTime? Du, DateTime? Au, int Page = 1, int PageSize = 50);
public record VenteListeDto(Guid Id, string NumeroTicket, DateTime DateVente, StatutVente Statut, decimal TotalTTC, int NbArticles);

public record DefinirPinRequest(Guid UtilisateurId, string Pin);
