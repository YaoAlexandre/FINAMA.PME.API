namespace Finama.Core.Entities;

/// <summary>Modules activables par entreprise (combinables : Comptabilite | Commerce…). Stocké en entier dans Tenants.Modules.</summary>
[Flags]
public enum ModuleFinama
{
    Aucun = 0,
    Comptabilite = 1,        // écritures, factures, tiers, balance, grand livre, états financiers
    GestionCommerciale = 2,  // module « Ventes & Devis » : devis, pipeline, clients, factures
    Commerce = 4,            // caisse, articles, stocks, inventaires, clients & fidélité
    Tous = Comptabilite | GestionCommerciale | Commerce
}

public static class ModulesFinama
{
    public static readonly (ModuleFinama Module, string Libelle, string Description, string Icone)[] Liste =
    [
        (ModuleFinama.Comptabilite, "Finance & Compta", "Écritures, factures, tiers, balance, grand livre, états SYSCOHADA", "bi-wallet2"),
        (ModuleFinama.GestionCommerciale, "Ventes & Devis", "Devis, pipeline, clients, factures de vente", "bi-file-earmark-check"),
        (ModuleFinama.Commerce, "Commerce & Ventes", "Caisse, articles, stocks, inventaires, fidélité", "bi-cart3"),
    ];
}
