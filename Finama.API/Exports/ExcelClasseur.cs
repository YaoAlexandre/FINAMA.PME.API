using ClosedXML.Excel;

namespace Finama.API.Exports;

public enum FormatColonne { Texte, Montant, Quantite, Entier, Date, DateHeure, Pourcentage }

public sealed record ColonneExcel(string Entete, double Largeur = 14, FormatColonne Format = FormatColonne.Texte);

/// <summary>
/// Classeur Excel au style Finama : en-tête entreprise / état / période, ligne de titres figée,
/// filtres, formats FCFA, totaux par formule (recalculés si l'utilisateur modifie une valeur),
/// mise en page d'impression (paysage, ajusté à la largeur, titres répétés).
/// </summary>
public sealed class ExcelClasseur : IEdition
{
    internal static readonly XLColor Vert = XLColor.FromHtml("#198754");
    internal static readonly XLColor VertClair = XLColor.FromHtml("#E8F5EE");
    internal static readonly XLColor Gris = XLColor.FromHtml("#F1F3F5");

    private readonly XLWorkbook _classeur = new();
    private readonly string _entreprise;

    public ExcelClasseur(string entreprise)
    {
        _entreprise = string.IsNullOrWhiteSpace(entreprise) ? "Finama" : entreprise;
        _classeur.Author = "Finama";
    }

    public string TypeMime => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public string Extension => "xlsx";

    public IFeuilleEdition AjouterFeuille(string nom, string titre, string? sousTitre, IReadOnlyList<ColonneExcel> colonnes)
        => new FeuilleExcel(_classeur.Worksheets.Add(NomFeuilleValide(nom)), _entreprise, titre, sousTitre, colonnes);

    public byte[] Generer()
    {
        using var flux = new MemoryStream();
        _classeur.SaveAs(flux);
        return flux.ToArray();
    }

    public void Dispose() => _classeur.Dispose();

    // Excel : 31 caractères max, sans : \ / ? * [ ]
    private static string NomFeuilleValide(string nom)
    {
        var propre = new string(nom.Where(c => !":\\/?*[]".Contains(c)).ToArray());
        return propre.Length > 31 ? propre[..31] : propre;
    }
}

public sealed class FeuilleExcel : IFeuilleEdition
{
    private const int LigneEntetes = 5;

    private readonly IXLWorksheet _ws;
    private readonly IReadOnlyList<ColonneExcel> _colonnes;
    private readonly int _premiereLigneDonnees = LigneEntetes + 1;
    private int _ligne = LigneEntetes + 1;

    internal FeuilleExcel(IXLWorksheet ws, string entreprise, string titre, string? sousTitre, IReadOnlyList<ColonneExcel> colonnes)
    {
        _ws = ws;
        _colonnes = colonnes;
        var derniere = colonnes.Count;

        _ws.Cell(1, 1).Value = entreprise;
        _ws.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(12).Font.SetFontColor(ExcelClasseur.Vert);

        _ws.Cell(2, 1).Value = titre;
        _ws.Cell(2, 1).Style.Font.SetBold().Font.SetFontSize(15);

        _ws.Cell(3, 1).Value = $"{sousTitre}{(string.IsNullOrWhiteSpace(sousTitre) ? "" : " · ")}Édité le {DateTime.Now:dd/MM/yyyy à HH:mm}";
        _ws.Cell(3, 1).Style.Font.SetItalic().Font.SetFontColor(XLColor.Gray);

        for (var c = 1; c <= derniere; c++)
        {
            var cell = _ws.Cell(LigneEntetes, c);
            cell.Value = colonnes[c - 1].Entete;
            cell.Style.Font.SetBold().Font.SetFontColor(XLColor.White)
                .Fill.SetBackgroundColor(ExcelClasseur.Vert)
                .Alignment.SetWrapText(true)
                .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
                .Alignment.SetHorizontal(EstNumerique(colonnes[c - 1].Format) ? XLAlignmentHorizontalValues.Right : XLAlignmentHorizontalValues.Left);
            _ws.Column(c).Width = colonnes[c - 1].Largeur;
        }
        _ws.Row(LigneEntetes).Height = 30;
    }

    /// <summary>Ligne de données. Les valeurs null laissent la cellule vide.</summary>
    public IFeuilleEdition AjouterLigne(params object?[] valeurs)
    {
        Ecrire(_ligne, valeurs);
        _ligne++;
        return this;
    }

    /// <summary>Ligne de section (ex. « ACTIF IMMOBILISÉ ») sur toute la largeur, fond gris.</summary>
    public IFeuilleEdition AjouterSection(string libelle)
    {
        var plage = _ws.Range(_ligne, 1, _ligne, _colonnes.Count);
        plage.Style.Fill.SetBackgroundColor(ExcelClasseur.Gris).Font.SetBold();
        _ws.Cell(_ligne, 1).Value = libelle;
        _ligne++;
        return this;
    }

    /// <summary>Ligne de total en gras : valeurs fournies (sous-totaux calculés côté serveur).</summary>
    public IFeuilleEdition AjouterLigneTotal(params object?[] valeurs)
    {
        Ecrire(_ligne, valeurs);
        _ws.Range(_ligne, 1, _ligne, _colonnes.Count).Style
            .Font.SetBold()
            .Fill.SetBackgroundColor(ExcelClasseur.VertClair)
            .Border.SetTopBorder(XLBorderStyleValues.Thin);
        _ligne++;
        return this;
    }

    /// <summary>Numéro de la prochaine ligne écrite (pour borner un sous-total).</summary>
    public int LigneCourante => _ligne;

