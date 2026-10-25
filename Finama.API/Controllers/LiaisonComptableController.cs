using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Finama.API.Services;
using Finama.Commerce.Application.Common;
using Finama.Commerce.Core.Domain;
using Finama.Core.Entities;
using Finama.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Finama.API.Controllers;

/// <summary>Réglages de la liaison modules → comptabilité, contrôle des comptes et rattrapage des pièces.</summary>
[ApiController]
[Route("api/compta/liaison")]
[Authorize(Policy = Policies.AdminTenant)]
public class LiaisonComptableController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICommerceDbContext _commerce;
    private readonly ILiaisonComptableService _liaison;
    private readonly ITenantContext _tenant;

    public LiaisonComptableController(AppDbContext db, ICommerceDbContext commerce, ILiaisonComptableService liaison, ITenantContext tenant)
    {
        _db = db;
        _commerce = commerce;
        _liaison = liaison;
        _tenant = tenant;
    }

    public record ParametresDto(bool LiaisonActive, bool ValiderAutomatiquement, DateTime? ActiveDepuis,
        string CompteVentes, string CompteAchats, string CompteTvaCollectee, string CompteTvaDeductible,
        string CompteClients, string CompteFournisseurs, string CompteBanque, string CompteCaisse,
        string CompteMobileMoney, string CompteClientsCaisse, string CompteRemisesFidelite);

    [HttpGet]
    public async Task<IActionResult> Lire(CancellationToken ct)
    {
        if (_tenant.TenantId is not { } t) return BadRequest(new { message = "Aucune entreprise." });
        var p = await _liaison.ParametresAsync(t, ct);

        // Chaque compte réglé est vérifié dans le plan comptable (numéro + libellé trouvés, ou « introuvable »)
        var comptes = new Dictionary<string, string>();
        foreach (var (cle, numero) in new[] { ("CompteVentes", p.CompteVentes), ("CompteAchats", p.CompteAchats), ("CompteTvaCollectee", p.CompteTvaCollectee),
                     ("CompteTvaDeductible", p.CompteTvaDeductible), ("CompteClients", p.CompteClients), ("CompteFournisseurs", p.CompteFournisseurs),
                     ("CompteBanque", p.CompteBanque), ("CompteCaisse", p.CompteCaisse), ("CompteMobileMoney", p.CompteMobileMoney),
                     ("CompteClientsCaisse", p.CompteClientsCaisse), ("CompteRemisesFidelite", p.CompteRemisesFidelite) })
            comptes[cle] = (await _liaison.VerifierCompteAsync(t, numero, ct)).Affichage;

        // Pièces pas encore comptabilisées (pour le bouton « Rattraper »)
        var faites = _db.Set<PieceComptabilisee>();
        var facturesEnAttente = await _db.Factures.CountAsync(f => f.Statut != StatutFacture.Brouillon && f.Statut != StatutFacture.Annulee
                                                                 && !faites.Any(x => x.Source == "FACTURE" && x.SourceId == f.Id), ct);
        var reglementsEnAttente = await _db.Set<ReglementFacture>().CountAsync(r => !faites.Any(x => x.Source == "REGLEMENT" && x.SourceId == r.Id), ct);
        var caissesFaites = await faites.Where(x => x.Source == "CAISSE").Select(x => x.SourceId).ToListAsync(ct);
        var caissesEnAttente = await _commerce.Sessions.CountAsync(s => s.Statut == StatutSession.Cloturee && !caissesFaites.Contains(s.Id), ct);

        return Ok(new
        {
            parametres = new ParametresDto(p.LiaisonActive, p.ValiderAutomatiquement, p.ActiveDepuis, p.CompteVentes, p.CompteAchats,
                p.CompteTvaCollectee, p.CompteTvaDeductible, p.CompteClients, p.CompteFournisseurs, p.CompteBanque, p.CompteCaisse,
                p.CompteMobileMoney, p.CompteClientsCaisse, p.CompteRemisesFidelite),
            comptes,
            enAttente = new { factures = facturesEnAttente, reglements = reglementsEnAttente, caisses = caissesEnAttente }
        });
    }

    [HttpPut]
    public async Task<IActionResult> Enregistrer([FromBody] ParametresDto r, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } t) return BadRequest(new { message = "Aucune entreprise." });
        var numeros = new[] { r.CompteVentes, r.CompteAchats, r.CompteTvaCollectee, r.CompteTvaDeductible, r.CompteClients, r.CompteFournisseurs,
                              r.CompteBanque, r.CompteCaisse, r.CompteMobileMoney, r.CompteClientsCaisse, r.CompteRemisesFidelite };
        if (numeros.Any(n => string.IsNullOrWhiteSpace(n) || n.Trim().Length > 20 || !n.Trim().All(char.IsDigit)))
            return BadRequest(new { message = "Chaque compte doit être un numéro (chiffres uniquement)." });

        // À l'activation, tous les comptes doivent exister : sinon la première facture échouerait
        if (r.LiaisonActive)
        {
            var manquants = new List<string>();
            foreach (var n in numeros.Distinct())
                if ((await _liaison.VerifierCompteAsync(t, n, ct)).Id is null) manquants.Add(n);
            if (manquants.Count > 0)
                return BadRequest(new { message = $"Comptes introuvables dans le plan comptable : {string.Join(", ", manquants)}. Créez-les ou changez les numéros avant d'activer." });
        }

        var p = await _db.Set<ParametresComptables>().FirstOrDefaultAsync(ct);
        if (p is null) { p = new ParametresComptables { TenantId = t }; _db.Add(p); }
        if (r.LiaisonActive && !p.LiaisonActive) p.ActiveDepuis = DateTime.UtcNow;
        p.LiaisonActive = r.LiaisonActive;
        p.ValiderAutomatiquement = r.ValiderAutomatiquement;
        p.CompteVentes = r.CompteVentes.Trim(); p.CompteAchats = r.CompteAchats.Trim();
        p.CompteTvaCollectee = r.CompteTvaCollectee.Trim(); p.CompteTvaDeductible = r.CompteTvaDeductible.Trim();
        p.CompteClients = r.CompteClients.Trim(); p.CompteFournisseurs = r.CompteFournisseurs.Trim();
        p.CompteBanque = r.CompteBanque.Trim(); p.CompteCaisse = r.CompteCaisse.Trim(); p.CompteMobileMoney = r.CompteMobileMoney.Trim();
        p.CompteClientsCaisse = r.CompteClientsCaisse.Trim(); p.CompteRemisesFidelite = r.CompteRemisesFidelite.Trim();
        await _db.SaveChangesAsync(ct);

        return Ok(new { message = r.LiaisonActive
            ? "Liaison comptable activée : les prochaines factures, règlements et clôtures de caisse passeront leurs écritures automatiquement."
            : "Liaison comptable désactivée." });
    }

    /// <summary>Comptabilise les pièces existantes qui ne l'ont pas encore été (optionnel : à partir d'une date).</summary>
    [HttpPost("rattrapage")]
    public async Task<IActionResult> Rattraper([FromQuery] DateTime? depuis, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } t) return BadRequest(new { message = "Aucune entreprise." });
        if (!(await _liaison.ParametresAsync(t, ct)).LiaisonActive)
            return BadRequest(new { message = "Activez d'abord la liaison comptable." });
        var res = await _liaison.RattraperAsync(t, UtilisateurId(), depuis, ct);
        return Ok(new
        {
            message = $"{res.Factures} facture(s), {res.Reglements} règlement(s) et {res.Caisses} clôture(s) de caisse comptabilisés.",
            res.Erreurs
        });
    }

    private Guid UtilisateurId()
        => Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) ? id : Guid.Empty;
}
