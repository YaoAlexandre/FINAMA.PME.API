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
    Guid PaysId                        // ID depuis GET /api/pays
);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpireAt,
    string NomUtilisateur,
    string Email,
    string Role,
    Guid TenantId,
    string NomEntreprise,
    string Pays,
    string Devise,
    string DeviseSymbole,
    decimal TauxTVA,
    bool RequiresOtp,
    string? Message
);

public record RefreshTokenRequest(
    string AccessToken,
    string RefreshToken
);

public record VerifyOtpRequest(string Email, string CodeOtp);