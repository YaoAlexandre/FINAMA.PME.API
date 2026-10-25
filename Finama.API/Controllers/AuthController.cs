using Finama.Core.DTOs;
using Finama.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace Finama.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting(RateLimits.Auth)]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>Étape 1 : email + mot de passe. Renvoie RequiresOtp=true ou directement les jetons (appareil de confiance).</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
        => Ok(await _authService.LoginAsync(request));

    /// <summary>Étape 2 : validation du code OTP reçu par email (5 tentatives max par code).</summary>
    [HttpPost("verify-otp")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.CodeOtp))
            return BadRequest(new { message = "L'email et le code OTP sont obligatoires." });

        return Ok(await _authService.VerifierOtpAsync(request));
    }

    /// <summary>
    /// Création directe d'un tenant : réservée au SuperAdmin.
    /// Le parcours public passe par POST /api/demandesacces/soumettre puis approbation.
    /// </summary>
    [HttpPost("register")]
    [Authorize(Policy = Policies.SuperAdmin)]
    public async Task<IActionResult> Register([FromBody] RegisterTenantRequest request)
    {
        var result = await _authService.RegisterTenantAsync(request);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// Renouvelle la session. Un refus est un cas normal (session expirée, autre appareil, autre base) :
    /// il est géré ici en 401 simple, sans remonter comme une erreur (pas d'arrêt du débogueur).
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        try
        {
            return Ok(await _authService.RefreshTokenAsync(request));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { statut = 401, code = "SESSION_EXPIREE", message = ex.Message });
        }
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        if (Guid.TryParse(ObtenirUtilisateurId(), out var utilisateurId))
            await _authService.LogoutAsync(utilisateurId);

        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me() => Ok(new
    {
        Id = ObtenirUtilisateurId(),
        Email = User.FindFirst("email")?.Value ?? User.FindFirst(ClaimTypes.Email)?.Value,
        Nom = User.FindFirst("nom")?.Value,
        Role = User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value,
        TenantId = User.FindFirst("tenant_id")?.Value,
        TenantNom = User.FindFirst("tenant_nom")?.Value,
        DoitChangerMotDePasse = User.HasClaim("pwd_change", "1"),
    });

    /// <summary>Change le mot de passe et renvoie une nouvelle session (Session) sans le claim pwd_change.</summary>
    [HttpPost("changer-mot-de-passe")]
    [Authorize]
    public async Task<IActionResult> ChangerMotDePasse([FromBody] ChangerMotDePasseRequest request)
    {
        if (!Guid.TryParse(ObtenirUtilisateurId(), out var userId))
            return Unauthorized();

        var result = await _authService.DefinirNouveauMotDePasseAsync(userId, request);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    private string? ObtenirUtilisateurId()
        => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
}