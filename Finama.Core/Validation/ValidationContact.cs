using System.Text.RegularExpressions;

namespace Finama.Core.Validation;

/// <summary>
/// Contrôles communs des coordonnées (clients caisse, clients/fournisseurs comptables, entreprise).
/// Lève ArgumentException (→ HTTP 400 avec le message) ; renvoie les valeurs normalisées à enregistrer.
/// </summary>
public static partial class ValidationContact
{
    /// <summary>
    /// Norme UIT E.164 : 15 chiffres au maximum pour un numéro international complet.
    /// Saisie autorisée : 15 chiffres + « + » + 1 espace de confort = 17 caractères.
    /// </summary>
    public const int TelephoneChiffresMax = 15;
    public const int TelephoneChiffresMin = 8;
    public const int TelephoneSaisieMax = 17;
    public const int EmailMax = 254;
    public const int NomMax = 150;
    public const int AdresseMax = 250;
    public const int NifMax = 30;
    public const decimal MontantMax = 1_000_000_000_000m;

    [GeneratedRegex(@"^\+?[0-9 ]+$")]
    private static partial Regex CaracteresTelephone();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$")]
    private static partial Regex FormeEmail();

    [GeneratedRegex(@"^[A-Za-z0-9\-/ ]+$")]
    private static partial Regex CaracteresNif();

    /// <summary>Téléphone : chiffres, « + » en tête, espaces. Renvoie le numéro sans espaces (ex. +22890000001), ou null si vide.</summary>
    public static string? Telephone(string? saisie, string champ = "Téléphone")
    {
        if (string.IsNullOrWhiteSpace(saisie)) return null;
        var t = saisie.Trim();

        if (t.Length > TelephoneSaisieMax)
            throw new ArgumentException($"{champ} : {TelephoneSaisieMax} caractères maximum.");
        if (!CaracteresTelephone().IsMatch(t))
            throw new ArgumentException($"{champ} : seuls les chiffres, les espaces et le « + » initial sont acceptés.");

        var normalise = t.Replace(" ", "");
        var chiffres = normalise.TrimStart('+').Length;
        if (chiffres < TelephoneChiffresMin)
            throw new ArgumentException($"{champ} : {TelephoneChiffresMin} chiffres au minimum.");
        if (chiffres > TelephoneChiffresMax)
            throw new ArgumentException($"{champ} : {TelephoneChiffresMax} chiffres au maximum (norme internationale E.164).");

        return normalise;
    }

    /// <summary>E-mail : forme nom@domaine.ext, en minuscules, ou null si vide.</summary>
    public static string? Email(string? saisie, string champ = "E-mail")
    {
        if (string.IsNullOrWhiteSpace(saisie)) return null;
        var e = saisie.Trim().ToLowerInvariant();
        if (e.Length > EmailMax || !FormeEmail().IsMatch(e))
            throw new ArgumentException($"{champ} invalide : format attendu nom@domaine.com");
        return e;
    }

    public static string Nom(string? saisie, string champ = "Nom")
    {
        if (string.IsNullOrWhiteSpace(saisie))
            throw new ArgumentException($"{champ} : obligatoire.");
        var n = saisie.Trim();
        if (n.Length < 2) throw new ArgumentException($"{champ} : 2 caractères au minimum.");
        if (n.Length > NomMax) throw new ArgumentException($"{champ} : {NomMax} caractères au maximum.");
        return n;
    }

    public static string? Texte(string? saisie, int max, string champ)
    {
        if (string.IsNullOrWhiteSpace(saisie)) return null;
        var t = saisie.Trim();
        if (t.Length > max) throw new ArgumentException($"{champ} : {max} caractères au maximum.");
        return t;
    }

    public static string? Nif(string? saisie, string champ = "NIF")
    {
        var n = Texte(saisie, NifMax, champ);
        if (n is not null && !CaracteresNif().IsMatch(n))
            throw new ArgumentException($"{champ} : lettres, chiffres, tirets et barres obliques uniquement.");
        return n?.ToUpperInvariant();
    }

    public static decimal MontantPositif(decimal valeur, string champ)
    {
        if (valeur < 0) throw new ArgumentException($"{champ} : ne peut pas être négatif.");
        if (valeur > MontantMax) throw new ArgumentException($"{champ} : montant trop élevé.");
        return Math.Round(valeur, 2);
    }
}
