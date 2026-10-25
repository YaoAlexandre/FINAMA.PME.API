namespace Finama.API.Exports;

/// <summary>
/// Édition multi-format : le contrôleur décrit le contenu une seule fois (feuilles, lignes, totaux),
/// l'implémentation produit le fichier — Excel (ExcelClasseur) ou PDF (PdfEdition).
/// Les deux formats affichent donc toujours exactement les mêmes chiffres.
/// </summary>
public interface IEdition : IDisposable
{
    string TypeMime { get; }
    string Extension { get; }
    IFeuilleEdition AjouterFeuille(string nom, string titre, string? sousTitre, IReadOnlyList<ColonneExcel> colonnes);
    byte[] Generer();
}

public interface IFeuilleEdition
{
    /// <summary>Repère de la prochaine ligne écrite (borne de début d'un sous-total).</summary>
    int LigneCourante { get; }

    IFeuilleEdition AjouterLigne(params object?[] valeurs);
    IFeuilleEdition AjouterSection(string libelle);
    IFeuilleEdition AjouterLigneTotal(params object?[] valeurs);
    IFeuilleEdition AjouterSousTotalFormule(string libelle, int depuisLigne, params int[] colonnesASommer);
    IFeuilleEdition AjouterTotalFormule(string libelle, params int[] colonnesASommer);
    IFeuilleEdition AjouterNote(string texte);
    void Terminer(bool filtres = true);
}

public static class Editions
{
    /// <summary>« pdf » → PDF ; toute autre valeur → Excel.</summary>
    public static IEdition Creer(string? format, string entreprise, byte[]? logo)
        => string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase)
            ? new PdfEdition(entreprise, logo)
            : new ExcelClasseur(entreprise);
}
