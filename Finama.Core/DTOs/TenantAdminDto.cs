using Finama.Core.Entities;

namespace Finama.Core.DTOs;

public class ChangerStatutTenantRequest
{
    public bool EstActif { get; set; }
}

public record DemandeAccesResponseDto(
    Guid Id,
    string NomEntreprise,
    string NomContact,
    string PrenomContact,
    string Email,
    string Telephone,
    Guid PaysId,
    string Message,
    int Statut, // 0 = EnAttente, 1 = Approuve, 2 = Rejete
    DateTime DateDemande
);

public record ValiderDemandeAccesRequest(
    Guid DemandeId,
    bool Approuver
);

public record TenantAdminDto(
    Guid Id, string Nom, string Email, string Pays,
    string Plan, DateTime? AbonnementExpireAt, bool EstActif, int NbUtilisateurs);

public record UtilisateurAdminDto(
    Guid Id, Guid? TenantId, string Nom, string Prenom, string Email,
    string Role, bool EstActif, DateTime? DerniereConnexionAt);

public record ActiverAbonnementRequest(PlanAbonnement Plan, int DureeJours);