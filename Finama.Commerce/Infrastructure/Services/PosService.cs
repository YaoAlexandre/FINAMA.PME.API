using Finama.Commerce.Application.Common;
using Finama.Commerce.Core.Domain;
using Finama.Commerce.Core.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Finama.Commerce.Infrastructure.Services;

public interface IPosService
{
    Task<List<CaisseDto>> ListerCaissesAsync(Guid magasinId, CancellationToken ct = default);
    Task<SessionDto?> SessionActiveAsync(Guid magasinId, CancellationToken ct = default);
    Task<SessionDto> OuvrirSessionAsync(OuvrirSessionRequest request, CancellationToken ct = default);
    Task<TicketDto> CreerVenteAsync(CreerVenteRequest request, CancellationToken ct = default, OptionsVente? options = null);
    Task<ClotureRecueDto> CloturerSessionAsync(ClotureAveugleRequest request, CancellationToken ct = default);
    Task<RapportZDto> ObtenirRapportZAsync(Guid sessionId, CancellationToken ct = default);
    Task<List<TicketSessionDto>> ListerTicketsSessionAsync(Guid sessionId, CancellationToken ct = default);
    Task<AnnulationVenteDto> AnnulerVenteAsync(Guid venteId, string motif, Guid autoriseParId, CancellationToken ct = default);
}

public class PosService : IPosService
{
    // Modes qui passent physiquement par le tiroir/terminal : seuls ceux-là se déclarent à la clôture.
    private static readonly ModePaiement[] ModesPhysiques = { ModePaiement.Especes, ModePaiement.Carte, ModePaiement.MobileMoney };

    private readonly ICommerceDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IPinHasher _pinHasher;

    private const int TentativesPinMax = 5;
    private static readonly TimeSpan DureeVerrouPin = TimeSpan.FromMinutes(15);

    public PosService(ICommerceDbContext db, ICurrentUser currentUser, IPinHasher pinHasher)
    {
        _db = db;
        _currentUser = currentUser;
        _pinHasher = pinHasher;
    }

    // ─────────────────────────────── CAISSES & SESSIONS ───────────────────────────────

    public async Task<List<CaisseDto>> ListerCaissesAsync(Guid magasinId, CancellationToken ct = default)
    {
        var magasin = await _db.Magasins.FirstOrDefaultAsync(m => m.Id == magasinId && m.EstActif, ct)
            ?? throw new NotFoundException("Magasin introuvable.");

        if (magasin.Type != TypeMagasin.PointDeVente)
            throw new BusinessRuleException("Ce magasin n'est pas un point de vente.");

        var caisses = await _db.Caisses
            .Where(c => c.MagasinId == magasinId && c.EstActif)
            .OrderBy(c => c.Code)
            .ToListAsync(ct);

        // Premier accès (ou magasin créé avant ce correctif) : on crée la caisse par défaut.
        if (caisses.Count == 0)
        {
            var defaut = new Caisse { TenantId = _currentUser.TenantId, MagasinId = magasinId, Code = "C01", Libelle = "Caisse 1" };
            _db.Caisses.Add(defaut);
            await _db.SaveChangesAsync(ct);
            caisses.Add(defaut);
        }

        var ouvertes = await _db.Sessions
            .Where(s => s.MagasinId == magasinId && s.Statut == StatutSession.Ouverte)
            .Select(s => s.CaisseId)
            .ToListAsync(ct);

        return caisses.Select(c => new CaisseDto(c.Id, c.Code, c.Libelle, ouvertes.Contains(c.Id))).ToList();
    }

    public async Task<SessionDto?> SessionActiveAsync(Guid magasinId, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId;
        var session = await _db.Sessions
            .Where(s => s.MagasinId == magasinId && s.CaissierId == userId && s.Statut == StatutSession.Ouverte)
            .OrderByDescending(s => s.DateOuverture)
            .FirstOrDefaultAsync(ct);

        return session == null
            ? null
            : new SessionDto(session.Id, session.CaisseId, session.MagasinId, session.DateOuverture, session.FondDeCaisse, session.Statut);
    }

