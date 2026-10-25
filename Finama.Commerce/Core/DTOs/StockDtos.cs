using Finama.Commerce.Core.Domain;

namespace Finama.Commerce.Core.DTOs;

public record LotDto(Guid Id, string NumeroLot, DateOnly? DatePeremption, decimal QuantiteRestante, StatutLot Statut);
public record StockProduitDto(Guid MagasinId, Guid ProduitId, decimal Quantite, IReadOnlyList<LotDto> Lots);

public record MouvementsQuery(Guid? MagasinId, Guid? ProduitId, TypeMouvement? Type, DateTime? Du, DateTime? Au, int Page = 1, int PageSize = 50);
public record MouvementDto(Guid Id, DateTime DateMouvement, Guid MagasinId, Guid ProduitId, string ProduitLibelle, Guid? LotId,
                           TypeMouvement Type, decimal Quantite, decimal StockApres, string? DocumentType, Guid? DocumentId, string? Motif);

public record AlerteReapproDto(Guid ProduitId, string Reference, string Libelle, decimal StockActuel, decimal SeuilReappro, decimal QuantiteSuggeree);
public record AlertePeremptionDto(Guid LotId, Guid ProduitId, string Libelle, string NumeroLot, DateOnly DatePeremption, decimal QuantiteRestante, int JoursRestants);

public enum MotifAjustement { Correction = 0, Casse = 1, Peremption = 2 }

/// <summary>Delta signé : + ajoute au stock, − retire. Les ajustements négatifs de produits à lots visent un lot précis.</summary>
public record AjusterStockRequest(Guid MagasinId, Guid ProduitId, Guid? LotId, decimal Delta, MotifAjustement Motif, string Commentaire);

/// <summary>Stock d'ouverture (reprise de l'existant) avant que le module Achats ne prenne le relais.</summary>
public record EntreeInitialeRequest(Guid MagasinId, Guid ProduitId, decimal Quantite, decimal? CoutUnitaire, string? NumeroLot, DateOnly? DatePeremption);

public record OuvrirInventaireRequest(Guid MagasinId, string Libelle, Guid? CategorieId);
public record LigneComptageDto(Guid LigneId, decimal QuantiteComptee);
public record ComptageRequest(IReadOnlyList<LigneComptageDto> Lignes);
public record ClotureInventaireRequest(bool TraiterNonComptesCommeZero);

/// <summary>QuantiteTheorique et Ecart restent null pour un compteur : le comptage se fait à l'aveugle.</summary>
public record InventaireLigneDto(Guid LigneId, Guid ProduitId, string ProduitLibelle, Guid? LotId, string? NumeroLot,
                                 decimal? QuantiteTheorique, decimal? QuantiteComptee, decimal? Ecart);
public record InventaireDto(Guid Id, string Numero, string Libelle, StatutInventaire Statut, DateTime DateOuverture,
                            DateTime? DateCloture, IReadOnlyList<InventaireLigneDto> Lignes);
public record ResultatInventaireDto(int NbLignes, int NbEcarts, decimal ValeurEcartAuCout);
