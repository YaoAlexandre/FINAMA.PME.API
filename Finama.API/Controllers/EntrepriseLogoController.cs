using Finama.Infrastructure.Data;
using Finama.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Finama.API.Controllers;

/// <summary>
/// Logo de l'entreprise (tickets de caisse, factures, en-tête). Stocké en data URL PNG/JPEG/WebP, 300 Ko max.
/// Lecture : tout utilisateur de l'entreprise (le caissier en a besoin pour le ticket). Modification : AdminTenant.
/// SVG refusé : il peut contenir du script.
/// </summary>
[ApiController]
[Route("api/entreprise/logo")]
[Authorize]
public class EntrepriseLogoController : ControllerBase
{
    private const int TailleMaxOctets = 300 * 1024;

    private static readonly Dictionary<string, byte[][]> SignaturesParType = new()
    {
        ["image/png"] = [[0x89, 0x50, 0x4E, 0x47]],
        ["image/jpeg"] = [[0xFF, 0xD8, 0xFF]],
        ["image/webp"] = [[0x52, 0x49, 0x46, 0x46]],   // "RIFF" (contrôle "WEBP" ci-dessous)
    };

    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;

    public EntrepriseLogoController(AppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        if (_tenantContext.TenantId is not { } tenantId) return Ok(new { dataUrl = (string?)null });

        var logo = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => t.LogoDataUrl)
            .FirstOrDefaultAsync(ct);

        return Ok(new { dataUrl = logo });
    }

    [HttpPut]
    [Authorize(Policy = Policies.AdminTenant)]
    public async Task<IActionResult> Put([FromBody] LogoRequest request, CancellationToken ct)
    {
        var tenant = await ChargerTenantAsync(ct);
        if (tenant is null) return NotFound(new { message = "Entreprise introuvable." });

        var erreur = Valider(request.DataUrl);
        if (erreur is not null) return BadRequest(new { message = erreur });

        tenant.LogoDataUrl = request.DataUrl;
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "Logo enregistré." });
    }

    [HttpDelete]
    [Authorize(Policy = Policies.AdminTenant)]
    public async Task<IActionResult> Delete(CancellationToken ct)
    {
        var tenant = await ChargerTenantAsync(ct);
        if (tenant is null) return NotFound(new { message = "Entreprise introuvable." });

        tenant.LogoDataUrl = null;
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "Logo supprimé." });
    }

    private async Task<Finama.Core.Entities.Tenant?> ChargerTenantAsync(CancellationToken ct)
        => _tenantContext.TenantId is { } id
            ? await _db.Tenants.FirstOrDefaultAsync(t => t.Id == id, ct)
            : null;

    /// <summary>Contrôle le type déclaré, la taille et la signature réelle du fichier (pas de faux PNG).</summary>
    private static string? Valider(string? dataUrl)
    {
        if (string.IsNullOrWhiteSpace(dataUrl) || !dataUrl.StartsWith("data:image/", StringComparison.Ordinal))
            return "Format attendu : image PNG, JPEG ou WebP.";

        var virgule = dataUrl.IndexOf(";base64,", StringComparison.Ordinal);
        if (virgule < 0) return "Image mal encodée.";

        var type = dataUrl[5..virgule];
        if (!SignaturesParType.TryGetValue(type, out var signatures))
            return "Format non accepté : utilisez PNG, JPEG ou WebP (le SVG est refusé).";

        byte[] octets;
        try { octets = Convert.FromBase64String(dataUrl[(virgule + 8)..]); }
        catch (FormatException) { return "Image mal encodée."; }

        if (octets.Length == 0) return "Image vide.";
        if (octets.Length > TailleMaxOctets) return "Logo trop lourd : 300 Ko maximum.";

        var signatureOk = signatures.Any(s => octets.Length >= s.Length && octets.AsSpan(0, s.Length).SequenceEqual(s));
        if (type == "image/webp")
            signatureOk &= octets.Length > 12 && octets.AsSpan(8, 4).SequenceEqual("WEBP"u8);

        return signatureOk ? null : "Le contenu du fichier ne correspond pas à une image valide.";
    }

    public record LogoRequest(string? DataUrl);
}
