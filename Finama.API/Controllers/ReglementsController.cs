using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Finama.API.Services;
using Finama.Core.Entities;
using Finama.Core.Securite;
using Finama.Infrastructure.Data;
using Finama.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Finama.API.Controllers;

/// <summary>Règlements des factures (encaissements clients, paiements fournisseurs), totaux ou partiels.</summary>
[ApiController]
[Route("api/factures/{factureId:guid}/reglements")]
[Authorize]
public class ReglementsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILiaisonComptableService _liaison;
    private readonly IPermissionsService _permissions;

    public ReglementsController(AppDbContext db, ILiaisonComptableService liaison, IPermissionsService permissions)
    {
        _db = db;
        _liaison = liaison;
        _permissions = permissions;
    }

    public record ReglementRequest(DateTime DateReglement, decimal Montant, ModeReglement Mode, string? Reference);
    public record ReglementDto(Guid Id, DateTime DateReglement, decimal Montant, ModeReglement Mode, string? Reference, string? Ecriture);

    [HttpGet]
    public async Task<IActionResult> Lister(Guid factureId, CancellationToken ct)
    {
        if (!await AutoriseAsync(lecture: true)) return Forbid();
        var liste = await _db.Set<ReglementFacture>().Where(r => r.FactureId == factureId).OrderBy(r => r.DateReglement).ToListAsync(ct);
        var ecritureIds = liste.Where(r => r.EcritureId.HasValue).Select(r => r.EcritureId!.Value).ToList();
        var refs = await _db.Ecritures.Where(e => ecritureIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.Reference, ct);
        return Ok(liste.Select(r => new ReglementDto(r.Id, r.DateReglement, r.Montant, r.Mode, r.Reference,
            r.EcritureId is { } e ? refs.GetValueOrDefault(e) : null)));
    }

    [HttpPost]
    public async Task<IActionResult> Enregistrer(Guid factureId, [FromBody] ReglementRequest r, CancellationToken ct)
    {
        if (!await AutoriseAsync(lecture: false)) return Forbid();

        var f = await _db.Factures.FirstOrDefaultAsync(x => x.Id == factureId, ct);
        if (f?.TenantId is not { } tenantId) return NotFound(new { message = "Facture introuvable." });
        if (f.Statut is StatutFacture.Brouillon or StatutFacture.Annulee)
            return BadRequest(new { message = "Une facture en brouillon ou annulée ne peut pas être réglée." });

        var montant = Math.Round(r.Montant, 2);
        var solde = Math.Round(f.TotalTTC - f.MontantRegle, 2);
        if (montant <= 0) return BadRequest(new { message = "Le montant doit être supérieur à zéro." });
        if (montant > solde) return BadRequest(new { message = $"Le montant dépasse le reste à régler ({solde:N0})." });
        if (r.DateReglement.Date < f.DateFacture.Date) return BadRequest(new { message = "Le règlement ne peut pas précéder la date de la facture." });
        if (r.DateReglement.Date > DateTime.UtcNow.Date.AddDays(1)) return BadRequest(new { message = "La date du règlement ne peut pas être dans le futur." });
        if (!Enum.IsDefined(r.Mode)) return BadRequest(new { message = "Mode de règlement invalide." });

        var reglement = new ReglementFacture
        {
            TenantId = tenantId, FactureId = f.Id, DateReglement = r.DateReglement.Date, Montant = montant, Mode = r.Mode,
            Reference = string.IsNullOrWhiteSpace(r.Reference) ? null : r.Reference.Trim()[..Math.Min(r.Reference.Trim().Length, 100)]
        };
        _db.Add(reglement);
        f.MontantRegle += montant;
        f.Statut = f.MontantRegle >= f.TotalTTC ? StatutFacture.Regle : StatutFacture.PartiellemntRegle;
        await _db.SaveChangesAsync(ct);

        // Écriture automatique si la liaison est active (sinon : règlement enregistré, comptabilité à saisir à la main)
        string? info = null;
        try
        {
            var res = await _liaison.ComptabiliserReglementAsync(reglement.Id, UtilisateurId(), ct);
            if (res.Comptabilise) info = res.Message;
        }
        catch (Exception ex) { info = $"Règlement enregistré, mais écriture non passée : {ex.Message}"; }

        var reste = f.TotalTTC - f.MontantRegle;
        return Ok(new
        {
            message = (reste <= 0 ? "Facture soldée." : $"Règlement enregistré. Reste à régler : {reste:N0}.") + (info is null ? "" : $" {info}"),
            statut = f.Statut.ToString(),
            montantRegle = f.MontantRegle,
            solde = reste
        });
    }

    private async Task<bool> AutoriseAsync(bool lecture)
    {
        var droits = await _permissions.DeAsync(User);
        return lecture
            ? droits.Contains(Perm.ComptaConsulter) || droits.Contains(Perm.VentesFacturer) || droits.Contains(Perm.VentesTousDevis)
            : droits.Contains(Perm.ComptaSaisir) || droits.Contains(Perm.VentesFacturer);
    }

    private Guid UtilisateurId()
        => Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) ? id : Guid.Empty;
}
