using Finama.Core.Entities;

namespace Finama.Commerce.Core.Domain;

public enum TypeProduit { Standard = 0, Vrac = 1, Livre = 2, Service = 3 }   // Service : prestation sans stock
public enum UniteMesure { Piece = 0, Kilogramme = 1, Gramme = 2, Litre = 3, Metre = 4, Heure = 5 }

public class Categorie : TenantEntity
{
    public string Libelle { get; set; } = "";
    public Guid? ParentId { get; set; }
    public Categorie? Parent { get; set; }
    public string? ChampsJson { get; set; }              // champs personnalisés de la catégorie (Taille, Marque, Dosage...), hérités par les sous-catégories
}

public class Editeur : TenantEntity
{
    public string Nom { get; set; } = "";
    public string? Contact { get; set; }
    public bool EstActif { get; set; } = true;
}



public class Produit : TenantEntity
{
    public string Reference { get; set; } = "";          // SKU interne, unique par tenant
    public string Libelle { get; set; } = "";
    public string? Description { get; set; }
    public TypeProduit Type { get; set; }
    public UniteMesure Unite { get; set; } = UniteMesure.Piece;
    public Guid? CategorieId { get; set; }
    public Categorie? Categorie { get; set; }

    public decimal PrixVenteTTC { get; set; }            // prix de détail TTC (par unité de mesure)
    public decimal TauxTva { get; set; }                 // en %, ex. 18.00
    public decimal PrixAchatMoyen { get; set; }          // CMUP, 4 décimales (voir configuration)
    public decimal? DernierPrixAchat { get; set; }

    public bool SuiviLots { get; set; }                  // lots + date de péremption (DLC), sortie FEFO
    public int JoursAlertePeremption { get; set; } = 30;
    public decimal SeuilReappro { get; set; }            // 0 = pas d'alerte
    public decimal QuantiteReappro { get; set; }         // quantité suggérée à commander
    public bool EstActif { get; set; } = true;
    public string? AttributsJson { get; set; }           // attributs libres par secteur : Taille, Couleur, Editeur...

    public bool EstVenduAuPoids => Type == TypeProduit.Vrac;
    public bool GereStock => Type != TypeProduit.Service;

    public List<CodeBarresProduit> CodesBarres { get; set; } = [];
    public ProduitLivre? Livre { get; set; }
}

/// <summary>Un produit peut porter plusieurs codes (EAN fabricant, code interne, code balance, ISBN).</summary>
public class CodeBarresProduit : TenantEntity
{
    public Guid ProduitId { get; set; }
    public Produit? Produit { get; set; }
    public string Code { get; set; } = "";
    public bool EstPrincipal { get; set; }
}

/// <summary>Extension 1-1 pour les articles de librairie (clé = ProduitId).</summary>
public class ProduitLivre : ITenantEntity
{
    public Guid ProduitId { get; set; }
    public Guid TenantId { get; set; }
    public Produit? Produit { get; set; }
    public string Isbn { get; set; } = "";               // ISBN-13 normalisé, sans tirets
    public string Auteur { get; set; } = "";
    public Guid? EditeurId { get; set; }
    public Editeur? Editeur { get; set; }
    public string? Collection { get; set; }
    public int? AnneeEdition { get; set; }
    public string? Rayon { get; set; }
    public string? Etagere { get; set; }
    public bool RetournableEditeur { get; set; } = true; // utilisé par le module 4 (retours éditeurs)
}
