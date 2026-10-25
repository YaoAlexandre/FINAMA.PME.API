namespace Finama.Commerce.Core.Domain;

public static class MoneyMath
{
    public static decimal Arrondir(decimal valeur) => Math.Round(valeur, 2, MidpointRounding.AwayFromZero);

    /// <summary>Les prix de détail sont saisis TTC : le HT est déduit du TTC, la TVA est le solde.</summary>
    public static (decimal Ht, decimal Tva) Decomposer(decimal ttc, decimal tauxTva)
    {
        var ht = Arrondir(ttc / (1 + tauxTva / 100m));
        return (ht, ttc - ht);
    }
}

public static class Gs1
{
    /// <summary>Clé de contrôle GS1 (EAN-8, UPC-12, EAN-13, GTIN-14) : poids 3/1 en partant de la droite.</summary>
    public static bool ChecksumValide(string chiffres)
    {
        if (chiffres.Length is not (8 or 12 or 13 or 14) || !chiffres.All(char.IsDigit)) return false;
        var somme = 0;
        for (var i = 0; i < chiffres.Length - 1; i++)
        {
            var poids = (chiffres.Length - 1 - i) % 2 == 1 ? 3 : 1;
            somme += (chiffres[i] - '0') * poids;
        }
        return (10 - somme % 10) % 10 == chiffres[^1] - '0';
    }

    public static string NormaliserIsbn(string isbn) => new(isbn.Where(char.IsLetterOrDigit).ToArray());

    /// <summary>ISBN-13 : préfixe 978/979 + clé EAN-13 valide.</summary>
    public static bool Isbn13Valide(string isbn)
    {
        var n = NormaliserIsbn(isbn);
        return n.Length == 13 && (n.StartsWith("978") || n.StartsWith("979")) && ChecksumValide(n);
    }
}