    public async Task<SessionDto> OuvrirSessionAsync(OuvrirSessionRequest request, CancellationToken ct = default)
    {
        if (request.FondDeCaisse < 0)
            throw new BusinessRuleException("Le fond de caisse ne peut pas être négatif.");

        var caisse = await _db.Caisses.FirstOrDefaultAsync(c => c.Id == request.CaisseId && c.EstActif, ct)
            ?? throw new NotFoundException("Caisse introuvable.");

        var sessionActive = await _db.Sessions.AnyAsync(s => s.CaisseId == request.CaisseId && s.Statut == StatutSession.Ouverte, ct);
        if (sessionActive)
            throw new BusinessRuleException("Une session est déjà ouverte sur cette caisse.");

        var session = new SessionCaisse
        {
            TenantId = _currentUser.TenantId,
            CaisseId = request.CaisseId,
            MagasinId = caisse.MagasinId,
            CaissierId = _currentUser.UserId,
            DateOuverture = DateTime.UtcNow,
            FondDeCaisse = request.FondDeCaisse,
            Statut = StatutSession.Ouverte
        };

        _db.Sessions.Add(session);
        await _db.SaveChangesAsync(ct);

        return new SessionDto(session.Id, session.CaisseId, session.MagasinId, session.DateOuverture, session.FondDeCaisse, session.Statut);
    }

    // ─────────────────────────────────── VENTE ───────────────────────────────────

