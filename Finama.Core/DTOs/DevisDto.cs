using Finama.Core.Entities;

namespace Finama.Core.DTOs;

// ─── Requêtes ────────────────────────────────────────────────────────────────
public record MettreAJourStatutDevisRequest(StatutDevis Statut);
public record CreerDevisRequest(
    string Libelle,
    Guid TiersId,
    DateTime? DateExpiration,
    string? Notes,
    List<LigneDevisRequest> Lignes,
    decimal RemiseGlobalePourcent = 0,
    string? Conditions = null);

public record LigneDevisRequest(
    string Designation,
    decimal Quantite,
    decimal PrixUnitaireHT,
    decimal TauxTVA,
    decimal RemisePourcent = 0,
    Guid? ProduitId = null);

public record RefuserDevisRequest(string? Motif);

/// <summary>Qui consulte : un commercial ne voit que ses devis, le gérant / comptable voit tout.</summary>
public record ContexteVendeur(Guid UtilisateurId, bool VoitTout);

// ─── Réponses ────────────────────────────────────────────────────────────────

public record DevisDto(
    Guid Id,
    string Numero,
    string Libelle,
    DateTime DateCreation,
    DateTime? DateExpiration,
    StatutDevis Statut,
    string StatutLibelle,
    Guid TiersId,
    string TiersNom,
    string? TiersEmail,
    string? TiersTelephone,
    decimal SousTotalHT,
    decimal RemiseGlobalePourcent,
    decimal RemiseGlobale,
    decimal TotalHT,
    decimal TotalTVA,
    decimal TotalTTC,
    string? Notes,
    string? Conditions,
    DateTime? DateEnvoi,
    DateTime? DateDecision,
    string? MotifRefus,
    Guid? FactureId,
    string? FactureNumero,
    int Version,
    string CreeParNom,
    List<LigneDevisDto> Lignes);

public record LigneDevisDto(
    Guid Id,
    int Ordre,
    Guid? ProduitId,
    string Designation,
    decimal Quantite,
    decimal PrixUnitaireHT,
    decimal RemisePourcent,
    decimal TauxTVA,
    decimal MontantHT,
    decimal MontantTVA,
    decimal MontantTTC);

public record DevisResumeDto(
    Guid Id,
    string Numero,
    string Libelle,
    DateTime DateCreation,
    DateTime? DateExpiration,
    StatutDevis Statut,
    string StatutLibelle,
    string TiersNom,
    decimal TotalHT,
    decimal TotalTTC,
    string CreeParNom,
    string? FactureNumero);

public record VentesTableauBordDto(
    int NbBrouillons,
    int NbEnvoyes,
    decimal MontantEnAttente,
    int NbAcceptesNonFactures,
    decimal MontantAcceptesNonFactures,
    decimal TauxTransformation,          // acceptés / (acceptés + refusés + expirés), sur 12 mois
    decimal CaDevisFacturesMois,
    int NbExpirentSous7Jours,
    List<DevisResumeDto> AExpirer,
    List<DevisResumeDto> Derniers);

public record ConversionFactureDto(Guid FactureId, string FactureNumero);

public static class StatutsDevis
{
    public static string Libelle(StatutDevis s) => s switch
    {
        StatutDevis.Brouillon => "Brouillon",
        StatutDevis.Envoye => "Envoyé",
        StatutDevis.Accepte => "Accepté",
        StatutDevis.Refuse => "Refusé",
        StatutDevis.Expire => "Expiré",
        StatutDevis.Converti => "Facturé",
        _ => "Inconnu"
    };
}
