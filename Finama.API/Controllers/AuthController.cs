using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Finama.Core.DTOs;
using Finama.Infrastructure.Services;

namespace Finama.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Connexion — retourne access token + refresh token.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            var result = await _authService.LoginAsync(request);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Valide le code OTP envoyé par e-mail et finalise l'authentification de l'utilisateur.
    /// </summary>
    /// <remarks>
    /// Cette étape intervient après la validation de l'e-mail et du mot de passe. 
    /// Si le code OTP est valide, non expiré et associé au bon utilisateur, l'API génère 
    /// les jetons d'authentification finaux (JWT) et initialise le plan comptable du Tenant si nécessaire.
    /// </remarks>
    /// <param name="request">Le modèle contenant l'e-mail de l'utilisateur et le code OTP reçu.</param>
    /// <returns>Une réponse contenant le token d'accès JWT et les informations de l'entreprise (Tenant).</returns>
    /// <response code="200">Connexion réussie. Les jetons d'authentification sont émis.</response>
    /// <response code="400">Si l'adresse e-mail ou le code OTP fourni est vide ou mal formé.</response>
    /// <response code="401">Si le code est incorrect, expiré, ou si l'entreprise associée est suspendue.</response>
    [HttpPost("verify-otp")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.CodeOtp))
        {
            return BadRequest(new { Message = "L'email et le code OTP sont obligatoires." });
        }

        try
        {
            var response = await _authService.VerifierOtpAsync(request);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Une erreur interne est survenue lors de la validation.", Details = ex.Message });
        }
    }

    /// <summary>
    /// Inscription d'un nouveau tenant (entreprise) + admin + plan comptable OHADA.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterTenantRequest request)
    {
        try
        {
            var result = await _authService.RegisterTenantAsync(request);
            return CreatedAtAction(nameof(Login), result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Renouvelle l'access token via le refresh token.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        try
        {
            var result = await _authService.RefreshTokenAsync(request);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Déconnexion — invalide le refresh token côté serveur.
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                   ?? User.FindFirst("sub")?.Value;

        if (Guid.TryParse(idClaim, out var utilisateurId))
            await _authService.LogoutAsync(utilisateurId);

        return NoContent();
    }

    /// <summary>
    /// Retourne les infos du compte connecté (test d'auth).
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        return Ok(new
        {
            Id = User.FindFirst("sub")?.Value,
            Email = User.FindFirst("email")?.Value,
            Nom = User.FindFirst("nom")?.Value,
            Role = User.FindFirst("role")?.Value,
            TenantId = User.FindFirst("tenant_id")?.Value,
            TenantNom = User.FindFirst("tenant_nom")?.Value,
        });
    }
}