    public async Task<TicketDto> CreerVenteAsync(CreerVenteRequest request, CancellationToken ct = default, OptionsVente? options = null)
    {
        // Le PIN est vérifié HORS transaction : un PIN faux lève une exception qui annule la transaction de vente,
        // et le compteur d'échecs (verrouillage) serait annulé avec elle. Ici il est enregistré immédiatement.
        Guid? superviseurValideId = null;
        var parametresPrealables = await _db.ParametresOuDefautAsync(ct);
        if (request.Lignes != null && request.Lignes.Any(l => (l.RemisePourcent ?? 0) > parametresPrealables.SeuilRemiseSansPinPourcent))
            superviseurValideId = await VerifierSuperviseurAsync(request.Autorisation, ct);

        return await _db.ExecuterAsync(async () =>
        {
            // Idempotence : un renvoi réseau ne crée jamais de doublon.
            var existante = await _db.Ventes
                .Include(v => v.Lignes)
                .Include(v => v.Paiements)
                .FirstOrDefaultAsync(v => v.ClientRequestId == request.ClientRequestId, ct);
            if (existante != null) return ChargerTicketDto(existante);

            if (request.Lignes == null || !request.Lignes.Any())
                throw new BusinessRuleException("Le ticket est vide.");
            if (request.Paiements == null || !request.Paiements.Any())
                throw new BusinessRuleException("Aucun paiement renseigné.");

            var session = await _db.Sessions.FirstOrDefaultAsync(s => s.Id == request.SessionCaisseId, ct)
                ?? throw new NotFoundException("Session de caisse introuvable.");
            if (session.Statut != StatutSession.Ouverte)
                throw new BusinessRuleException("La session de caisse n'est pas ouverte.");
            if (session.CaissierId != _currentUser.UserId)
                throw new BusinessRuleException("Cette session de caisse appartient à un autre caissier.");

            var parametres = await _db.ParametresOuDefautAsync(ct);
            var horsLigne = options?.HorsLigne == true;

            // Vente hors connexion : la date est celle du ticket papier (heure du téléphone, bornée)
            var maintenant = DateTime.UtcNow;
            if (horsLigne && options!.DateVente is { } dateLocale)
            {
                var utc = dateLocale.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dateLocale, DateTimeKind.Utc) : dateLocale.ToUniversalTime();
                if (utc > maintenant.AddMinutes(10) || utc < maintenant.AddDays(-7))
                    throw new BusinessRuleException("Date de vente hors connexion invalide (heure du téléphone erronée ou vente de plus de 7 jours).");
                if (utc < session.DateOuverture.AddMinutes(-5))
                    throw new BusinessRuleException("Cette vente hors connexion est antérieure à l'ouverture de la caisse.");
                maintenant = utc;
            }
            if (horsLigne && request.Paiements.Any(p => p.Mode is ModePaiement.Credit or ModePaiement.Points))
                throw new BusinessRuleException("Le crédit client et les points de fidélité ne sont pas utilisables hors connexion.");

            Client? client = null;
            if (request.ClientId.HasValue)
            {
                client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == request.ClientId.Value && c.EstActif, ct)
                    ?? throw new NotFoundException("Client introuvable ou inactif.");
            }

            var vente = new Vente
            {
                TenantId = _currentUser.TenantId,
                MagasinId = session.MagasinId,
                CaisseId = session.CaisseId,
                SessionCaisseId = session.Id,
                CaissierId = _currentUser.UserId,
                ClientRequestId = request.ClientRequestId,
                EstHorsLigne = horsLigne,
                NumeroHorsLigne = horsLigne ? options!.NumeroHorsLigne : null,
                NumeroTicket = await ProchainNumeroTicketAsync(parametres.PrefixeTicket, maintenant.Year, ct),
                DateVente = maintenant,
                ClientId = client?.Id,
                Statut = StatutVente.Validee
            };

            // Chargement groupé : produits, codes principaux, soldes du magasin.
            var ids = request.Lignes.Select(l => l.ProduitId).Distinct().ToList();
            var produits = await _db.Produits.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
            var stocks = await _db.StocksProduits
                .Where(s => s.MagasinId == session.MagasinId && ids.Contains(s.ProduitId))
                .ToDictionaryAsync(s => s.ProduitId, ct);
            var codesListe = await _db.CodesBarres.Where(c => ids.Contains(c.ProduitId) && c.EstPrincipal).ToListAsync(ct);
            var codes = codesListe.GroupBy(c => c.ProduitId).ToDictionary(g => g.Key, g => g.First().Code);

            decimal totalHT = 0, totalTVA = 0, totalTTC = 0, totalRemise = 0;

            foreach (var l in request.Lignes)
            {
                if (l.Quantite <= 0)
                    throw new BusinessRuleException("Quantité invalide.");
                if (!produits.TryGetValue(l.ProduitId, out var produit) || !produit.EstActif)
                    throw new NotFoundException($"Produit {l.ProduitId} introuvable ou inactif.");
                // Quantité décimale autorisée au poids/à la mesure et pour toute unité autre que la pièce (heures, mètres...).
                if (!produit.EstVenduAuPoids && produit.Unite == UniteMesure.Piece && l.Quantite != decimal.Truncate(l.Quantite))
                    throw new BusinessRuleException($"« {produit.Libelle} » ne se vend qu'à l'unité.");

                // ── Prix & remise : le prix vient TOUJOURS du serveur ──
                var puTTC = produit.PrixVenteTTC;
                var remisePct = l.RemisePourcent ?? 0;

                if (remisePct < 0 || remisePct > parametres.RemiseMaxPourcent)
                    throw new BusinessRuleException($"Remise refusée : maximum {parametres.RemiseMaxPourcent}%.");

                if (remisePct > parametres.SeuilRemiseSansPinPourcent)
                {
                    if (superviseurValideId == null)
                        throw new BusinessRuleException($"Une remise de {remisePct}% nécessite la validation d'un superviseur.");
                    vente.RemiseAutoriseeParId = superviseurValideId;
                }

                var prixApresRemise = puTTC * (1 - remisePct / 100m);
                var mtTTC = MoneyMath.Arrondir(prixApresRemise * l.Quantite);
                var (mtHT, mtTVA) = MoneyMath.Decomposer(mtTTC, produit.TauxTva);
                var remiseMontant = MoneyMath.Arrondir(puTTC * l.Quantite - mtTTC);

                totalTTC += mtTTC;
                totalHT += mtHT;
                totalTVA += mtTVA;
                totalRemise += remiseMontant;

                vente.Lignes.Add(new VenteLigne
                {
                    TenantId = _currentUser.TenantId,
                    ProduitId = produit.Id,
                    Libelle = produit.Libelle,
                    CodeBarres = codes.GetValueOrDefault(produit.Id),
                    Quantite = l.Quantite,
                    PrixUnitaireTTC = puTTC,
                    RemisePourcent = remisePct,
                    RemiseMontant = remiseMontant,
                    TauxTva = produit.TauxTva,
                    MontantHT = mtHT,
                    MontantTVA = mtTVA,
                    MontantTTC = mtTTC,
                    CoutUnitaire = produit.PrixAchatMoyen
                });

                // Un service n'a pas de stock : ni contrôle de disponibilité, ni mouvement.
                if (produit.GereStock)
                {
                    // ── Stock : contrôle de disponibilité (avant, pas après) ──
                    if (!stocks.TryGetValue(produit.Id, out var stock))
                    {
                        stock = new StockProduit { TenantId = _currentUser.TenantId, MagasinId = session.MagasinId, ProduitId = produit.Id, Quantite = 0 };
                        _db.StocksProduits.Add(stock);
                        stocks[produit.Id] = stock;
                    }

                    if (stock.Quantite < l.Quantite && !parametres.AutoriserStockNegatif && !horsLigne)
                        throw new BusinessRuleException($"Stock insuffisant pour « {produit.Libelle} » (disponible : {stock.Quantite:0.###}).");

                    var stockCourant = stock.Quantite;

                    void Sortie(Guid? lotId, decimal qte)
                    {
                        stockCourant -= qte;
                        _db.Mouvements.Add(new MouvementStock
                        {
                            TenantId = _currentUser.TenantId,
                            MagasinId = session.MagasinId,
                            ProduitId = produit.Id,
                            LotId = lotId,
                            Type = TypeMouvement.SortieVente,
                            Quantite = -qte,
                            StockApres = stockCourant,
                            CoutUnitaire = produit.PrixAchatMoyen,
                            DocumentType = "VENTE",
                            DocumentId = vente.Id,
                            DateMouvement = maintenant,
                            UtilisateurId = _currentUser.UserId
                        });
                    }

                    if (produit.SuiviLots)
                    {
                        // FEFO : on sort d'abord les lots qui expirent le plus tôt, jamais un lot périmé.
                        var aujourdhui = DateOnly.FromDateTime(maintenant);
                        var lots = await _db.Lots
                            .Where(x => x.MagasinId == session.MagasinId && x.ProduitId == produit.Id
                                        && x.Statut == StatutLot.Actif && x.QuantiteRestante > 0
                                        && (x.DatePeremption == null || x.DatePeremption >= aujourdhui))
                            .OrderBy(x => x.DatePeremption == null)
                            .ThenBy(x => x.DatePeremption)
                            .ToListAsync(ct);

                        var reste = l.Quantite;
                        foreach (var lot in lots)
                        {
                            if (reste <= 0) break;
                            var prelevement = Math.Min(lot.QuantiteRestante, reste);
                            lot.QuantiteRestante -= prelevement;
                            if (lot.QuantiteRestante <= 0) lot.Statut = StatutLot.Epuise;
                            reste -= prelevement;
                            Sortie(lot.Id, prelevement);
                        }

                        if (reste > 0)
                        {
                            if (!parametres.AutoriserStockNegatif && !horsLigne)
                                throw new BusinessRuleException($"Lots valides insuffisants pour « {produit.Libelle} » (lots périmés ou stock non lotti).");
                            Sortie(null, reste);
                        }
                    }
                    else
                    {
                        Sortie(null, l.Quantite);
                    }

                    stock.Quantite = stockCourant;
                    stock.DateDernierMouvement = maintenant;
                }
            }

            vente.TotalHT = totalHT;
            vente.TotalTVA = totalTVA;
            vente.TotalTTC = totalTTC;
            vente.TotalRemise = totalRemise;

            // ── Paiements : validés, rendu monnaie calculé côté serveur ──
            if (request.Paiements.Any(p => p.Montant <= 0))
                throw new BusinessRuleException("Chaque paiement doit avoir un montant positif.");

            decimal Somme(ModePaiement mode) => request.Paiements.Where(p => p.Mode == mode).Sum(p => p.Montant);

            var recu = request.Paiements.Sum(p => p.Montant);
            var especes = Somme(ModePaiement.Especes);
            var credit = Somme(ModePaiement.Credit);
            var pointsFcfa = Somme(ModePaiement.Points);

            if (recu < totalTTC)
                throw new BusinessRuleException($"Paiement insuffisant : il manque {totalTTC - recu:N0}.");

            var rendu = recu - totalTTC;
            if (rendu > especes)
                throw new BusinessRuleException("Le rendu monnaie dépasse les espèces reçues.");

            if ((credit > 0 || pointsFcfa > 0) && client == null)
                throw new BusinessRuleException("Un client est requis pour un paiement à crédit ou en points.");

            var renduRestant = rendu;
            foreach (var p in request.Paiements)
            {
                var imputé = p.Montant;
                if (p.Mode == ModePaiement.Especes && renduRestant > 0)
                {
                    var deduit = Math.Min(renduRestant, p.Montant);
                    imputé -= deduit;
                    renduRestant -= deduit;
                }

                vente.Paiements.Add(new Paiement
                {
                    TenantId = _currentUser.TenantId,
                    SessionCaisseId = session.Id,
                    Mode = p.Mode,
                    Montant = imputé,          // net du rendu monnaie
                    MontantRecu = p.Montant,   // ce que le client a tendu
                    Operateur = p.Operateur,
                    Reference = p.Reference,
                    DatePaiement = maintenant
                });
            }

            vente.MontantPaye = recu;
            vente.RenduMonnaie = rendu;

            // ── Clientèle : crédit & fidélité ──
            if (client != null)
            {
                if (credit > 0)
                {
                    if (client.SoldeCredit + credit > client.PlafondCredit)
                        throw new BusinessRuleException($"Plafond de crédit dépassé (disponible : {Math.Max(0, client.PlafondCredit - client.SoldeCredit):N0}).");
                    client.SoldeCredit += credit;
                }

                if (pointsFcfa > 0)
                {
                    if (parametres.ValeurPointFcfa <= 0)
                        throw new BusinessRuleException("Le paiement par points n'est pas activé.");

                    var pointsUtilises = (int)Math.Ceiling(pointsFcfa / parametres.ValeurPointFcfa);
                    if (client.PointsFidelite < pointsUtilises)
                        throw new BusinessRuleException($"Points insuffisants ({client.PointsFidelite} disponibles, {pointsUtilises} nécessaires).");

                    client.PointsFidelite -= pointsUtilises;
                    _db.MouvementsFidelite.Add(new MouvementFidelite
                    {
                        TenantId = _currentUser.TenantId,
                        ClientId = client.Id,
                        VenteId = vente.Id,
                        Type = TypeMouvementFidelite.Utilisation,
                        Points = -pointsUtilises,
                        SoldeApres = client.PointsFidelite,
                        Motif = $"Paiement ticket {vente.NumeroTicket}",
                        DateMouvement = maintenant
                    });
                }

                if (parametres.MontantParPoint > 0)
                {
                    var base_ = Math.Max(0, totalTTC - credit - pointsFcfa);
                    var gagnes = (int)Math.Floor(base_ / parametres.MontantParPoint);
                    if (gagnes > 0)
                    {
                        client.PointsFidelite += gagnes;
                        _db.MouvementsFidelite.Add(new MouvementFidelite
                        {
                            TenantId = _currentUser.TenantId,
                            ClientId = client.Id,
                            VenteId = vente.Id,
                            Type = TypeMouvementFidelite.Gain,
                            Points = gagnes,
                            SoldeApres = client.PointsFidelite,
                            Motif = $"Achat ticket {vente.NumeroTicket}",
                            DateMouvement = maintenant
                        });
                    }
                }

                client.DateDerniereVente = maintenant;
            }

            _db.Ventes.Add(vente);
            await _db.SaveChangesAsync(ct);

            return ChargerTicketDto(vente);
        }, ct);
    }

    /// <summary>Vérifie le PIN du superviseur choisi (hash) avec verrouillage après échecs répétés. Retourne son Id.</summary>
    private async Task<Guid> VerifierSuperviseurAsync(AutorisationSuperviseurDto? autorisation, CancellationToken ct)
    {
        if (autorisation == null || string.IsNullOrWhiteSpace(autorisation.Pin))
            throw new BusinessRuleException("Cette remise nécessite la validation d'un superviseur (PIN).");

        var maintenant = DateTime.UtcNow;
        var superviseur = await _db.SuperviseurPins
            .FirstOrDefaultAsync(s => s.UtilisateurId == autorisation.SuperviseurId && s.EstActif, ct)
            ?? throw new BusinessRuleException("Superviseur non autorisé.");

        if (superviseur.VerrouJusqua.HasValue && superviseur.VerrouJusqua.Value > maintenant)
            throw new BusinessRuleException("Ce superviseur est verrouillé temporairement après plusieurs PIN erronés.");

        if (!_pinHasher.Verify(autorisation.Pin, superviseur.PinHash))
        {
            superviseur.TentativesEchouees++;
            if (superviseur.TentativesEchouees >= TentativesPinMax)
            {
                superviseur.VerrouJusqua = maintenant.Add(DureeVerrouPin);
                superviseur.TentativesEchouees = 0;
            }
            await _db.SaveChangesAsync(ct);
            throw new BusinessRuleException("PIN superviseur invalide.");
        }

        if (superviseur.TentativesEchouees != 0 || superviseur.VerrouJusqua != null)
        {
            superviseur.TentativesEchouees = 0;
            superviseur.VerrouJusqua = null;
            await _db.SaveChangesAsync(ct);
        }

        return superviseur.UtilisateurId;
    }

    // ─────────────────────────────── ANNULATION D'UN TICKET ───────────────────────────────

    public async Task<List<TicketSessionDto>> ListerTicketsSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        var ventes = await _db.Ventes.AsNoTracking()
            .Where(v => v.SessionCaisseId == sessionId)
            .OrderByDescending(v => v.DateVente)
            .Take(200)
            .ToListAsync(ct);

        var clientIds = ventes.Where(v => v.ClientId.HasValue).Select(v => v.ClientId!.Value).Distinct().ToList();
        var clients = await _db.Clients.AsNoTracking().Where(c => clientIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Nom, ct);

        return ventes.Select(v => new TicketSessionDto(
            v.Id, v.NumeroTicket, v.DateVente, v.TotalTTC, v.Statut == StatutVente.Annulee,
            v.ClientId is { } c ? clients.GetValueOrDefault(c) : null, v.MotifAnnulation)).ToList();
    }

    /// <summary>
    /// Annule un ticket de la session EN COURS (erreur de saisie, client qui renonce au comptoir).
    /// Tout est remis en l'état : stock et lots réintégrés, crédit client et points de fidélité rétablis.
    /// Le ticket reste visible, marqué « Annulé » (jamais supprimé) ; il sort des totaux et du rapport Z :
    /// la caisse doit rendre l'argent encaissé. Après clôture de la session, il faudra un avoir / retour.
    /// </summary>
    public async Task<AnnulationVenteDto> AnnulerVenteAsync(Guid venteId, string motif, Guid autoriseParId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(motif) || motif.Trim().Length < 3)
            throw new BusinessRuleException("Indiquez le motif de l'annulation.");

        return await _db.ExecuterAsync(async () =>
        {
            var vente = await _db.Ventes.Include(v => v.Paiements).FirstOrDefaultAsync(v => v.Id == venteId, ct)
                ?? throw new NotFoundException("Ticket introuvable.");

            if (vente.Statut == StatutVente.Annulee)
                throw new BusinessRuleException($"Le ticket {vente.NumeroTicket} est déjà annulé.");

            var session = await _db.Sessions.FirstOrDefaultAsync(s => s.Id == vente.SessionCaisseId, ct)
                ?? throw new NotFoundException("Session de caisse introuvable.");
            if (session.Statut != StatutSession.Ouverte)
                throw new BusinessRuleException("La caisse de ce ticket est déjà clôturée : l'annulation n'est plus possible (il faudra faire un avoir).");

            var maintenant = DateTime.UtcNow;

            // 1. Stock : chaque sortie du ticket est réintégrée (même magasin, même lot)
            var sorties = await _db.Mouvements
                .Where(m => m.DocumentType == "VENTE" && m.DocumentId == vente.Id && m.Quantite < 0)
                .ToListAsync(ct);

            foreach (var groupe in sorties.GroupBy(m => new { m.MagasinId, m.ProduitId }))
            {
                var stock = await _db.StocksProduits.FirstOrDefaultAsync(s => s.MagasinId == groupe.Key.MagasinId && s.ProduitId == groupe.Key.ProduitId, ct);
                var courant = stock?.Quantite ?? 0;

                foreach (var m in groupe)
                {
                    var qte = -m.Quantite;
                    courant += qte;

                    if (m.LotId is { } lotId && await _db.Lots.FirstOrDefaultAsync(l => l.Id == lotId, ct) is { } lot)
                    {
                        lot.QuantiteRestante += qte;
                        if (lot.Statut == StatutLot.Epuise && lot.QuantiteRestante > 0) lot.Statut = StatutLot.Actif;
                    }

                    _db.Mouvements.Add(new MouvementStock
                    {
                        TenantId = _currentUser.TenantId,
                        MagasinId = m.MagasinId,
                        ProduitId = m.ProduitId,
                        LotId = m.LotId,
                        Type = TypeMouvement.EntreeRetourClient,
                        Quantite = qte,
                        StockApres = courant,
                        CoutUnitaire = m.CoutUnitaire,
                        DocumentType = "ANNULATION",
                        DocumentId = vente.Id,
                        Motif = $"Annulation ticket {vente.NumeroTicket} : {motif.Trim()}",
                        DateMouvement = maintenant,
                        UtilisateurId = _currentUser.UserId
                    });
                }

                if (stock != null)
                {
                    stock.Quantite = courant;
                    stock.DateDernierMouvement = maintenant;
                }
            }

            // 2. Client : crédit et points rétablis
            if (vente.ClientId is { } clientId && await _db.Clients.FirstOrDefaultAsync(c => c.Id == clientId, ct) is { } client)
            {
                var credit = vente.Paiements.Where(p => p.Mode == ModePaiement.Credit).Sum(p => p.Montant);
                if (credit > 0) client.SoldeCredit = Math.Max(0, client.SoldeCredit - credit);

                var points = await _db.MouvementsFidelite.Where(f => f.VenteId == vente.Id).SumAsync(f => f.Points, ct);
                if (points != 0)
                {
                    // Gain annulé (−) et points utilisés rendus (+) en un seul mouvement d'ajustement
                    client.PointsFidelite = Math.Max(0, client.PointsFidelite - points);
                    _db.MouvementsFidelite.Add(new MouvementFidelite
                    {
                        TenantId = _currentUser.TenantId,
                        ClientId = client.Id,
                        VenteId = vente.Id,
                        Type = TypeMouvementFidelite.Ajustement,
                        Points = -points,
                        SoldeApres = client.PointsFidelite,
                        Motif = $"Annulation ticket {vente.NumeroTicket}",
                        DateMouvement = maintenant
                    });
                }
            }

            // 3. Ticket marqué annulé (traçabilité complète)
            vente.Statut = StatutVente.Annulee;
            vente.MotifAnnulation = motif.Trim();
            vente.AnnuleParId = autoriseParId;
            vente.DateAnnulation = maintenant;

            await _db.SaveChangesAsync(ct);

            // Montants nets du rendu monnaie : c'est exactement ce qui est resté en caisse pour ce ticket
            var aRendre = vente.Paiements.Where(p => ModesPhysiques.Contains(p.Mode)).Sum(p => p.Montant);
            return new AnnulationVenteDto(vente.Id, vente.NumeroTicket, aRendre,
                aRendre > 0 ? $"Ticket {vente.NumeroTicket} annulé. Rendez {aRendre:N0} F au client." : $"Ticket {vente.NumeroTicket} annulé.");
        }, ct);
    }

    // ─────────────────────────────── CLÔTURE & RAPPORT Z ───────────────────────────────

    public async Task<ClotureRecueDto> CloturerSessionAsync(ClotureAveugleRequest request, CancellationToken ct = default)
    {
        return await _db.ExecuterAsync(async () =>
        {
            var session = await _db.Sessions.FirstOrDefaultAsync(s => s.Id == request.SessionCaisseId, ct)
                ?? throw new NotFoundException("Session introuvable.");

            if (session.Statut != StatutSession.Ouverte)
                throw new BusinessRuleException("La session est déjà clôturée.");

            var parametres = await _db.ParametresOuDefautAsync(ct);
            var paiements = await PaiementsValidesAsync(session.Id, ct);

            // Le théorique est calculé ICI, jamais reçu du client.
            var lignes = ModesPhysiques.Select(mode =>
            {
                var theorique = paiements.Where(p => p.Mode == mode).Sum(p => p.Montant)
                                + (mode == ModePaiement.Especes ? session.FondDeCaisse : 0);
                var declare = (request.Lignes ?? new()).Where(l => l.Mode == mode).Sum(l => l.MontantDeclare);

                return new ClotureCaisseLigne
                {
                    TenantId = _currentUser.TenantId,
                    Mode = mode,
                    MontantDeclare = declare,
                    MontantTheorique = theorique,
                    Ecart = declare - theorique
                };
            }).ToList();

            var ecartTotal = lignes.Sum(l => l.Ecart);

            var cloture = new ClotureCaisse
            {
                TenantId = _currentUser.TenantId,
                SessionCaisseId = session.Id,
                DateCloture = DateTime.UtcNow,
                TotalDeclare = lignes.Sum(l => l.MontantDeclare),
                TotalTheorique = lignes.Sum(l => l.MontantTheorique),
                EcartTotal = ecartTotal,
                EcartSignale = Math.Abs(ecartTotal) >= parametres.SeuilEcartCaisseSignale,
                Commentaire = request.Commentaire,
                Lignes = lignes
            };

            session.Statut = StatutSession.Cloturee;
            session.DateCloture = cloture.DateCloture;

            _db.Clotures.Add(cloture);
            await _db.SaveChangesAsync(ct);

            // Réponse volontairement neutre : le caissier ne voit jamais l'écart.
            return new ClotureRecueDto(session.Id, session.DateCloture.Value, "Clôture enregistrée avec succès. Merci pour votre journée !");
        }, ct);
    }

    public async Task<RapportZDto> ObtenirRapportZAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await _db.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new NotFoundException("Session introuvable.");

        // Clôture à l'aveugle : le rapport (avec le théorique) n'existe qu'après la clôture.
        if (session.Statut == StatutSession.Ouverte)
            throw new BusinessRuleException("Le rapport Z n'est disponible qu'après la clôture de la session.");

        var caisse = await _db.Caisses.FirstOrDefaultAsync(c => c.Id == session.CaisseId, ct);
        var ventes = await _db.Ventes.Where(v => v.SessionCaisseId == sessionId && v.Statut == StatutVente.Validee).ToListAsync(ct);
        var paiements = await PaiementsValidesAsync(sessionId, ct);
        var cloture = await _db.Clotures.Include(c => c.Lignes).FirstOrDefaultAsync(c => c.SessionCaisseId == sessionId, ct);

        var totalTTC = ventes.Sum(v => v.TotalTTC);
        var totalRemises = ventes.Sum(v => v.TotalRemise);

        var lignes = new List<RapportZLigneDto>();
        foreach (var mode in ModesPhysiques)
        {
            var theorique = paiements.Where(p => p.Mode == mode).Sum(p => p.Montant)
                            + (mode == ModePaiement.Especes ? session.FondDeCaisse : 0);
            var decl = cloture?.Lignes.FirstOrDefault(l => l.Mode == mode);

            lignes.Add(new RapportZLigneDto(mode, theorique, decl?.MontantDeclare, decl != null ? decl.MontantDeclare - theorique : null));
        }

        return new RapportZDto(
            session.Id, caisse?.Libelle ?? "", session.CaissierId, session.DateOuverture, session.DateCloture, session.Statut,
            session.FondDeCaisse, ventes.Count, 0, totalTTC, totalRemises, lignes,
            cloture?.EcartTotal, cloture?.EcartSignale ?? false, cloture?.ValideParId, cloture?.DateValidation, cloture?.Commentaire
        );
    }

    /// <summary>
    /// Numéro continu par tenant et par année (T-2026-000001). L'incrément est atomique (UPDLOCK/HOLDLOCK) et fait partie
    /// de la transaction de la vente : si la vente échoue, le numéro n'est pas consommé, donc pas de trou.
    /// </summary>
    private async Task<string> ProchainNumeroTicketAsync(string prefixe, int annee, CancellationToken ct)
    {
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM SequencesDocuments WITH (UPDLOCK, HOLDLOCK) WHERE TenantId = {0} AND Cle = {1} AND Annee = {2})
    INSERT INTO SequencesDocuments (Id, TenantId, Cle, Annee, DernierNumero) VALUES (NEWID(), {0}, {1}, {2}, 0);
