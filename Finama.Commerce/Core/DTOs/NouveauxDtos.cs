using Finama.Commerce.Core.Domain;

namespace Finama.Commerce.Core.DTOs;

// ── Clientèle ──
public record ClientDto(
    Guid Id, string Code, string Nom, TypeClient Type,
    string? Telephone, string? Email, string? Adresse, string? Nif,
    decimal PlafondCredit, decimal SoldeCredit, int PointsFidelite, bool EstActif);

public record SaveClientRequest(
    string Nom, TypeClient Type,
    string? Telephone, string? Email, string? Adresse, string? Nif,
    decimal PlafondCredit);

public record AchatClientDto(Guid VenteId, string NumeroTicket, DateTime DateVente, decimal TotalTTC);

public record MouvementFideliteDto(TypeMouvementFidelite Type, int Points, int SoldeApres, DateTime DateMouvement, string? Motif);

public record ClientDetailDto(
    ClientDto Client, decimal TotalAchats,
    List<AchatClientDto> DerniersAchats, List<MouvementFideliteDto> Fidelite);

// ── Caisse ──
public record CaisseDto(Guid Id, string Code, string Libelle, bool SessionOuverte);

/// <summary>Clôture Z à l'aveugle : le caissier ne déclare que ce qu'il a compté ; le théorique est calculé côté serveur.</summary>
public record ClotureAveugleLigneRequest(ModePaiement Mode, decimal MontantDeclare);
public record ClotureAveugleRequest(Guid SessionCaisseId, List<ClotureAveugleLigneRequest> Lignes, string? Commentaire);