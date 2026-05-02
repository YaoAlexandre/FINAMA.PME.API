namespace Finama.Core.DTOs;

public record LoginRequest(
    string Email,
    string MotDePasse
);

public record RegisterTenantRequest(
    string NomEntreprise,
    string Email,
    string MotDePasse,
    string NomAdministrateur,
    string PrenomAdministrateur,
    string DeviseBase = "STN",
    string PlanComptableCode = "OHADA"
);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpireAt,
    string NomUtilisateur,
    string Email,
    string Role,
    Guid TenantId,
    string NomEntreprise
);

public record RefreshTokenRequest(
    string AccessToken,
    string RefreshToken
);
