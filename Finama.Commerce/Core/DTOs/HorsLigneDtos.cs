namespace Finama.Commerce.Core.DTOs;

/// <summary>Options d'enregistrement d'une vente (ventes faites sans réseau puis synchronisées).</summary>
public sealed record OptionsVente(bool HorsLigne, DateTime? DateVente, string? NumeroHorsLigne);

/// <summary>Vente faite en caisse sans connexion, envoyée au retour du réseau.</summary>
public sealed record VenteHorsLigneRequest(CreerVenteRequest Vente, DateTime DateVenteLocale, string NumeroHorsLigne);

public sealed record ResultatVenteHorsLigne(Guid VenteId, string NumeroTicket, string NumeroHorsLigne, IReadOnlyList<string> StocksNegatifs);
