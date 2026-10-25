using System.Text.RegularExpressions;
using Finama.Commerce.Application.Common;
using Finama.Commerce.Core.DTOs;
using Finama.Commerce.Infrastructure.Services;
using Finama.Core.Entities;
using Finama.Core.Securite;
using Finama.Infrastructure.Data;
using Finama.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Finama.API.Controllers;

/// <summary>
/// Synchronisation des ventes faites en caisse SANS réseau.
/// Règles : jamais de doublon (clé d'idempotence du ticket), une vente déjà encaissée n'est jamais refusée pour
/// cause de stock (le stock peut passer en négatif : le gérant est alors prévenu), crédit client et points interdits.
/// </summary>
[ApiController]
[Route("api/commerce/pos/ventes/hors-ligne")]
[Authorize(Policy = Policies.Caisse)]
public partial class VentesHorsLigneController : ControllerBase
{
    private readonly IPosService _pos;
    private readonly ICommerceDbContext _commerce;
    private readonly AppDbContext _db;
    private readonly IPushService _push;
    private readonly IPermissionsService _permissions;
    private readonly ITenantContext _tenant;

    public VentesHorsLigneController(IPosService pos, ICommerceDbContext commerce, AppDbContext db,
        IPushService push, IPermissionsService permissions, ITenantContext tenant)
    {
        _pos = pos;
        _commerce = commerce;
        _db = db;
        _push = push;
        _permissions = permissions;
        _tenant = tenant;
    }

    [GeneratedRegex(@"^HL-[A-Z0-9]{1,10}-\d{6}-\d{1,6}$")]
    private static partial Regex FormatNumero();

    [HttpPost]
    public async Task<IActionResult> Synchroniser([FromBody] VenteHorsLigneRequest r, CancellationToken ct)
    {
        if (r?.Vente is null) return BadRequest(new { message = "Vente manquante." });
        if (string.IsNullOrWhiteSpace(r.NumeroHorsLigne) || !FormatNumero().IsMatch(r.NumeroHorsLigne))
            return BadRequest(new { message = "Numéro de ticket hors connexion invalide." });

        var ticket = await _pos.CreerVenteAsync(r.Vente, ct, new OptionsVente(true, r.DateVenteLocale, r.NumeroHorsLigne));

        // Articles passés en stock négatif à cause de ventes faites sans réseau : le gérant est prévenu
        var ids = r.Vente.Lignes.Select(l => l.ProduitId).Distinct().ToList();
        var negatifs = (await (from s in _commerce.StocksProduits
                               join p in _commerce.Produits on s.ProduitId equals p.Id
                               where ids.Contains(s.ProduitId) && s.Quantite < 0
                               select new { p.Libelle, s.Quantite })
                              .ToListAsync(ct))
                       .Select(x => $"{x.Libelle} ({x.Quantite:0.###})")
                       .ToList();

        if (negatifs.Count > 0 && _push.EstActif && _tenant.TenantId is { } tenantId)
        {
            try
            {
                var abonnes = await _db.Set<AbonnementPush>().Select(a => a.UtilisateurId).Distinct().ToListAsync(ct);
                var destinataires = new List<Guid>();
                foreach (var u in abonnes)
                {
                    var droits = await _permissions.DeUtilisateurAsync(u);
                    if (droits.Contains(Perm.StockGerer) || droits.Contains(Perm.AdminParametres)) destinataires.Add(u);
                }
                await _push.EnvoyerAsync(tenantId, destinataires, new MessagePush(
                    "Stock négatif après des ventes hors connexion",
                    string.Join(", ", negatifs.Take(3)) + (negatifs.Count > 3 ? "…" : "") + " — vérifiez le stock ou saisissez la réception.",
                    "/commerce/stocks", "stock-negatif"), ct);
            }
            catch { /* la vente est enregistrée : l'alerte est un plus */ }
        }

        return Ok(new ResultatVenteHorsLigne(ticket.Id, ticket.NumeroTicket, r.NumeroHorsLigne, negatifs));
    }
}
