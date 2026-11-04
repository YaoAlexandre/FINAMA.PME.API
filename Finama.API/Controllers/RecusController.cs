using System.Security.Cryptography;
using System.Text;
using Finama.API.Exports;
using Finama.Commerce.Application.Common;
using Finama.Commerce.Core.Domain;
using Finama.Core.Temps;
using Finama.Infrastructure.Data;
using Finama.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Finama.API.Controllers;

/// <summary>
/// Reçu de caisse en PDF.
///  • GET  api/commerce/pos/ventes/{id}/recu       : le PDF (caisse connectée) — partagé comme fichier sur WhatsApp
///  • POST api/commerce/pos/ventes/{id}/lien-recu  : lien public signé, valable 30 jours (appareils sans partage de fichier)
///  • GET  api/public/recus/{id}?e=…&amp;s=…            : ouverture du lien par le client, sans compte
/// Le lien est signé (HMAC) : impossible de deviner ou de modifier l'adresse pour voir le ticket d'un autre.
/// </summary>
[ApiController]
public class RecusController : ControllerBase
{
    private static readonly TimeSpan ValiditeLien = TimeSpan.FromDays(30);

    private readonly ICommerceDbContext _commerce;
    private readonly AppDbContext _db;
    private readonly byte[] _cle;

    public RecusController(ICommerceDbContext commerce, AppDbContext db, IOptions<JwtSettings> jwt)
    {
        _commerce = commerce;
        _db = db;
        _cle = Encoding.UTF8.GetBytes("recu:" + jwt.Value.SecretKey);
    }

    [HttpGet("api/commerce/pos/ventes/{venteId:guid}/recu")]
    [Authorize(Policy = Policies.Commercial)]
    public async Task<IActionResult> Pdf(Guid venteId, CancellationToken ct)
    {
        var vente = await ChargerAsync(_commerce.Ventes, venteId, ct);   // filtre entreprise automatique
        if (vente is null) return NotFound(new { message = "Ticket introuvable." });
        return File(await GenererAsync(vente, ct), "application/pdf", $"Recu_{vente.NumeroTicket}.pdf");
    }

    [HttpPost("api/commerce/pos/ventes/{venteId:guid}/lien-recu")]
    [Authorize(Policy = Policies.Commercial)]
    public async Task<IActionResult> Lien(Guid venteId, CancellationToken ct)
    {
        var vente = await _commerce.Ventes.AsNoTracking().Where(v => v.Id == venteId)
            .Select(v => new { v.Id, v.TenantId }).FirstOrDefaultAsync(ct);
        if (vente?.TenantId is not { } tenantId) return NotFound(new { message = "Ticket introuvable." });

        var expire = DateTimeOffset.UtcNow.Add(ValiditeLien).ToUnixTimeSeconds();
        var url = $"{Request.Scheme}://{Request.Host}/api/public/recus/{vente.Id:N}?e={expire}&s={Signer(vente.Id, tenantId, expire)}";
        return Ok(new { url, expireLe = DateTimeOffset.FromUnixTimeSeconds(expire).UtcDateTime });
    }

    [HttpGet("api/public/recus/{venteId:guid}")]
    [AllowAnonymous]
    [EnableRateLimiting("public")]
    public async Task<IActionResult> Public(Guid venteId, [FromQuery(Name = "e")] long expire, [FromQuery(Name = "s")] string? signature, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(signature) || DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expire)
            return NotFound(new { message = "Ce lien a expiré. Demandez un nouveau reçu au magasin." });

        // Pas de compte côté client : on lit hors filtre entreprise, mais seulement après contrôle de la signature
        var vente = await ChargerAsync(_commerce.Ventes.IgnoreQueryFilters(), venteId, ct);
        if (vente?.TenantId is not { } tenantId
            || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Signer(venteId, tenantId, expire)), Encoding.ASCII.GetBytes(signature)))
            return NotFound(new { message = "Reçu introuvable." });

        var pdf = await GenererAsync(vente, ct, horsFiltre: true);
        Response.Headers.CacheControl = "private, max-age=3600";
        return File(pdf, "application/pdf");   // affiché directement dans le navigateur du téléphone
    }

    // ─── Outils ──────────────────────────────────────────────────────────────

    private static Task<Vente?> ChargerAsync(IQueryable<Vente> ventes, Guid id, CancellationToken ct)
        => ventes.AsNoTracking().Include(v => v.Lignes).Include(v => v.Paiements).AsSplitQuery()
                 .FirstOrDefaultAsync(v => v.Id == id, ct);

    private async Task<byte[]> GenererAsync(Vente v, CancellationToken ct, bool horsFiltre = false)
    {
        var magasins = horsFiltre ? _commerce.Magasins.IgnoreQueryFilters() : _commerce.Magasins;
        var caisses = horsFiltre ? _commerce.Caisses.IgnoreQueryFilters() : _commerce.Caisses;
        var clients = horsFiltre ? _commerce.Clients.IgnoreQueryFilters() : _commerce.Clients;

        var magasin = await magasins.AsNoTracking().FirstOrDefaultAsync(m => m.Id == v.MagasinId, ct);
        var caisse = await caisses.AsNoTracking().Where(c => c.Id == v.CaisseId).Select(c => c.Libelle).FirstOrDefaultAsync(ct);
        var client = v.ClientId is { } cid ? await clients.AsNoTracking().Where(c => c.Id == cid).Select(c => c.Nom).FirstOrDefaultAsync(ct) : null;
        var t = await _db.Tenants.AsNoTracking().IgnoreQueryFilters().Where(x => x.Id == v.TenantId)
            .Select(x => new { x.Nom, x.Adresse, x.Telephone, x.NumeroFiscal, x.LogoDataUrl, Devise = x.Pays.DeviseSymbole, PaysIso = x.Pays.CodeISO })
            .FirstOrDefaultAsync(ct);

        var donnees = new RecuPdf.Donnees(
            t?.Nom ?? "", LogoNoirBlanc.Convertir(LogoHelper.Decoder(t?.LogoDataUrl)), t?.Adresse, t?.Telephone, t?.NumeroFiscal,
            magasin?.Nom ?? "", magasin?.Adresse, magasin?.Telephone, caisse ?? "Caisse",
            v.NumeroTicket, FuseauPays.VersLocal(v.DateVente, t?.PaysIso), client, v.Statut == StatutVente.Annulee,
            v.Lignes.Select(l => new RecuPdf.Ligne(l.Libelle, l.Quantite, l.PrixUnitaireTTC, l.RemiseMontant, l.MontantTTC)).ToList(),
            v.Paiements.Select(p => new RecuPdf.Reglement(LibelleMode(p.Mode), p.Montant, p.Reference)).ToList(),
            v.TotalHT, v.TotalTVA, v.TotalRemise, v.TotalTTC, v.RenduMonnaie,
            string.IsNullOrWhiteSpace(t?.Devise) ? "FCFA" : t!.Devise);

        return RecuPdf.Generer(donnees);
    }

    private string Signer(Guid venteId, Guid tenantId, long expire)
    {
        var hash = HMACSHA256.HashData(_cle, Encoding.UTF8.GetBytes($"{venteId:N}.{tenantId:N}.{expire}"));
        return Convert.ToBase64String(hash, 0, 20).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string LibelleMode(ModePaiement m) => m switch
    {
        ModePaiement.Especes => "Espèces",
        ModePaiement.Carte => "Carte",
        ModePaiement.MobileMoney => "Mobile Money",
        ModePaiement.Credit => "Crédit client",
        ModePaiement.Points => "Points fidélité",
        _ => m.ToString()
    };
}
