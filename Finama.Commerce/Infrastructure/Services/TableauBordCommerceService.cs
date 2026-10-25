using Finama.Commerce.Application.Common;
using Finama.Commerce.Core.Domain;
using Finama.Commerce.Core.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Finama.Commerce.Infrastructure.Services;

public interface ITableauBordCommerceService
{
    Task<TableauBordCommerceDto> ObtenirAsync(CancellationToken ct = default);
}

public class TableauBordCommerceService : ITableauBordCommerceService
{
    private const int JoursAlertePeremption = 30;

    private readonly ICommerceDbContext _db;
    private readonly ICurrentUser _currentUser;

    public TableauBordCommerceService(ICommerceDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<TableauBordCommerceDto> ObtenirAsync(CancellationToken ct = default)
    {
        var parametres = await _db.ParametresOuDefautAsync(ct);
        var userId = _currentUser.UserId;
        var debutJour = DateTime.UtcNow.Date;          // Lomé = UTC+0 toute l'année
        var finJour = debutJour.AddDays(1);

        var nbMagasins = await _db.Magasins.CountAsync(m => m.EstActif, ct);
        var nbPdv = await _db.Magasins.CountAsync(m => m.EstActif && m.Type == TypeMagasin.PointDeVente, ct);
        var nbArticles = await _db.Produits.CountAsync(p => p.EstActif, ct);
        var nbClients = await _db.Clients.CountAsync(c => c.EstActif, ct);

        var sessionOuverte = await _db.Sessions.AnyAsync(s => s.CaissierId == userId && s.Statut == StatutSession.Ouverte, ct);

        var ventesDuJour = _db.Ventes.Where(v => v.Statut == StatutVente.Validee && v.DateVente >= debutJour && v.DateVente < finJour);
        var nbVentes = await ventesDuJour.CountAsync(ct);
        var ca = await ventesDuJour.SumAsync(v => (decimal?)v.TotalTTC, ct) ?? 0;

        // Stock cumulé tous magasins <= seuil de réapprovisionnement (articles suivis en stock uniquement).
        var sousSeuil = await _db.Produits.CountAsync(p =>
            p.EstActif && p.Type != TypeProduit.Service && p.SeuilReappro > 0 &&
            (_db.StocksProduits.Where(s => s.ProduitId == p.Id).Sum(s => (decimal?)s.Quantite) ?? 0) <= p.SeuilReappro, ct);

        var perimes = 0;
        var proches = 0;
        if (parametres.UtiliseLots)
        {
            var aujourdhui = DateOnly.FromDateTime(DateTime.UtcNow);
            var limite = aujourdhui.AddDays(JoursAlertePeremption);
            var lotsActifs = _db.Lots.Where(l => l.Statut == StatutLot.Actif && l.QuantiteRestante > 0 && l.DatePeremption != null);

            perimes = await lotsActifs.CountAsync(l => l.DatePeremption < aujourdhui, ct);
            proches = await lotsActifs.CountAsync(l => l.DatePeremption >= aujourdhui && l.DatePeremption <= limite, ct);
        }

        var enCredit = _db.Clients.Where(c => c.EstActif && c.SoldeCredit > 0);
        var nbEnCredit = await enCredit.CountAsync(ct);
        var creances = await enCredit.SumAsync(c => (decimal?)c.SoldeCredit, ct) ?? 0;

        return new TableauBordCommerceDto(
            !string.IsNullOrWhiteSpace(parametres.Activite), parametres.UtiliseLots,
            nbMagasins, nbPdv, nbArticles, nbClients,
            sessionOuverte, nbVentes, ca,
            sousSeuil, perimes, proches,
            creances, nbEnCredit);
    }
}