    /// <summary>
    /// Sous-total par formule SOUS.TOTAL depuis <paramref name="depuisLigne"/> : le total général
    /// (lui aussi en SOUS.TOTAL) l'ignore automatiquement, donc pas de double comptage.
    /// </summary>
    public IFeuilleEdition AjouterSousTotalFormule(string libelle, int depuisLigne, params int[] colonnesASommer)
    {
        if (_ligne <= depuisLigne) return this;

        _ws.Cell(_ligne, 1).Value = libelle;
        foreach (var c in colonnesASommer)
        {
            var lettre = _ws.Column(c).ColumnLetter();
            var cell = _ws.Cell(_ligne, c);
            cell.FormulaA1 = $"SUBTOTAL(9,{lettre}{depuisLigne}:{lettre}{_ligne - 1})";
            AppliquerFormat(cell, _colonnes[c - 1].Format);
        }
        _ws.Range(_ligne, 1, _ligne, _colonnes.Count).Style
            .Font.SetBold()
            .Fill.SetBackgroundColor(ExcelClasseur.VertClair)
            .Border.SetTopBorder(XLBorderStyleValues.Thin);
        _ligne++;
        return this;
    }

    /// <summary>Total général par formule SOMME sur les colonnes indiquées (1 = colonne A).</summary>
    public IFeuilleEdition AjouterTotalFormule(string libelle, params int[] colonnesASommer)
    {
        if (_ligne == _premiereLigneDonnees) return this;   // aucune donnée

        _ws.Cell(_ligne, 1).Value = libelle;
        foreach (var c in colonnesASommer)
        {
            var lettre = _ws.Column(c).ColumnLetter();
            var cell = _ws.Cell(_ligne, c);
            cell.FormulaA1 = $"SUBTOTAL(9,{lettre}{_premiereLigneDonnees}:{lettre}{_ligne - 1})";   // ignore les lignes filtrées
            AppliquerFormat(cell, _colonnes[c - 1].Format);
        }

        _ws.Range(_ligne, 1, _ligne, _colonnes.Count).Style
            .Font.SetBold()
            .Fill.SetBackgroundColor(ExcelClasseur.VertClair)
            .Border.SetTopBorder(XLBorderStyleValues.Medium);
        _ligne++;
        return this;
    }

    /// <summary>Ligne de remarque en italique sous le tableau.</summary>
    public IFeuilleEdition AjouterNote(string texte)
    {
        _ligne++;
        _ws.Cell(_ligne, 1).Value = texte;
        _ws.Cell(_ligne, 1).Style.Font.SetItalic().Font.SetFontColor(XLColor.Gray);
        _ligne++;
        return this;
    }

    /// <summary>Filtres, volet figé, impression. À appeler une fois toutes les lignes écrites.</summary>
    public void Terminer(bool filtres = true)
    {
        var derniere = _colonnes.Count;

        if (filtres && _ligne > _premiereLigneDonnees)
            _ws.Range(LigneEntetes, 1, _ligne - 1, derniere).SetAutoFilter();

        _ws.SheetView.FreezeRows(LigneEntetes);

        var mep = _ws.PageSetup;
        mep.PageOrientation = derniere > 6 ? XLPageOrientation.Landscape : XLPageOrientation.Portrait;
        mep.PaperSize = XLPaperSize.A4Paper;
        mep.FitToPages(1, 0);
        mep.SetRowsToRepeatAtTop(LigneEntetes, LigneEntetes);
        mep.Margins.SetLeft(0.4).SetRight(0.4).SetTop(0.5).SetBottom(0.5);
        mep.Footer.Right.AddText("Page &P / &N");
        mep.Footer.Left.AddText("Finama");
    }

    private void Ecrire(int ligne, object?[] valeurs)
    {
        for (var i = 0; i < valeurs.Length && i < _colonnes.Count; i++)
        {
            var cell = _ws.Cell(ligne, i + 1);
            cell.Value = valeurs[i] switch
            {
                null => Blank.Value,
                string s => s,
                decimal d => d,
                double d => d,
                int n => n,
                long n => n,
                bool b => b ? "Oui" : "Non",
                DateTime dt => dt,
                DateOnly d => d.ToDateTime(TimeOnly.MinValue),
                _ => valeurs[i]!.ToString()
            };
            AppliquerFormat(cell, _colonnes[i].Format);
        }
    }

    private static void AppliquerFormat(IXLCell cell, FormatColonne format)
    {
        switch (format)
        {
            case FormatColonne.Montant:
                cell.Style.NumberFormat.Format = "#,##0;[Red]-#,##0;\"\"";   // zéro affiché vide : plus lisible
                cell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
                break;
            case FormatColonne.Quantite:
                cell.Style.NumberFormat.Format = "#,##0.###";
                cell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
                break;
            case FormatColonne.Entier:
                cell.Style.NumberFormat.Format = "#,##0";
                cell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
                break;
            case FormatColonne.Date:
                cell.Style.DateFormat.Format = "dd/mm/yyyy";
                break;
            case FormatColonne.DateHeure:
                cell.Style.DateFormat.Format = "dd/mm/yyyy hh:mm";
                break;
            case FormatColonne.Pourcentage:
                cell.Style.NumberFormat.Format = "0.0%";
                cell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
                break;
        }
    }

    private static bool EstNumerique(FormatColonne f) =>
        f is FormatColonne.Montant or FormatColonne.Quantite or FormatColonne.Entier or FormatColonne.Pourcentage;
}
