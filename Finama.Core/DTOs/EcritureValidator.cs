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
            .Must(d => d.Date <= DateTime.UtcNow.Date.AddDays(1))
            .WithMessage("La date ne peut pas être dans le futur.");

        RuleFor(x => x.Libelle)
            .NotEmpty().WithMessage("Le libellé est obligatoire.")
            .MaximumLength(300).WithMessage("Le libellé ne peut pas dépasser 300 caractères.");

        RuleFor(x => x.Journal)
            .NotEmpty().WithMessage("Le journal est obligatoire.")
            .Must(j => j != null && JournauxValides.Contains(j.Trim().ToUpperInvariant()))
            .WithMessage($"Journal invalide. Valeurs acceptées : {string.Join(", ", JournauxValides)}");

        RuleFor(x => x.ExerciceId)
            .NotEmpty().WithMessage("L'exercice comptable est obligatoire.");

        RuleFor(x => x.Lignes)
            .NotEmpty().WithMessage("L'écriture doit contenir au moins deux lignes.")
            .Must(l => l != null && l.Count >= 2).WithMessage("Une écriture doit avoir au minimum 2 lignes.");

        RuleForEach(x => x.Lignes).SetValidator(new LigneEcritureValidator());

        // Contrôle de forme uniquement : l'équilibre exact EN DEVISE DE BASE (taux inclus)
        // est vérifié par EcritureService, seul à connaître la devise du tenant.
        RuleFor(x => x.Lignes)
            .Must(l => l.Sum(x => x.Debit) > 0)
            .When(x => x.Lignes != null && x.Lignes.Count >= 2)
            .WithMessage("Le total de l'écriture ne peut pas être nul.");
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
            .Length(3).When(x => !string.IsNullOrWhiteSpace(x.Devise))
            .WithMessage("Le code devise doit faire 3 caractères (ex : XOF, EUR, USD).");

        RuleFor(x => x.TauxChange)
            .GreaterThan(0).When(x => x.TauxChange.HasValue)
            .WithMessage("Le taux de change doit être positif.");
    }
}
