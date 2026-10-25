using Finama.Commerce.Application.Common;
using Finama.Commerce.Core.Domain;
using Finama.Core.Entities;
using Finama.Core.Securite;
using Finama.Infrastructure.Data;
using Finama.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Finama.API.Services;

/// <summary>
/// Toutes les 30 minutes (de 7 h à 20 h, heure de Lomé), pour chaque entreprise ayant au moins un appareil abonné :
///  • stock sous le seuil de réapprovisionnement   → gestion du stock      (1 fois par jour)
///  • lots périmés ou qui périment sous 10 jours   → gestion du stock      (1 fois par jour)
///  • factures de vente en retard de paiement       → compta / facturation  (1 fois par jour)
///  • devis accepté                                  → auteur du devis + « voir tous les devis » (1 fois par devis)
/// Une alerte déjà envoyée n'est jamais répétée (table NotificationsEnvoyees).
/// </summary>
public class AlertesPushService : BackgroundService
{
    private static readonly TimeSpan Intervalle = TimeSpan.FromMinutes(30);
    private const int JoursPeremption = 10;

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<AlertesPushService> _log;

    public AlertesPushService(IServiceScopeFactory scopes, ILogger<AlertesPushService> log)
    {
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        await Task.Delay(TimeSpan.FromMinutes(1), stop);   // laisse l'API finir de démarrer (migrations)
        while (!stop.IsCancellationRequested)
        {
            var heure = DateTime.UtcNow.Hour;   // Lomé = UTC
            if (heure is >= 7 and < 20)
            {
                try { await ExecuterTourAsync(stop); }
                catch (Exception ex) when (ex is not OperationCanceledException) { _log.LogError(ex, "Alertes : tour interrompu"); }
            }
            await Task.Delay(Intervalle, stop);
        }
    }

    private async Task ExecuterTourAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var push = scope.ServiceProvider.GetRequiredService<IPushService>();
        if (!push.EstActif) return;

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenants = await db.Set<AbonnementPush>().IgnoreQueryFilters()
            .Where(a => !a.IsDeleted && a.TenantId != null)
            .Select(a => a.TenantId!.Value).Distinct().ToListAsync(ct);

