using Finama.Core.Entities;
using Finama.Infrastructure.Data;
using Finama.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Finama.API.Services;

/// <summary>
/// Alertes de pilotage pour le SuperAdmin (appareils « plateforme »), vérifiées toutes les 5 minutes :
///  • nouvelle demande d'accès (site vitrine)        → à rappeler vite          (1 fois par demande)
///  • nouvelle entreprise inscrite                   → bienvenue / démo         (1 fois par entreprise)
///  • abonnement qui expire dans 7 jours ou moins    → relance avant blocage    (1 fois par échéance)
///  • abonnement expiré (depuis moins de 3 jours)    → contacter le client      (1 fois par échéance)
///  • rafale d'erreurs serveur (≥ 5 en 15 minutes)   → intervenir               (au plus 1 fois par heure, jour et nuit)
/// Les alertes commerciales sont envoyées de 7 h à 21 h (heure de Lomé) ; elles attendent le matin sinon.
/// </summary>
public class AlertesPlateformeService : BackgroundService
{
    private static readonly TimeSpan Intervalle = TimeSpan.FromMinutes(5);
    private const int SeuilErreurs = 5;

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<AlertesPlateformeService> _log;

    public AlertesPlateformeService(IServiceScopeFactory scopes, ILogger<AlertesPlateformeService> log)
    {
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        await Task.Delay(TimeSpan.FromSeconds(90), stop);
        while (!stop.IsCancellationRequested)
        {
            try { await ExecuterTourAsync(stop); }
            catch (Exception ex) when (ex is not OperationCanceledException) { _log.LogError(ex, "Alertes plateforme : tour interrompu"); }
            await Task.Delay(Intervalle, stop);
        }
    }

    private async Task ExecuterTourAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var push = scope.ServiceProvider.GetRequiredService<IPushService>();
        if (!push.EstActif) return;
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Destinataires : SuperAdmins actifs ayant au moins un appareil « plateforme »
        var superAdmins = await db.Utilisateurs.IgnoreQueryFilters()
            .Where(u => u.Role == RoleUtilisateur.SuperAdmin && u.EstActif && !u.IsDeleted)
            .Select(u => u.Id).ToListAsync(ct);
        var destinataires = await db.Set<AbonnementPush>().IgnoreQueryFilters()
            .Where(a => a.TenantId == null && !a.IsDeleted && superAdmins.Contains(a.UtilisateurId))
            .Select(a => a.UtilisateurId).Distinct().ToListAsync(ct);
        if (destinataires.Count == 0) return;

        var maintenant = DateTime.UtcNow;

        // ── Rafale d'erreurs serveur (24 h/24) ──
        var (nbErreurs, chemin) = CompteurErreurs.Depuis(maintenant.AddMinutes(-15));
        if (nbErreurs >= SeuilErreurs)
            await EnvoyerUneFoisAsync(db, push, destinataires, $"erreurs:{maintenant:yyyy-MM-dd-HH}",
                new MessagePush($"⚠️ {nbErreurs} erreurs serveur en 15 min",
                    $"Route la plus touchée : {chemin}. Consultez les journaux de l'API.", "/superadmin/tenants", "erreurs-serveur"), ct);

        if (maintenant.Hour is < 7 or >= 21) return;   // le commercial attend le matin

        // ── Nouvelles demandes d'accès ──
        var demandes = await db.DemandesAcces.IgnoreQueryFilters()
            .Where(d => d.Statut == StatutDemandeAcces.EnAttente && d.DateDemande >= maintenant.AddDays(-2))
            .Select(d => new { d.Id, d.NomEntreprise, d.PrenomContact, d.NomContact, d.Telephone })
            .ToListAsync(ct);
        foreach (var d in demandes)
            await EnvoyerUneFoisAsync(db, push, destinataires, $"demande:{d.Id}",
                new MessagePush($"Nouvelle demande d'accès : {d.NomEntreprise}",
                    $"{d.PrenomContact} {d.NomContact} · {d.Telephone}. Rappelez dans l'heure !", "/superadmin/demandes-acces", $"demande-{d.Id}"), ct);

        // ── Nouvelles entreprises ──
        var nouvelles = await db.Tenants.IgnoreQueryFilters()
            .Where(t => !t.IsDeleted && t.DateCreation >= maintenant.AddDays(-1))
            .Select(t => new { t.Id, t.Nom, t.Email })
            .ToListAsync(ct);
        foreach (var t in nouvelles)
            await EnvoyerUneFoisAsync(db, push, destinataires, $"tenant-nouveau:{t.Id}",
                new MessagePush($"Nouvelle entreprise : {t.Nom}", $"Inscrite aujourd'hui ({t.Email}). Proposez une démo de bienvenue.",
                    "/superadmin/tenants", $"tenant-{t.Id}"), ct);

        // ── Abonnements : bientôt expirés / expirés ──
        var echeances = await db.Tenants.IgnoreQueryFilters()
            .Where(t => !t.IsDeleted && t.EstActif && t.AbonnementExpireAt != null
                        && t.AbonnementExpireAt >= maintenant.AddDays(-3) && t.AbonnementExpireAt <= maintenant.AddDays(7))
            .Select(t => new { t.Id, t.Nom, Expire = t.AbonnementExpireAt!.Value, t.Plan })
            .ToListAsync(ct);
        foreach (var t in echeances)
        {
            var jour = t.Expire.ToString("yyyy-MM-dd");
            if (t.Expire > maintenant)
            {
                var jours = Math.Max(1, (int)Math.Ceiling((t.Expire - maintenant).TotalDays));
                await EnvoyerUneFoisAsync(db, push, destinataires, $"abo-bientot:{t.Id}:{jour}",
                    new MessagePush($"Abonnement de {t.Nom} : fin dans {jours} jour(s)",
                        $"Formule {t.Plan}, échéance le {t.Expire:dd/MM}. Relancez avant le blocage.", "/superadmin/tenants", $"abo-{t.Id}"), ct);
            }
            else
            {
                await EnvoyerUneFoisAsync(db, push, destinataires, $"abo-expire:{t.Id}:{jour}",
                    new MessagePush($"Abonnement expiré : {t.Nom}",
                        $"Depuis le {t.Expire:dd/MM}. Ses utilisateurs ne peuvent plus se connecter : contactez-le.", "/superadmin/tenants", $"abo-{t.Id}"), ct);
            }
        }
    }

    private static async Task EnvoyerUneFoisAsync(AppDbContext db, IPushService push, IReadOnlyCollection<Guid> destinataires,
        string cle, MessagePush message, CancellationToken ct)
    {
        var deja = await db.Set<NotificationEnvoyee>().IgnoreQueryFilters().AnyAsync(n => n.TenantId == null && n.Cle == cle, ct);
        if (deja) return;
        await push.EnvoyerAsync(null, destinataires, message, ct);
        db.Set<NotificationEnvoyee>().Add(new NotificationEnvoyee { TenantId = null, Cle = cle });
        await db.SaveChangesAsync(ct);
    }
}
