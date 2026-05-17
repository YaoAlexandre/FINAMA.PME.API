

using Finama.Core.Entities;

namespace Finama.Infrastructure.Seeds;

/// <summary>
/// Plan comptable OHADA — comptes de niveau 1 et 2.
/// À appeler lors de la création d'un nouveau tenant.
/// </summary>
public static class PlanComptableOhadaSeed
{
    public static IReadOnlyList<(string Numero, string Libelle, ClasseCompte Classe, TypeCompte Type)> Comptes =>
    [
        // ── Classe 1 : Capitaux ──────────────────────────────────────────────
        ("10", "Capital et réserves",              ClasseCompte.Classe1, TypeCompte.Bilan),
        ("101", "Capital social",                  ClasseCompte.Classe1, TypeCompte.Bilan),
        ("106", "Réserves",                        ClasseCompte.Classe1, TypeCompte.Bilan),
        ("12", "Résultat net de l'exercice",       ClasseCompte.Classe1, TypeCompte.Bilan),
        ("16", "Emprunts et dettes assimilées",    ClasseCompte.Classe1, TypeCompte.Bilan),

        // ── Classe 2 : Immobilisations ───────────────────────────────────────
        ("21", "Immobilisations incorporelles",    ClasseCompte.Classe2, TypeCompte.Bilan),
        ("22", "Terrains",                         ClasseCompte.Classe2, TypeCompte.Bilan),
        ("23", "Bâtiments",                        ClasseCompte.Classe2, TypeCompte.Bilan),
        ("24", "Matériel",                         ClasseCompte.Classe2, TypeCompte.Bilan),
        ("28", "Amortissements",                   ClasseCompte.Classe2, TypeCompte.Bilan),

        // ── Classe 3 : Stocks ────────────────────────────────────────────────
        ("31", "Marchandises",                     ClasseCompte.Classe3, TypeCompte.Bilan),
        ("32", "Matières premières",               ClasseCompte.Classe3, TypeCompte.Bilan),
        ("35", "Produits finis",                   ClasseCompte.Classe3, TypeCompte.Bilan),

        // ── Classe 4 : Tiers ─────────────────────────────────────────────────
        ("40", "Fournisseurs et comptes rattachés",ClasseCompte.Classe4, TypeCompte.Bilan),
        ("401", "Fournisseurs",                    ClasseCompte.Classe4, TypeCompte.Bilan),
        ("404", "Fournisseurs d'immobilisations",  ClasseCompte.Classe4, TypeCompte.Bilan),
        ("41", "Clients et comptes rattachés",     ClasseCompte.Classe4, TypeCompte.Bilan),
        ("411", "Clients",                         ClasseCompte.Classe4, TypeCompte.Bilan),
        ("419", "Clients créditeurs",              ClasseCompte.Classe4, TypeCompte.Bilan),
        ("43", "Personnel et comptes rattachés",   ClasseCompte.Classe4, TypeCompte.Bilan),
        ("431", "Personnel — rémunérations dues",  ClasseCompte.Classe4, TypeCompte.Bilan),
        ("44", "État et collectivités publiques",  ClasseCompte.Classe4, TypeCompte.Bilan),
        ("4451", "TVA collectée",                  ClasseCompte.Classe4, TypeCompte.Bilan),
        ("4452", "TVA déductible",                 ClasseCompte.Classe4, TypeCompte.Bilan),

        // ── Classe 5 : Trésorerie ────────────────────────────────────────────
        ("51", "Banques",                          ClasseCompte.Classe5, TypeCompte.Tresorerie),
        ("511", "Chèques à encaisser",             ClasseCompte.Classe5, TypeCompte.Tresorerie),
        ("512", "Banque principale",               ClasseCompte.Classe5, TypeCompte.Tresorerie),
        ("57", "Caisse",                           ClasseCompte.Classe5, TypeCompte.Tresorerie),
        ("571", "Caisse siège",                    ClasseCompte.Classe5, TypeCompte.Tresorerie),

        // ── Classe 6 : Charges ───────────────────────────────────────────────
        ("60", "Achats",                           ClasseCompte.Classe6, TypeCompte.ResultatCharge),
        ("601", "Achats de marchandises",          ClasseCompte.Classe6, TypeCompte.ResultatCharge),
        ("602", "Achats de matières premières",    ClasseCompte.Classe6, TypeCompte.ResultatCharge),
        ("61", "Transports",                       ClasseCompte.Classe6, TypeCompte.ResultatCharge),
        ("62", "Services extérieurs A",            ClasseCompte.Classe6, TypeCompte.ResultatCharge),
        ("63", "Services extérieurs B",            ClasseCompte.Classe6, TypeCompte.ResultatCharge),
        ("64", "Impôts et taxes",                  ClasseCompte.Classe6, TypeCompte.ResultatCharge),
        ("65", "Autres charges",                   ClasseCompte.Classe6, TypeCompte.ResultatCharge),
        ("66", "Charges de personnel",             ClasseCompte.Classe6, TypeCompte.ResultatCharge),
        ("661", "Rémunérations directes",          ClasseCompte.Classe6, TypeCompte.ResultatCharge),
        ("67", "Frais financiers",                 ClasseCompte.Classe6, TypeCompte.ResultatCharge),
        ("68", "Dotations aux amortissements",     ClasseCompte.Classe6, TypeCompte.ResultatCharge),

        // ── Classe 7 : Produits ──────────────────────────────────────────────
        ("70", "Ventes",                           ClasseCompte.Classe7, TypeCompte.ResultatProduit),
        ("701", "Ventes de marchandises",          ClasseCompte.Classe7, TypeCompte.ResultatProduit),
        ("702", "Ventes de produits finis",        ClasseCompte.Classe7, TypeCompte.ResultatProduit),
        ("706", "Prestations de services",         ClasseCompte.Classe7, TypeCompte.ResultatProduit),
        ("71", "Variation de stocks",              ClasseCompte.Classe7, TypeCompte.ResultatProduit),
        ("75", "Autres produits",                  ClasseCompte.Classe7, TypeCompte.ResultatProduit),
        ("77", "Produits financiers",              ClasseCompte.Classe7, TypeCompte.ResultatProduit),
    ];

    /// <summary>
    /// Génère les entités CompteComptable pour un tenant donné.
    /// </summary>
    public static List<CompteComptable> GenererPourTenant(Guid tenantId)
    {
        return Comptes.Select(c => new CompteComptable
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Numero = c.Numero,
            Libelle = c.Libelle,
            Classe = c.Classe,
            Type = c.Type,
            EstSysteme = true,
            EstActif = true,
            CreatedAt = DateTime.UtcNow,
        }).ToList();
    }
}