UPDATE SequencesDocuments SET DernierNumero = DernierNumero + 1
OUTPUT INSERTED.DernierNumero AS Value
WHERE TenantId = {0} AND Cle = {1} AND Annee = {2};";

        var resultat = await _db.Database
            .SqlQueryRaw<long>(sql, _currentUser.TenantId, "TICKET", annee)
            .ToListAsync(ct);

        return $"{prefixe}-{annee}-{resultat.Single():D6}";
    }

    /// <summary>Paiements des seules ventes validées (les ventes annulées sortent du théorique).</summary>
    private async Task<List<Paiement>> PaiementsValidesAsync(Guid sessionId, CancellationToken ct)
    {
        return await (from p in _db.Paiements
                      join v in _db.Ventes on p.VenteId equals v.Id
                      where v.SessionCaisseId == sessionId && v.Statut == StatutVente.Validee
                      select p).ToListAsync(ct);
    }

    private static TicketDto ChargerTicketDto(Vente v)
    {
        return new TicketDto(
            v.Id, v.NumeroTicket, v.DateVente, v.Statut, v.TotalHT, v.TotalTVA, v.TotalRemise, v.TotalTTC, v.MontantPaye, v.RenduMonnaie,
            v.Lignes.Select(l => new TicketLigneDto(l.ProduitId, l.Libelle, l.CodeBarres, l.Quantite, l.PrixUnitaireTTC, l.RemisePourcent, l.RemiseMontant, l.TauxTva, l.MontantTTC)).ToList(),
            v.Paiements.Select(p => new TicketPaiementDto(p.Mode, p.Montant, p.MontantRecu, p.Operateur, p.Reference)).ToList()
        );
    }
}
