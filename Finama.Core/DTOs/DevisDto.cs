using Finama.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Finama.Core.DTOs;

// Requêtes
public record CreerDevisRequest(
    string Libelle,
    Guid TiersId,
    DateTime? DateExpiration,
    string? Notes,
    List<LigneDevisRequest> Lignes
);

public record LigneDevisRequest(
    string Designation,
    decimal Quantite,
    decimal PrixUnitaireHT,
    decimal TauxTVA
);

public record MettreAJourStatutDevisRequest(StatutDevis Statut);

// Réponses
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
    decimal TotalHT,
    decimal TotalTVA,
    decimal TotalTTC,
    List<LigneDevisDto> Lignes
);

public record LigneDevisDto(
    Guid Id,
    string Designation,
    decimal Quantite,
    decimal PrixUnitaireHT,
    decimal TauxTVA,
    decimal MontantHT,
    decimal MontantTVA,
    decimal MontantTTC
);
