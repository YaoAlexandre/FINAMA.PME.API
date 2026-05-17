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