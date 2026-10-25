using Finama.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Finama.Infrastructure.Services;

/// <summary>Logo de l'entreprise (data URL stockée dans Tenants.LogoDataUrl) converti en octets pour les PDF.</summary>
public static class LogoHelper
{
    public static byte[]? Decoder(string? dataUrl)
    {
        if (string.IsNullOrWhiteSpace(dataUrl)) return null;
        var i = dataUrl.IndexOf(";base64,", StringComparison.Ordinal);
        if (i < 0) return null;
        try { return Convert.FromBase64String(dataUrl[(i + 8)..]); }
        catch (FormatException) { return null; }
    }

    public static async Task<(string Nom, byte[]? Logo)> EnteteAsync(AppDbContext db, Guid? tenantId, CancellationToken ct = default)
    {
        if (tenantId is not { } id) return ("", null);
        var t = await db.Tenants.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new { x.Nom, x.LogoDataUrl })
            .FirstOrDefaultAsync(ct);
        return t is null ? ("", null) : (t.Nom, Decoder(t.LogoDataUrl));
    }
}