        foreach (var tenantId in tenants)
        {
            try { await TraiterEntrepriseAsync(scope.ServiceProvider, tenantId, ct); }
            catch (Exception ex) when (ex is not OperationCanceledException) { _log.LogWarning(ex, "Alertes : entreprise {Tenant}", tenantId); }
        }
    }

    private async Task TraiterEntrepriseAsync(IServiceProvider sp, Guid tenantId, CancellationToken ct)
    {
        var db = sp.GetRequiredService<AppDbContext>();
        var commerce = sp.GetRequiredService<ICommerceDbContext>();
        var push = sp.GetRequiredService<IPushService>();
        var permissions = sp.GetRequiredService<IPermissionsService>();

        var modules = await db.Tenants.IgnoreQueryFilters().Where(t => t.Id == tenantId).Select(t => t.Modules).FirstOrDefaultAsync(ct);
        var jour = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var aujourdhui = DateOnly.FromDateTime(DateTime.UtcNow);

        // Utilisateurs abonnés et leurs permissions
        var abonnes = await db.Set<AbonnementPush>().IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId && !a.IsDeleted).Select(a => a.UtilisateurId).Distinct().ToListAsync(ct);
        var droits = new Dictionary<Guid, IReadOnlySet<string>>();
        foreach (var u in abonnes) droits[u] = await permissions.DeUtilisateurAsync(u);
        List<Guid> Ayant(params string[] perms) => droits.Where(d => perms.Any(d.Value.Contains)).Select(d => d.Key).ToList();

        // ── Commerce : stock bas et péremption ──
        if (modules.HasFlag(ModuleFinama.Commerce))
        {
            var gestionnaires = Ayant(Perm.StockGerer, Perm.AdminParametres);
            if (gestionnaires.Count > 0)
            {
                var cle = $"stock-bas:{jour}";
                if (!await DejaEnvoyeAsync(db, tenantId, cle, ct))
                {
                    var bas = await (from s in commerce.StocksProduits.IgnoreQueryFilters()
                                     join p in commerce.Produits.IgnoreQueryFilters() on s.ProduitId equals p.Id
                                     where s.TenantId == tenantId && p.EstActif && p.SeuilReappro > 0
                                     group s by new { p.Id, p.Libelle, p.SeuilReappro } into g
                                     where g.Sum(x => x.Quantite) <= g.Key.SeuilReappro
                                     select new { g.Key.Libelle, Quantite = g.Sum(x => x.Quantite) })
                                    .OrderBy(x => x.Quantite).Take(50).ToListAsync(ct);
                    if (bas.Count > 0)
                    {
                        var detail = string.Join(", ", bas.Take(3).Select(x => $"{x.Libelle} ({x.Quantite:0.##})")) + (bas.Count > 3 ? "…" : "");
                        await EnvoyerEtMemoriserAsync(db, push, tenantId, cle, gestionnaires,
                            new MessagePush($"Stock bas : {bas.Count} article(s) à recommander", detail, "/commerce/stocks", "stock-bas"), ct);
                    }
                }

                cle = $"peremption:{jour}";
                if (!await DejaEnvoyeAsync(db, tenantId, cle, ct))
                {
                    var limite = aujourdhui.AddDays(JoursPeremption);
                    var lots = await commerce.Lots.IgnoreQueryFilters()
                        .Where(l => l.TenantId == tenantId && l.Statut == StatutLot.Actif && l.QuantiteRestante > 0
                                    && l.DatePeremption != null && l.DatePeremption <= limite)
                        .Select(l => new { l.DatePeremption })
                        .ToListAsync(ct);
                    if (lots.Count > 0)
                    {
                        var perimes = lots.Count(l => l.DatePeremption < aujourdhui);
                        var corps = perimes > 0
                            ? $"{perimes} lot(s) déjà périmé(s) à retirer de la vente, {lots.Count - perimes} à écouler sous {JoursPeremption} jours."
                            : $"{lots.Count} lot(s) périment dans moins de {JoursPeremption} jours : pensez à les mettre en avant.";
                        await EnvoyerEtMemoriserAsync(db, push, tenantId, cle, gestionnaires,
                            new MessagePush("Dates de péremption", corps, "/commerce/stocks", "peremption"), ct);
                    }
                }
            }
        }

        // ── Factures de vente en retard ──
        if (modules.HasFlag(ModuleFinama.Comptabilite) || modules.HasFlag(ModuleFinama.GestionCommerciale))
        {
            var destinataires = Ayant(Perm.ComptaValider, Perm.VentesFacturer, Perm.AdminParametres);
            var cle = $"factures-retard:{jour}";
            if (destinataires.Count > 0 && !await DejaEnvoyeAsync(db, tenantId, cle, ct))
            {
                var retards = await db.Factures.IgnoreQueryFilters()
                    .Where(f => f.TenantId == tenantId && !f.IsDeleted && f.Type == TypeFacture.Vente
                                && (f.Statut == StatutFacture.Emise || f.Statut == StatutFacture.PartiellemntRegle)
                                && f.DateEcheance != null && f.DateEcheance < DateTime.UtcNow.Date
                                && f.TotalTTC - f.MontantRegle > 0)
                    .Select(f => f.TotalTTC - f.MontantRegle)
                    .ToListAsync(ct);
                if (retards.Count > 0)
                    await EnvoyerEtMemoriserAsync(db, push, tenantId, cle, destinataires,
                        new MessagePush($"{retards.Count} facture(s) en retard de paiement",
                            $"Reste à encaisser : {retards.Sum():N0} FCFA. Pensez à relancer vos clients.", "/factures", "factures-retard"), ct);
            }
        }

        // ── Devis acceptés (une notification par devis) ──
        if (modules.HasFlag(ModuleFinama.GestionCommerciale))
        {
            var depuis = DateTime.UtcNow.AddDays(-2);
            // TotalTTC est calculé à partir des lignes : on charge les lignes avec le devis
            var acceptes = await db.Devis.IgnoreQueryFilters()
                .Where(d => d.TenantId == tenantId && !d.IsDeleted && d.Statut == StatutDevis.Accepte && d.DateDecision >= depuis)
                .Include(d => d.Lignes)
                .AsNoTracking()
                .ToListAsync(ct);
            var superviseurs = Ayant(Perm.VentesTousDevis);
            foreach (var d in acceptes)
            {
                var cle = $"devis-accepte:{d.Id}";
                if (await DejaEnvoyeAsync(db, tenantId, cle, ct)) continue;
                var destinataires = superviseurs.Append(d.CreePar).Where(droits.ContainsKey).Distinct().ToList();
                await EnvoyerEtMemoriserAsync(db, push, tenantId, cle, destinataires,
                    new MessagePush($"Devis {d.Numero} accepté 🎉", $"Montant : {d.TotalTTC:N0} FCFA. Vous pouvez créer la facture.", $"/ventes/devis/{d.Id}", cle), ct);
            }
        }
    }

    private static Task<bool> DejaEnvoyeAsync(AppDbContext db, Guid tenantId, string cle, CancellationToken ct)
        => db.Set<NotificationEnvoyee>().IgnoreQueryFilters().AnyAsync(n => n.TenantId == tenantId && n.Cle == cle, ct);

    private static async Task EnvoyerEtMemoriserAsync(AppDbContext db, IPushService push, Guid tenantId, string cle,
        IReadOnlyCollection<Guid> destinataires, MessagePush message, CancellationToken ct)
    {
        if (destinataires.Count == 0) return;
        await push.EnvoyerAsync(tenantId, destinataires, message, ct);
        db.Set<NotificationEnvoyee>().Add(new NotificationEnvoyee { TenantId = tenantId, Cle = cle });
        await db.SaveChangesAsync(ct);
    }
}
