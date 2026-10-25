using System.Net;
using System.Text.Json;
using Finama.Core.Entities;
using Finama.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebPush;

namespace Finama.Infrastructure.Services;

/// <summary>Clés VAPID (identifient Finama auprès des services de notification Apple, Google, Microsoft, Mozilla).</summary>
public class PushOptions
{
    public string VapidPublicKey { get; set; } = string.Empty;
    public string VapidPrivateKey { get; set; } = string.Empty;
    public string Sujet { get; set; } = "mailto:contact@finamaweb.com";   // adresse de contact exigée par la norme
    public bool EstConfigure => !string.IsNullOrWhiteSpace(VapidPublicKey) && !string.IsNullOrWhiteSpace(VapidPrivateKey);
}

public sealed record MessagePush(string Titre, string Corps, string Url, string Tag);

public interface IPushService
{
    bool EstActif { get; }
    string ClePublique { get; }

    /// <summary>Dernière erreur d'envoi (affichée par le bouton « Tester »).</summary>
    string? DerniereErreur { get; }

    /// <summary>
    /// Envoie le message à tous les appareils de ces utilisateurs. Renvoie le nombre d'appareils atteints.
    /// tenantId = null : appareils « plateforme » (SuperAdmin).
    /// </summary>
    Task<int> EnvoyerAsync(Guid? tenantId, IEnumerable<Guid> utilisateurIds, MessagePush message, CancellationToken ct = default);
}

public class PushService : IPushService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly AppDbContext _db;
    private readonly PushOptions _options;
    private readonly ILogger<PushService> _log;
    private readonly WebPushClient _client = new();

    public PushService(AppDbContext db, IOptions<PushOptions> options, ILogger<PushService> log)
    {
        _db = db;
        _options = options.Value;
        _log = log;
    }

    public bool EstActif => _options.EstConfigure;
    public string ClePublique => _options.VapidPublicKey;
    public string? DerniereErreur { get; private set; }

    public async Task<int> EnvoyerAsync(Guid? tenantId, IEnumerable<Guid> utilisateurIds, MessagePush message, CancellationToken ct = default)
    {
        DerniereErreur = null;
        if (!EstActif)
        {
            DerniereErreur = "Notifications non configurées sur le serveur (clés VAPID absentes).";
            return 0;
        }

        var ids = utilisateurIds.Distinct().ToList();
        if (ids.Count == 0) return 0;

        var abonnements = await _db.Set<AbonnementPush>().IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId && !a.IsDeleted && ids.Contains(a.UtilisateurId))
            .ToListAsync(ct);

        if (abonnements.Count == 0)
        {
            DerniereErreur = "Aucun appareil abonné pour ce compte : activez les notifications sur cet appareil.";
            _log.LogInformation("Notification « {Titre} » : aucun appareil abonné (entreprise {Tenant})", message.Titre, tenantId);
            return 0;
        }

        var vapid = new VapidDetails(_options.Sujet, _options.VapidPublicKey, _options.VapidPrivateKey);
        var charge = JsonSerializer.Serialize(new { titre = message.Titre, corps = message.Corps, url = message.Url, tag = message.Tag }, Json);
        var atteints = 0;

        foreach (var a in abonnements)
        {
            try
            {
                await _client.SendNotificationAsync(new PushSubscription(a.Endpoint, a.P256dh, a.Auth), charge, vapid, ct);
                a.DernierEnvoi = DateTime.UtcNow;
                atteints++;
                _log.LogInformation("Notification « {Titre} » envoyée à {Appareil}", message.Titre, a.Appareil);
            }
            catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
            {
                DerniereErreur = $"Abonnement expiré sur {a.Appareil} : désactivez puis réactivez les notifications sur cet appareil.";
                _log.LogWarning("Notification : abonnement expiré ({Code}) pour {Appareil}, retiré", (int)ex.StatusCode, a.Appareil);
                _db.Remove(a);   // appareil désabonné ou application désinstallée
            }
            catch (WebPushException ex)
            {
                var detail = "";
                try { if (ex.HttpResponseMessage is { } rep) detail = await rep.Content.ReadAsStringAsync(ct); } catch { }
                DerniereErreur = $"Refusée par le service de notification ({(int)ex.StatusCode} {ex.StatusCode}) {detail}".Trim();
                _log.LogWarning("Notification refusée pour {Appareil} : {Code} {Detail}", a.Appareil, (int)ex.StatusCode, detail);
            }
            catch (Exception ex)
            {
                DerniereErreur = $"Erreur d'envoi : {ex.Message}";
                _log.LogWarning(ex, "Notification non remise à l'appareil {Appareil}", a.Appareil);
            }
        }

        await _db.SaveChangesAsync(ct);
        return atteints;
    }
}