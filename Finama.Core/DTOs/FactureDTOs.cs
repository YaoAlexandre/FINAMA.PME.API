namespace Finama.Core.DTOs;

public record FactureDetailDto(
    Guid Id,
    string Numero,
    string Type,
    string Statut,
    DateTime DateFacture,
    DateTime? DateEcheance,
    string Devise,

    // Émetteur (tenant)
    string EntrepriseNom,
    string? EntrepriseAdresse,
    string? EntrepriseTelephone,
    string EntrepriseEmail,
    string? EntrepriseNumeroFiscal,
    string EntreprisePays,
    string EntrepriseDeviseSymbole,

    // Client/Fournisseur
    string TiersNom,
    string? TiersAdresse,
    string? TiersTelephone,
    string? TiersEmail,
    string? TiersNumeroFiscal,
    string TiersCode,
    // Dans FactureDetailDto.cs
    string? EntrepriseBanqueNom, 
    string? EntrepriseBanqueBIC, 

// Lignes
List<LigneFactureDetailDto> Lignes,

    // Totaux
    decimal TotalHT,
    decimal TotalTVA,
    decimal TotalTTC,
    decimal MontantRegle,
    decimal Solde,

    string? Notes
);

public record LigneFactureDetailDto(
    string Description,
    decimal Quantite,
    decimal PrixUnitaireHT,
    decimal TauxTVA,
    decimal MontantHT,
    decimal MontantTVA,
    decimal MontantTTC
);

public record CreerFactureRequest(
    TypeFactureDto Type,
    DateTime DateFacture,
    DateTime? DateEcheance,
    Guid TiersId,
    Guid ExerciceId,
    string? Notes,
    List<CreerLigneFactureRequest> Lignes
);

public record CreerLigneFactureRequest(
    string Description,
    decimal Quantite,
    decimal PrixUnitaireHT,
    decimal TauxTVA,
    Guid? CompteProduitsId
);

public enum TypeFactureDto
{
    Vente = 0,
    Achat = 1,
}
