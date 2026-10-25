namespace Finama.Commerce.Core.DTOs;

/// <summary>Champ personnalisé d'une catégorie. Type : texte | nombre | liste | date. Les sous-catégories héritent des champs du parent.</summary>
public record ChampPersonnaliseDto(string Nom, string Type, bool Obligatoire, List<string>? Options);

// ── Catégories / rayons ──
public record CategorieCommerceDto(Guid Id, string Libelle, Guid? ParentId, int NbProduits, List<ChampPersonnaliseDto> Champs);
public record SaveCategorieRequest(string Libelle, Guid? ParentId, List<ChampPersonnaliseDto>? Champs);

// ── Éditeurs (module Librairie, optionnel) ──
public record EditeurCommerceDto(Guid Id, string Nom, string? Contact, bool EstActif, int NbTitres);
public record SaveEditeurRequest(string Nom, string? Contact);
