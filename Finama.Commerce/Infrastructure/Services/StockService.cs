using Finama.Commerce.Application.Common;
using Finama.Commerce.Core.Domain;
using Finama.Commerce.Core.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Finama.Commerce.Infrastructure.Services;

public interface IStockService
{
    Task<StockProduitDto> AjusterStockAsync(AjusterStockRequest request, CancellationToken ct = default);
    Task EntreeInitialeAsync(EntreeInitialeRequest request, CancellationToken ct = default);
    Task<InventaireDto> OuvrirInventaireAsync(OuvrirInventaireRequest request, CancellationToken ct = default);
    Task CompterInventaireAsync(Guid inventaireId, ComptageRequest request, CancellationToken ct = default);
    Task<ResultatInventaireDto> CloturerInventaireAsync(Guid inventaireId, ClotureInventaireRequest request, CancellationToken ct = default);
    Task AnnulerInventaireAsync(Guid inventaireId, CancellationToken ct = default);
    Task<List<StockLotDto>> GetStocksLotsAsync(Guid? magasinId = null, CancellationToken ct = default);
    Task<List<InventaireDto>> GetInventairesAsync(CancellationToken ct = default);
    Task<InventaireDto?> GetInventaireDetailsAsync(Guid inventaireId, CancellationToken ct = default);
}

/// <summary>
/// Règles du stock :
///  • toujours rattaché à un magasin réel (Guid.Empty → magasin principal actif) ;
///  • article suivi par lots : toute entrée crée/complète un lot (numéro + péremption obligatoires),
///    tout retrait vise un lot précis ; StockProduit = somme des lots (+ éventuel reliquat historique sans lot) ;
///  • jamais de stock négatif, sauf paramètre AutoriserStockNegatif (et jamais pour un lot) ;
///  • quantité entière pour les articles à l'unité, décimale pour le vrac.
/// </summary>
public class StockService : IStockService
{
    private readonly ICommerceDbContext _db;
    private readonly ICurrentUser _currentUser;

