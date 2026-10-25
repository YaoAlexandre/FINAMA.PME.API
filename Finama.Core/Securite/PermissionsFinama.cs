using Finama.Core.Entities;

namespace Finama.Core.Securite;

/// <summary>
/// Catalogue des permissions. NOUVEAU MODULE = ajouter ses permissions ici (et les vérifier sur ses écrans / API).
/// Elles apparaissent automatiquement dans l'écran « Profils & permissions ». Aucun nouveau rôle à créer.
/// </summary>
public static class Perm
{
    // Finance & Compta
    public const string ComptaConsulter = "compta.consulter";
    public const string ComptaSaisir = "compta.saisir";
    public const string ComptaValider = "compta.valider";

    // Ventes & Devis
    public const string VentesDevis = "ventes.devis";
    public const string VentesTousDevis = "ventes.tousdevis";
    public const string VentesFacturer = "ventes.facturer";

    // Caisse
    public const string CaisseVendre = "caisse.vendre";
    public const string CaisseAnnuler = "caisse.annuler";
    public const string CaisseFinJournee = "caisse.finjournee";

    // Stock
    public const string StockConsulter = "stock.consulter";
    public const string StockGerer = "stock.gerer";
    public const string StockInventaire = "stock.inventaire";

    // Administration
    public const string AdminUtilisateurs = "admin.utilisateurs";
    public const string AdminParametres = "admin.parametres";
}

public static class PermissionsFinama
{
    public sealed record Permission(string Code, string Module, string Libelle, string Description);
    public sealed record Groupe(string Module, string Titre, string Icone, ModuleFinama? ModuleAbonnement);
    public sealed record ProfilPredefini(RoleUtilisateur Role, string Libelle, string Description, string[] Permissions);

    public static readonly Groupe[] Groupes =
    [
        new("COMPTA", "Finance & Compta", "bi-wallet2", ModuleFinama.Comptabilite),
        new("VENTES", "Ventes & Devis", "bi-file-earmark-check", ModuleFinama.GestionCommerciale),
        new("CAISSE", "Caisse", "bi-cash-coin", ModuleFinama.Commerce),
        new("STOCK", "Stock & catalogue", "bi-box-seam", ModuleFinama.Commerce),
        new("ADMIN", "Administration", "bi-gear", null),
    ];

    public static readonly Permission[] Toutes =
    [
        new(Perm.ComptaConsulter, "COMPTA", "Consulter la comptabilité", "Écritures, balance, grand livre, états financiers, exports."),
        new(Perm.ComptaSaisir, "COMPTA", "Saisir", "Créer des écritures en brouillon, des factures et des tiers."),
        new(Perm.ComptaValider, "COMPTA", "Valider", "Valider les écritures, émettre les factures."),

        new(Perm.VentesDevis, "VENTES", "Faire des devis", "Créer, envoyer et suivre ses propres devis ; gérer les clients."),
        new(Perm.VentesTousDevis, "VENTES", "Voir tous les devis", "Voir les devis de toute l'équipe (sinon : seulement les siens)."),
        new(Perm.VentesFacturer, "VENTES", "Facturer un devis", "Transformer un devis accepté en facture."),

        new(Perm.CaisseVendre, "CAISSE", "Tenir la caisse", "Ouvrir et clôturer sa caisse, vendre, encaisser, créer des clients fidélité."),
        new(Perm.CaisseAnnuler, "CAISSE", "Annuler un ticket", "Annuler un ticket sans validation du gérant, et valider les annulations des caissiers."),
        new(Perm.CaisseFinJournee, "CAISSE", "Fin de journée", "Voir les encaissements, écarts de caisse et marges du jour."),

        new(Perm.StockConsulter, "STOCK", "Consulter le stock", "Voir articles, prix et niveaux de stock."),
        new(Perm.StockGerer, "STOCK", "Gérer articles et stock", "Créer et modifier les articles, réceptionner, ajuster, imprimer les étiquettes."),
        new(Perm.StockInventaire, "STOCK", "Faire les inventaires", "Ouvrir un inventaire et saisir les comptages."),

        new(Perm.AdminUtilisateurs, "ADMIN", "Gérer les utilisateurs", "Créer les accès, attribuer les profils, réinitialiser les mots de passe."),
        new(Perm.AdminParametres, "ADMIN", "Paramètres de l'entreprise", "Entreprise, logo, magasins, options, clôtures (inventaire, exercice)."),
    ];

    /// <summary>Profils prêts à l'emploi (les 7 rôles historiques). Un utilisateur sans profil personnalisé utilise celui de son rôle.</summary>
    public static readonly ProfilPredefini[] Predefinis =
    [
        new(RoleUtilisateur.AdminTenant, "Administrateur (gérant)", "Accès total à l'entreprise.",
            Toutes.Select(p => p.Code).ToArray()),
        new(RoleUtilisateur.Comptable, "Comptable", "Comptabilité complète ; consulte et facture les devis.",
            [Perm.ComptaConsulter, Perm.ComptaSaisir, Perm.ComptaValider, Perm.VentesTousDevis, Perm.VentesFacturer]),
        new(RoleUtilisateur.Collaborateur, "Aide-comptable", "Saisie en brouillon, sans validation.",
            [Perm.ComptaConsulter, Perm.ComptaSaisir]),
        new(RoleUtilisateur.Commercial, "Commercial / vendeur", "Devis et clients ; caisse et stock dans une petite structure.",
            [Perm.VentesDevis, Perm.VentesFacturer, Perm.CaisseVendre, Perm.StockConsulter, Perm.StockGerer, Perm.StockInventaire]),
        new(RoleUtilisateur.Caissier, "Caissier / caissière", "Caisse uniquement ; annulation validée par le gérant.",
            [Perm.CaisseVendre, Perm.StockConsulter]),
        new(RoleUtilisateur.Magasinier, "Magasinier", "Articles, réceptions, inventaires, étiquettes ; pas de caisse.",
            [Perm.StockConsulter, Perm.StockGerer, Perm.StockInventaire]),
        new(RoleUtilisateur.Lecture, "Consultation seule", "Consulte la comptabilité, sans rien modifier.",
            [Perm.ComptaConsulter]),
    ];

    public static readonly HashSet<string> Codes = Toutes.Select(p => p.Code).ToHashSet();

    public static IReadOnlySet<string> DuRole(RoleUtilisateur role)
        => role == RoleUtilisateur.SuperAdmin
            ? Codes
            : Predefinis.FirstOrDefault(p => p.Role == role)?.Permissions.ToHashSet() ?? new HashSet<string>();

    /// <summary>Stockage : codes séparés par des virgules, uniquement des codes connus.</summary>
    public static string Serialiser(IEnumerable<string> codes) => string.Join(',', codes.Where(Codes.Contains).Distinct().Order());

    public static HashSet<string> Lire(string? stockees)
        => (stockees ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(Codes.Contains).ToHashSet();
}
