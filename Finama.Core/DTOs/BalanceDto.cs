using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Finama.Core.DTOs;

// ─── DTOs LOCAUX POUR L'API (Évite les conflits d'assemblies et résout les types manquants) ───

public record ApiBalanceWrapper(
    Guid ExerciceId,
    ApiBalanceDto Rapport,
    bool EstEquilibree,
    string? Avertissement);

public record ApiBalanceDto(
    int Annee,
    List<ApiBalanceLigneDto> Lignes,
    ApiBalanceTotauxDto Totaux);

public record ApiBalanceLigneDto(
    string Numero, string Libelle, int Classe,
    decimal SoldeOuvertureDebit, decimal SoldeOuvertureCredit,
    decimal MouvementsDebit, decimal MouvementsCredit,
    decimal SoldeFinalDebit, decimal SoldeFinalCredit);

public record ApiBalanceTotauxDto(
    decimal TotalOuvertureDebit, decimal TotalOuvertureCredit,
    decimal TotalMouvementsDebit, decimal TotalMouvementsCredit,
    decimal TotalSoldeFinalDebit, decimal TotalSoldeFinalCredit);