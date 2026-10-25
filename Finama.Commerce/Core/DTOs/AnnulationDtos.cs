namespace Finama.Commerce.Core.DTOs;

public record TicketSessionDto(Guid Id, string NumeroTicket, DateTime DateVente, decimal TotalTTC, bool EstAnnule, string? ClientNom, string? MotifAnnulation);

/// <summary>Annulation d'un ticket : motif obligatoire ; si l'utilisateur n'est pas gérant, identifiants d'un gérant.</summary>
public record AnnulerVenteRequest(string Motif, string? GerantEmail, string? GerantMotDePasse);

public record AnnulationVenteDto(Guid VenteId, string NumeroTicket, decimal MontantARendre, string Message);
