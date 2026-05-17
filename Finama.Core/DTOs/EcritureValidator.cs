using Finama.Core.DTOs;
using FluentValidation;

namespace Finama.Core.Validators;

public class CreerEcritureValidator : AbstractValidator<CreerEcritureRequest>
{
    private static readonly string[] JournauxValides = ["AC", "VT", "BQ", "CA", "OD"];

    public CreerEcritureValidator()
    {
        RuleFor(x => x.DateEcriture)
            .NotEmpty().WithMessage("La date est obligatoire.")
            .LessThanOrEqualTo(DateTime.Today.AddDays(1))
            .WithMessage("La date ne peut pas être dans le futur.");

        RuleFor(x => x.Libelle)
            .NotEmpty().WithMessage("Le libellé est obligatoire.")
            .MaximumLength(300).WithMessage("Le libellé ne peut pas dépasser 300 caractères.");

        RuleFor(x => x.Journal)
            .NotEmpty().WithMessage("Le journal est obligatoire.")
            .Must(j => JournauxValides.Contains(j.ToUpper()))
            .WithMessage($"Journal invalide. Valeurs acceptées : {string.Join(", ", JournauxValides)}");

        RuleFor(x => x.ExerciceId)
            .NotEmpty().WithMessage("L'exercice comptable est obligatoire.");

        RuleFor(x => x.Lignes)
            .NotEmpty().WithMessage("L'écriture doit contenir au moins deux lignes.")
            .Must(l => l.Count >= 2).WithMessage("Une écriture doit avoir au minimum 2 lignes.");

        RuleForEach(x => x.Lignes).SetValidator(new LigneEcritureValidator());

        RuleFor(x => x.Lignes)
            .Must(LignesEquilibrees)
            .WithMessage(req =>
            {
                var debit = req.Lignes.Sum(l => l.Debit);
                var credit = req.Lignes.Sum(l => l.Credit);
                return $"L'écriture n'est pas équilibrée. Débit : {debit:N2} / Crédit : {credit:N2}.";
            });
    }

    private static bool LignesEquilibrees(List<CreerLigneEcritureRequest> lignes)
    {
        var totalDebit = lignes.Sum(l => l.Debit);
        var totalCredit = lignes.Sum(l => l.Credit);
        return Math.Abs(totalDebit - totalCredit) < 0.01m; // tolérance d'arrondi
    }
}

public class LigneEcritureValidator : AbstractValidator<CreerLigneEcritureRequest>
{
    public LigneEcritureValidator()
    {
        RuleFor(x => x.CompteId)
            .NotEmpty().WithMessage("Le compte comptable est obligatoire.");

        RuleFor(x => x.Debit)
            .GreaterThanOrEqualTo(0).WithMessage("Le débit ne peut pas être négatif.");

        RuleFor(x => x.Credit)
            .GreaterThanOrEqualTo(0).WithMessage("Le crédit ne peut pas être négatif.");

        RuleFor(x => x)
            .Must(l => l.Debit > 0 || l.Credit > 0)
            .WithMessage("Chaque ligne doit avoir un débit ou un crédit non nul.")
            .Must(l => !(l.Debit > 0 && l.Credit > 0))
            .WithMessage("Une ligne ne peut pas avoir à la fois un débit et un crédit.");

        RuleFor(x => x.Devise)
            .Length(3).WithMessage("Le code devise doit faire 3 caractères (ex: STD, EUR, USD).");

        RuleFor(x => x.TauxChange)
            .GreaterThan(0).When(x => x.TauxChange.HasValue)
            .WithMessage("Le taux de change doit être positif.");
    }
}
