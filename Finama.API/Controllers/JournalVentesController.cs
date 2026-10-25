using Finama.Commerce.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finama.API.Controllers;

/// <summary>
/// Fin de journée de la caisse. Réservé à l'administrateur : le journal révèle les montants
/// théoriques, qui doivent rester cachés au caissier pendant la clôture à l'aveugle.
/// </summary>
[ApiController]
[Route("api/commerce/pos/journal")]
[Authorize(Policy = Policies.FinJournee)]
public class JournalVentesController : ControllerBase
{
    private readonly IJournalVentesService _journal;

    public JournalVentesController(IJournalVentesService journal)
    {
        _journal = journal;
    }

    /// <summary>GET api/commerce/pos/journal?date=2026-10-16&amp;magasinId=… (date et magasin facultatifs).</summary>
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] DateOnly? date, [FromQuery] Guid? magasinId, CancellationToken ct)
        => Ok(await _journal.ObtenirJournalAsync(date ?? DateOnly.FromDateTime(DateTime.UtcNow), magasinId, ct));
}
