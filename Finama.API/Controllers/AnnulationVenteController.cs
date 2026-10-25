using Finama.Commerce.Core.DTOs;
using Finama.Commerce.Infrastructure.Services;
using Finama.Core.Entities;
using Finama.Core.Securite;
using Finama.Infrastructure.Services;
using Finama.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Finama.API.Controllers;

/// <summary>
/// Annulation d'un ticket en caisse. Règle : sans la permission « Annuler un ticket », on ne peut PAS annuler seul.
///  • utilisateur avec la permission (gérant…) : il annule directement ;
///  • sinon : une personne qui a la permission saisit son e-mail et son mot de passe sur la caisse (validation « superviseur »).
/// Motif obligatoire, ticket conservé et marqué « Annulé », auteur de l'autorisation enregistré.
/// </summary>
[ApiController]
[Route("api/commerce/pos")]
[Authorize(Policy = Policies.Commercial)]
public class AnnulationVenteController : ControllerBase
{
    private readonly IPosService _pos;
    private readonly AppDbContext _db;
    private readonly IPermissionsService _permissions;

    public AnnulationVenteController(IPosService pos, AppDbContext db, IPermissionsService permissions)
    {
        _pos = pos;
        _db = db;
        _permissions = permissions;
    }

    /// <summary>Tickets de la session de caisse (pour choisir celui à annuler ou à réimprimer).</summary>
    [HttpGet("sessions/{sessionId:guid}/tickets")]
    public async Task<IActionResult> Tickets(Guid sessionId, CancellationToken ct)
        => Ok(await _pos.ListerTicketsSessionAsync(sessionId, ct));

    [HttpPost("ventes/{venteId:guid}/annuler")]
    [EnableRateLimiting("auth")]   // limite les essais de mot de passe du gérant
    public async Task<IActionResult> Annuler(Guid venteId, [FromBody] AnnulerVenteRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var utilisateurId))
            return Unauthorized();

        Guid autoriseParId;
        if ((await _permissions.DeAsync(User)).Contains(Perm.CaisseAnnuler))
        {
            autoriseParId = utilisateurId;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.GerantEmail) || string.IsNullOrWhiteSpace(request.GerantMotDePasse))
                return BadRequest(new { message = "L'annulation doit être validée par le gérant : e-mail et mot de passe du gérant." });

            var email = request.GerantEmail.Trim().ToLowerInvariant();
            // Filtre multi-entreprise automatique : seul un utilisateur de CETTE entreprise est accepté
            var gerant = await _db.Utilisateurs.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email && u.EstActif, ct);

            if (gerant is null || !BCrypt.Net.BCrypt.Verify(request.GerantMotDePasse, gerant.MotDePasseHash.Trim()))
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Identifiants incorrects : annulation refusée." });

            if (!(await _permissions.DeUtilisateurAsync(gerant.Id)).Contains(Perm.CaisseAnnuler))
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Cette personne n'a pas le droit d'annuler un ticket." });

            autoriseParId = gerant.Id;
        }

        return Ok(await _pos.AnnulerVenteAsync(venteId, request.Motif, autoriseParId, ct));
    }
}