    public StockService(ICommerceDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ENTRÉE DE STOCK (reprise de l'existant / réception hors module Achats)
    // ═══════════════════════════════════════════════════════════════════════
    public async Task EntreeInitialeAsync(EntreeInitialeRequest request, CancellationToken ct = default)
    {
        var magasin = await ResoudreMagasinAsync(request.MagasinId, ct);
        var produit = await ChargerProduitStockableAsync(request.ProduitId, ct);

        if (request.Quantite <= 0)
            throw new BusinessRuleException("La quantité entrante doit être supérieure à zéro.");
        VerifierQuantiteEntiere(produit, request.Quantite);

        if (request.CoutUnitaire is < 0)
            throw new BusinessRuleException("Le coût unitaire ne peut pas être négatif.");

        string? numeroLot = null;
        if (produit.SuiviLots)
        {
            if (string.IsNullOrWhiteSpace(request.NumeroLot))
                throw new BusinessRuleException($"« {produit.Libelle} » est suivi par lots : le numéro de lot est obligatoire.");
            if (request.DatePeremption is null)
                throw new BusinessRuleException($"« {produit.Libelle} » est suivi par lots : la date de péremption est obligatoire.");
            if (request.DatePeremption.Value < DateOnly.FromDateTime(DateTime.Today))
                throw new BusinessRuleException("Ce lot est déjà périmé : entrée refusée.");

            numeroLot = request.NumeroLot.Trim().ToUpperInvariant();
        }

        await _db.ExecuterAsync(async () =>
        {
            // Rechargé dans la transaction : en cas de nouvelle tentative (concurrence), le suivi EF est réinitialisé
            var article = await _db.Produits.FirstAsync(p => p.Id == produit.Id, ct);

            // Stock total avant entrée (tous magasins) pour le coût moyen pondéré
            var stockTotalAvant = await _db.StocksProduits
                .Where(s => s.ProduitId == produit.Id)
                .SumAsync(s => (decimal?)s.Quantite, ct) ?? 0m;

            var stock = await ObtenirOuCreerStockAsync(magasin.Id, produit.Id, ct);
            stock.Quantite += request.Quantite;
            stock.DateDernierMouvement = DateTime.UtcNow;

            Guid? lotId = null;
            if (numeroLot is not null)
            {
                var lotExistant = await _db.Lots.FirstOrDefaultAsync(l =>
                    l.MagasinId == magasin.Id && l.ProduitId == produit.Id && l.NumeroLot == numeroLot, ct);

                if (lotExistant is not null)
                {
                    // Même lot reçu une 2e fois : on complète, à condition que la péremption soit identique
                    if (lotExistant.DatePeremption != request.DatePeremption)
                        throw new BusinessRuleException(
                            $"Le lot {numeroLot} existe déjà avec une péremption au {lotExistant.DatePeremption:dd/MM/yyyy}. Vérifiez le numéro ou la date.");

                    lotExistant.QuantiteInitiale += request.Quantite;
                    lotExistant.QuantiteRestante += request.Quantite;
                    lotExistant.Statut = StatutLot.Actif;
                    lotId = lotExistant.Id;
                }
                else
                {
                    var lot = new LotStock
                    {
                        TenantId = _currentUser.TenantId,
                        MagasinId = magasin.Id,
                        ProduitId = produit.Id,
                        NumeroLot = numeroLot,
                        DatePeremption = request.DatePeremption,
                        DateReception = DateOnly.FromDateTime(DateTime.UtcNow),
                        QuantiteInitiale = request.Quantite,
                        QuantiteRestante = request.Quantite,
                        CoutUnitaire = request.CoutUnitaire ?? article.PrixAchatMoyen,
                        Statut = StatutLot.Actif
                    };
                    _db.Lots.Add(lot);
                    lotId = lot.Id;
                }
            }

            // Coût moyen unitaire pondéré (CMUP) et dernier prix d'achat
            if (request.CoutUnitaire is { } cout)
            {
                article.PrixAchatMoyen = stockTotalAvant <= 0
                    ? cout
                    : Math.Round((stockTotalAvant * article.PrixAchatMoyen + request.Quantite * cout) / (stockTotalAvant + request.Quantite), 4);
                article.DernierPrixAchat = cout;
            }

            _db.Mouvements.Add(new MouvementStock
            {
                TenantId = _currentUser.TenantId,
                MagasinId = magasin.Id,
                ProduitId = produit.Id,
                LotId = lotId,
                Type = TypeMouvement.EntreeInitiale,
                Quantite = request.Quantite,
                StockApres = stock.Quantite,
                CoutUnitaire = request.CoutUnitaire,
                DateMouvement = DateTime.UtcNow,
                UtilisateurId = _currentUser.UserId
            });

            await _db.SaveChangesAsync(ct);
            return true;
        }, ct);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AJUSTEMENT (casse, perte, péremption, correction)
    // ═══════════════════════════════════════════════════════════════════════
    public async Task<StockProduitDto> AjusterStockAsync(AjusterStockRequest request, CancellationToken ct = default)
    {
        if (request.Delta == 0)
            throw new BusinessRuleException("Indiquez une quantité à ajouter ou à retirer.");
        if (string.IsNullOrWhiteSpace(request.Commentaire))
            throw new BusinessRuleException("Le motif de l'ajustement est obligatoire.");

        var magasin = await ResoudreMagasinAsync(request.MagasinId, ct);
        var produit = await ChargerProduitStockableAsync(request.ProduitId, ct);
        VerifierQuantiteEntiere(produit, request.Delta);

        var autoriserNegatif = await _db.Parametres.Select(p => (bool?)p.AutoriserStockNegatif).FirstOrDefaultAsync(ct) ?? false;

        return await _db.ExecuterAsync(async () =>
        {
            var stock = await ObtenirOuCreerStockAsync(magasin.Id, produit.Id, ct);

            LotStock? lot = null;
            if (request.LotId is { } lotId)
            {
                lot = await _db.Lots.FirstOrDefaultAsync(l => l.Id == lotId && l.ProduitId == produit.Id && l.MagasinId == magasin.Id, ct)
                      ?? throw new NotFoundException("Lot introuvable pour cet article dans ce magasin.");

                if (lot.QuantiteRestante + request.Delta < 0)
                    throw new BusinessRuleException($"Le lot {lot.NumeroLot} ne contient que {lot.QuantiteRestante:0.###} : retrait impossible.");
            }
            else if (produit.SuiviLots)
            {
                if (request.Delta > 0)
                    throw new BusinessRuleException("Article suivi par lots : pour ajouter du stock, faites une entrée de stock avec numéro de lot et date de péremption.");

                // Retrait sans lot autorisé uniquement sur le reliquat historique non rattaché à un lot
                var totalLots = await _db.Lots
                    .Where(l => l.MagasinId == magasin.Id && l.ProduitId == produit.Id && l.Statut == StatutLot.Actif)
                    .SumAsync(l => (decimal?)l.QuantiteRestante, ct) ?? 0m;
                var reliquat = stock.Quantite - totalLots;

                if (-request.Delta > reliquat)
                    throw new BusinessRuleException("Article suivi par lots : choisissez le lot concerné par le retrait.");
            }

            var nouveauStock = stock.Quantite + request.Delta;
            if (nouveauStock < 0 && !autoriserNegatif)
                throw new BusinessRuleException($"Stock disponible : {stock.Quantite:0.###}. Retrait de {-request.Delta:0.###} impossible.");

            stock.Quantite = nouveauStock;
            stock.DateDernierMouvement = DateTime.UtcNow;

            if (lot is not null)
            {
                lot.QuantiteRestante += request.Delta;
                lot.Statut = lot.QuantiteRestante <= 0 ? StatutLot.Epuise : StatutLot.Actif;
            }

            _db.Mouvements.Add(new MouvementStock
            {
                TenantId = _currentUser.TenantId,
                MagasinId = magasin.Id,
                ProduitId = produit.Id,
                LotId = lot?.Id,
                Type = TypeMouvementAjustement(request.Motif, request.Delta),
                Quantite = request.Delta,
                StockApres = stock.Quantite,
                Motif = request.Commentaire.Trim(),
                DateMouvement = DateTime.UtcNow,
                UtilisateurId = _currentUser.UserId
            });

            await _db.SaveChangesAsync(ct);

            var lots = await _db.Lots
                .Where(l => l.MagasinId == magasin.Id && l.ProduitId == produit.Id && l.Statut == StatutLot.Actif)
                .OrderBy(l => l.DatePeremption)
                .Select(l => new LotDto(l.Id, l.NumeroLot, l.DatePeremption, l.QuantiteRestante, l.Statut))
                .ToListAsync(ct);

            return new StockProduitDto(stock.MagasinId, stock.ProduitId, stock.Quantite, lots);
        }, ct);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // NIVEAUX DE STOCK : lots actifs + articles sans lot (+ reliquat historique)
    // ═══════════════════════════════════════════════════════════════════════
    public async Task<List<StockLotDto>> GetStocksLotsAsync(Guid? magasinId = null, CancellationToken ct = default)
    {
        // Nouveau tenant sans magasin : liste vide plutôt qu'une erreur
        if ((magasinId ?? Guid.Empty) == Guid.Empty && !await _db.Magasins.AnyAsync(m => m.EstActif, ct))
            return new List<StockLotDto>();

        var idMagasin = (await ResoudreMagasinAsync(magasinId ?? Guid.Empty, ct)).Id;

        var lots = await _db.Lots
            .Where(l => l.MagasinId == idMagasin && l.Statut == StatutLot.Actif && l.QuantiteRestante > 0)
            .Join(_db.Produits, l => l.ProduitId, p => p.Id, (l, p) => new { Lot = l, p.Libelle })
            .ToListAsync(ct);

        var stocks = await _db.StocksProduits
            .Where(s => s.MagasinId == idMagasin && s.Quantite != 0)
            .Join(_db.Produits.Where(p => p.Type != TypeProduit.Service),
                  s => s.ProduitId, p => p.Id,
                  (s, p) => new { Stock = s, p.Libelle, p.SuiviLots })
            .ToListAsync(ct);

        var resultat = lots.Select(x => new StockLotDto
        {
            Id = x.Lot.Id,
            ProduitId = x.Lot.ProduitId,
            DesignationProduit = x.Libelle,
            NumeroLot = x.Lot.NumeroLot,
            DateExpiration = x.Lot.DatePeremption?.ToDateTime(TimeOnly.MinValue),
            QuantiteDisponible = x.Lot.QuantiteRestante          // décimal : 47,5 kg reste 47,5
        }).ToList();

        foreach (var s in stocks)
        {
            // Article sans lot : la ligne = le stock. Article à lots : seul le reliquat non rattaché apparaît.
            var quantite = s.SuiviLots
                ? s.Stock.Quantite - lots.Where(l => l.Lot.ProduitId == s.Stock.ProduitId).Sum(l => l.Lot.QuantiteRestante)
                : s.Stock.Quantite;

            if (quantite == 0) continue;

            resultat.Add(new StockLotDto
            {
                Id = s.Stock.Id,
                ProduitId = s.Stock.ProduitId,
                DesignationProduit = s.Libelle,
                NumeroLot = "",                                    // "" = ligne sans lot (le front envoie LotId = null)
                DateExpiration = null,
                QuantiteDisponible = quantite
            });
        }

        return resultat
            .OrderBy(r => r.DesignationProduit)
            .ThenBy(r => r.DateExpiration ?? DateTime.MaxValue)
            .ToList();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INVENTAIRES
    // ═══════════════════════════════════════════════════════════════════════
    public async Task<InventaireDto> OuvrirInventaireAsync(OuvrirInventaireRequest request, CancellationToken ct = default)
    {
        var magasin = await ResoudreMagasinAsync(request.MagasinId, ct);

        if (await _db.Inventaires.AnyAsync(i => i.MagasinId == magasin.Id && i.Statut == StatutInventaire.Ouvert, ct))
            throw new BusinessRuleException($"Un inventaire est déjà ouvert sur « {magasin.Nom} ». Clôturez-le avant d'en ouvrir un autre.");

        var produitsQuery = _db.Produits.Where(p => p.EstActif && p.Type != TypeProduit.Service);
        if (request.CategorieId.HasValue)
            produitsQuery = produitsQuery.Where(p => p.CategorieId == request.CategorieId.Value);

        var produits = await produitsQuery.OrderBy(p => p.Libelle).ToListAsync(ct);
        if (produits.Count == 0)
            throw new BusinessRuleException("Aucun article géré en stock à inventorier.");

        var ids = produits.Select(p => p.Id).ToList();
        var stocks = await _db.StocksProduits.Where(s => s.MagasinId == magasin.Id && ids.Contains(s.ProduitId)).ToListAsync(ct);
        var lots = await _db.Lots
            .Where(l => l.MagasinId == magasin.Id && ids.Contains(l.ProduitId) && l.Statut == StatutLot.Actif && l.QuantiteRestante > 0)
            .OrderBy(l => l.DatePeremption)
            .ToListAsync(ct);

        var inventaire = new Inventaire
        {
            TenantId = _currentUser.TenantId,
            MagasinId = magasin.Id,
            Numero = $"INV-{DateTime.UtcNow:yyyyMMddHHmmss}",
            Libelle = string.IsNullOrWhiteSpace(request.Libelle) ? $"Inventaire {magasin.Nom}" : request.Libelle.Trim(),
            Statut = StatutInventaire.Ouvert,
            DateOuverture = DateTime.UtcNow
        };

        foreach (var p in produits)
        {
            var quantiteStock = stocks.FirstOrDefault(s => s.ProduitId == p.Id)?.Quantite ?? 0;

            if (p.SuiviLots)
            {
                // Un comptage par lot : la clôture corrige le lot ET le stock (FEFO cohérent)
                var lotsProduit = lots.Where(l => l.ProduitId == p.Id).ToList();
                foreach (var lot in lotsProduit)
                    inventaire.Lignes.Add(NouvelleLigne(p.Id, lot.Id, lot.QuantiteRestante));

                var reliquat = quantiteStock - lotsProduit.Sum(l => l.QuantiteRestante);
                if (reliquat != 0 || lotsProduit.Count == 0)
                    inventaire.Lignes.Add(NouvelleLigne(p.Id, null, reliquat));
            }
            else
            {
                inventaire.Lignes.Add(NouvelleLigne(p.Id, null, quantiteStock));
            }
        }

        _db.Inventaires.Add(inventaire);
        await _db.SaveChangesAsync(ct);

        var libelles = produits.ToDictionary(p => p.Id, p => p.Libelle);
        var numeros = lots.ToDictionary(l => l.Id, l => l.NumeroLot);

        return new InventaireDto(
            inventaire.Id, inventaire.Numero, inventaire.Libelle, inventaire.Statut, inventaire.DateOuverture, null,
            inventaire.Lignes.Select(l => new InventaireLigneDto(
                l.Id, l.ProduitId, libelles[l.ProduitId], l.LotId,
                l.LotId is { } id ? numeros.GetValueOrDefault(id) : null,
                null, null, null)).ToList());   // comptage à l'aveugle : théorique masqué
    }

    public async Task CompterInventaireAsync(Guid inventaireId, ComptageRequest request, CancellationToken ct = default)
    {
        var inventaire = await _db.Inventaires
            .Include(i => i.Lignes)
            .FirstOrDefaultAsync(i => i.Id == inventaireId, ct)
            ?? throw new NotFoundException("Inventaire introuvable.");

        if (inventaire.Statut != StatutInventaire.Ouvert)
            throw new BusinessRuleException("Cet inventaire est clôturé : le comptage n'est plus modifiable.");

        if (request.Lignes.Any(s => s.QuantiteComptee < 0))
            throw new BusinessRuleException("Une quantité comptée ne peut pas être négative.");

        var produitsVrac = await _db.Produits
            .Where(p => p.Type == TypeProduit.Vrac)
            .Select(p => p.Id)
            .ToListAsync(ct);

        foreach (var saisie in request.Lignes)
        {
            var ligne = inventaire.Lignes.FirstOrDefault(l => l.Id == saisie.LigneId)
                ?? throw new NotFoundException("Ligne d'inventaire introuvable : rechargez la feuille de comptage.");

            if (!produitsVrac.Contains(ligne.ProduitId) && saisie.QuantiteComptee != Math.Floor(saisie.QuantiteComptee))
                throw new BusinessRuleException("Un article vendu à l'unité se compte en nombre entier.");

            ligne.QuantiteComptee = saisie.QuantiteComptee;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<ResultatInventaireDto> CloturerInventaireAsync(Guid inventaireId, ClotureInventaireRequest request, CancellationToken ct = default)
    {
        return await _db.ExecuterAsync(async () =>
        {
            var inventaire = await _db.Inventaires
                .Include(i => i.Lignes)
                .FirstOrDefaultAsync(i => i.Id == inventaireId, ct)
                ?? throw new NotFoundException("Inventaire introuvable.");

            if (inventaire.Statut != StatutInventaire.Ouvert)
                throw new BusinessRuleException("L'inventaire est déjà clôturé.");

            if (!request.TraiterNonComptesCommeZero && inventaire.Lignes.All(l => !l.QuantiteComptee.HasValue))
                throw new BusinessRuleException("Aucune ligne n'a été comptée.");

            var produitIds = inventaire.Lignes.Select(l => l.ProduitId).Distinct().ToList();
            var couts = await _db.Produits.Where(p => produitIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.PrixAchatMoyen, ct);

            int nbEcarts = 0;
            decimal valeurEcart = 0;

            foreach (var ligne in inventaire.Lignes)
            {
                if (!ligne.QuantiteComptee.HasValue && request.TraiterNonComptesCommeZero)
                    ligne.QuantiteComptee = 0;

                if (!ligne.QuantiteComptee.HasValue) continue;

                var ecart = ligne.QuantiteComptee.Value - ligne.QuantiteTheorique;
                if (ecart == 0) continue;

                nbEcarts++;
                valeurEcart += ecart * couts.GetValueOrDefault(ligne.ProduitId);

                var stock = await ObtenirOuCreerStockAsync(inventaire.MagasinId, ligne.ProduitId, ct);
                stock.Quantite += ecart;
                stock.DateDernierMouvement = DateTime.UtcNow;

                if (ligne.LotId is { } lotId)
                {
                    var lot = await _db.Lots.FirstOrDefaultAsync(l => l.Id == lotId, ct);
                    if (lot is not null)
                    {
                        lot.QuantiteRestante = Math.Max(0, lot.QuantiteRestante + ecart);
                        lot.Statut = lot.QuantiteRestante <= 0 ? StatutLot.Epuise : StatutLot.Actif;
                    }
                }

                _db.Mouvements.Add(new MouvementStock
                {
                    TenantId = _currentUser.TenantId,
                    MagasinId = inventaire.MagasinId,
                    ProduitId = ligne.ProduitId,
                    LotId = ligne.LotId,
                    Type = ecart > 0 ? TypeMouvement.EntreeAjustement : TypeMouvement.SortieAjustement,
                    Quantite = ecart,
                    StockApres = stock.Quantite,
                    DocumentType = "INVENTAIRE",
                    DocumentId = inventaire.Id,
                    Motif = $"Régularisation inventaire {inventaire.Numero}",
                    DateMouvement = DateTime.UtcNow,
                    UtilisateurId = _currentUser.UserId
                });
            }

            inventaire.Statut = StatutInventaire.Cloture;
            inventaire.DateCloture = DateTime.UtcNow;
            inventaire.ClotureParId = _currentUser.UserId;

            await _db.SaveChangesAsync(ct);
            return new ResultatInventaireDto(inventaire.Lignes.Count, nbEcarts, Math.Round(valeurEcart, 2));
        }, ct);
    }

    /// <summary>Abandonne un inventaire ouvert : aucun stock n'est modifié, la feuille reste consultable.</summary>
    public async Task AnnulerInventaireAsync(Guid inventaireId, CancellationToken ct = default)
    {
        var inventaire = await _db.Inventaires.FirstOrDefaultAsync(i => i.Id == inventaireId, ct)
            ?? throw new NotFoundException("Inventaire introuvable.");

        if (inventaire.Statut != StatutInventaire.Ouvert)
            throw new BusinessRuleException("Seul un inventaire en cours peut être annulé.");

        inventaire.Statut = StatutInventaire.Annule;
        inventaire.DateCloture = DateTime.UtcNow;
        inventaire.ClotureParId = _currentUser.UserId;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<InventaireDto>> GetInventairesAsync(CancellationToken ct = default)
    {
        var inventaires = await _db.Inventaires
            .AsNoTracking()
            .Include(i => i.Lignes)
            .OrderByDescending(i => i.DateOuverture)
            .ToListAsync(ct);

        return inventaires.Select(inv => new InventaireDto(
            inv.Id, inv.Numero, inv.Libelle, inv.Statut, inv.DateOuverture, inv.DateCloture,
            inv.Lignes.Select(l => new InventaireLigneDto(
                l.Id, l.ProduitId, "", l.LotId, null,
                l.QuantiteTheorique, l.QuantiteComptee, l.Ecart)).ToList()
        )).ToList();
    }

    public async Task<InventaireDto?> GetInventaireDetailsAsync(Guid inventaireId, CancellationToken ct = default)
    {
        var inv = await _db.Inventaires
            .AsNoTracking()
            .Include(i => i.Lignes)
            .FirstOrDefaultAsync(i => i.Id == inventaireId, ct);

        if (inv == null) return null;

        var produitIds = inv.Lignes.Select(l => l.ProduitId).Distinct().ToList();
        var libelles = await _db.Produits
            .Where(p => produitIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Libelle, ct);

        var lotIds = inv.Lignes.Where(l => l.LotId.HasValue).Select(l => l.LotId!.Value).Distinct().ToList();
        var numeros = await _db.Lots
            .Where(l => lotIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => l.NumeroLot, ct);

        return new InventaireDto(
            inv.Id, inv.Numero, inv.Libelle, inv.Statut, inv.DateOuverture, inv.DateCloture,
            inv.Lignes
                .OrderBy(l => libelles.GetValueOrDefault(l.ProduitId, ""))
                .Select(l => new InventaireLigneDto(
                    l.Id,
                    l.ProduitId,
                    libelles.GetValueOrDefault(l.ProduitId, string.Empty),
                    l.LotId,
                    l.LotId is { } id ? numeros.GetValueOrDefault(id) : null,
                    l.QuantiteTheorique,
                    l.QuantiteComptee,
                    l.Ecart))
                .ToList());
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Magasin demandé, ou à défaut le magasin principal actif (jamais Guid.Empty en base).</summary>
    private async Task<Magasin> ResoudreMagasinAsync(Guid magasinId, CancellationToken ct)
    {
        var magasin = magasinId != Guid.Empty
            ? await _db.Magasins.FirstOrDefaultAsync(m => m.Id == magasinId, ct)
            : await _db.Magasins.Where(m => m.EstActif).OrderByDescending(m => m.EstPrincipal).ThenBy(m => m.Nom).FirstOrDefaultAsync(ct);

        if (magasin is null)
            throw new NotFoundException(magasinId == Guid.Empty
                ? "Aucun magasin actif : créez un magasin avant de gérer le stock."
                                : "Le magasin sélectionné n'existe plus. Choisissez votre magasin dans Stock → Magasins & dépôts.");

        if (!magasin.EstActif)
            throw new BusinessRuleException($"Le magasin « {magasin.Nom} » est désactivé.");

        return magasin;
    }

    private async Task<Produit> ChargerProduitStockableAsync(Guid produitId, CancellationToken ct)
    {
        var produit = await _db.Produits.FirstOrDefaultAsync(p => p.Id == produitId, ct)
            ?? throw new NotFoundException("Article introuvable.");

        if (!produit.EstActif)
            throw new BusinessRuleException($"« {produit.Libelle} » est désactivé.");
        if (produit.Type == TypeProduit.Service)
            throw new BusinessRuleException($"« {produit.Libelle} » est un service : il ne se gère pas en stock.");

        return produit;
    }

    private static void VerifierQuantiteEntiere(Produit produit, decimal quantite)
    {
        if (produit.Type != TypeProduit.Vrac && quantite != Math.Floor(quantite))
            throw new BusinessRuleException($"« {produit.Libelle} » se gère à l'unité : saisissez une quantité entière.");
    }

    private async Task<StockProduit> ObtenirOuCreerStockAsync(Guid magasinId, Guid produitId, CancellationToken ct)
    {
        var stock = await _db.StocksProduits.FirstOrDefaultAsync(s => s.MagasinId == magasinId && s.ProduitId == produitId, ct);
        if (stock is not null) return stock;

        stock = new StockProduit
        {
            TenantId = _currentUser.TenantId,
            MagasinId = magasinId,
            ProduitId = produitId,
            Quantite = 0
        };
        _db.StocksProduits.Add(stock);
        return stock;
    }

    private InventaireLigne NouvelleLigne(Guid produitId, Guid? lotId, decimal theorique) => new()
    {
        TenantId = _currentUser.TenantId,
        ProduitId = produitId,
        LotId = lotId,
        QuantiteTheorique = theorique,   // figée à l'ouverture
        QuantiteComptee = null
    };

    private static TypeMouvement TypeMouvementAjustement(MotifAjustement motif, decimal delta) => (motif, delta > 0) switch
    {
        (_, true) => TypeMouvement.EntreeAjustement,
        (MotifAjustement.Casse, false) => TypeMouvement.SortieCasse,
        (MotifAjustement.Peremption, false) => TypeMouvement.SortiePeremption,
        _ => TypeMouvement.SortieAjustement
    };
}