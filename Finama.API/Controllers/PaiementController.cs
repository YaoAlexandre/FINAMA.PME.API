using Finama.Core.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Finama.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaiementController  : ControllerBase
    {
        [HttpPost("webhook-mobilemoney")]
        public async Task<IActionResult> WebhookPaiement(NotificationPaiementDto notification)
        {
            // 1. On vérifie auprès de l'agrégateur que le paiement est bien valide (Statut: SUCCESS)
            if (notification.Statut != "SUCCESS") return BadRequest();

            // 2. On récupère la facture correspondante en base de données
            var facture = await _db.Factures.FirstOrDefaultAsync(f => f.Numero == notification.FactureNumero);
            if (facture == null) return NotFound();

            // 3. On met à jour la facture
            facture.Statut = StatutFacture.Payee;

            // 4. 🚀 GÉNÉRATION DE L'ÉCRITURE D'ENCAISSEMENT COMPTABLE
            // Le client ne doit plus rien, l'argent est arrivé sur le compte Mobile Money de l'entreprise
            // Débit : 521 (Banque/Trésorerie Mobile Money) / Crédit : 411 (Compte Client)
            await _comptaService.GenererEcritureEncaissementAsync(new GenerationEncaissementRequest
            {
                TenantId = facture.TenantId,
                CompteTrésorerie = "521100", // Compte Trésorerie Mobile Money par exemple
                CompteClient = "411100",
                Montant = facture.TotalTTC,
                Libelle = $"Règlement Mobile Money Facture {facture.Numero}"
            });

            await _db.SaveChangesAsync();
            return Ok();
        }
    }
}
