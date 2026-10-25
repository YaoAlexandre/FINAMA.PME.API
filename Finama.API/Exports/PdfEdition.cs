using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Finama.API.Exports;

/// <summary>
/// Édition PDF (QuestPDF) au style Finama : logo + entreprise + titre + période, tableau avec en-tête répété
/// sur chaque page, sections, sous-totaux et totaux calculés, pagination « Page x / y ».
/// Même description que l'édition Excel (IEdition) : les chiffres sont identiques dans les deux formats.
/// </summary>
public sealed class PdfEdition : IEdition
{
    internal const string Vert = "#198754";
    internal const string VertClair = "#E8F5EE";
    internal const string Gris = "#F1F3F5";
    internal const string GrisTexte = "#374151";
    internal const string GrisSoft = "#6B7280";
    internal const string Bordure = "#E5E7EB";

    private readonly string _entreprise;
    private readonly byte[]? _logo;
    private readonly List<PdfFeuille> _feuilles = new();

    public PdfEdition(string entreprise, byte[]? logo)
    {
        _entreprise = string.IsNullOrWhiteSpace(entreprise) ? "Finama" : entreprise;
        _logo = logo;
    }

    public string TypeMime => "application/pdf";
    public string Extension => "pdf";

    public IFeuilleEdition AjouterFeuille(string nom, string titre, string? sousTitre, IReadOnlyList<ColonneExcel> colonnes)
    {
        var feuille = new PdfFeuille(titre, sousTitre, colonnes);
        _feuilles.Add(feuille);
        return feuille;
    }

    public byte[] Generer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var edition = DateTime.Now;

        return Document.Create(doc =>
        {
            foreach (var f in _feuilles)
            {
                doc.Page(page =>
                {
                    page.Size(f.Colonnes.Count > 6 ? PageSizes.A4.Landscape() : PageSizes.A4);
                    page.Margin(12, Unit.Millimetre);
                    page.DefaultTextStyle(x => x.FontSize(8).FontColor(GrisTexte));

                    // En-tête
                    page.Header().PaddingBottom(8).Row(row =>
                    {
                        if (_logo is not null)
                        {
                            row.ConstantItem(55).Height(42).AlignMiddle().Image(_logo).FitArea();
                            row.ConstantItem(8);
                        }
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(_entreprise).FontSize(10).Bold().FontColor(Vert);
                            c.Item().Text(f.Titre).FontSize(15).Bold();
                            c.Item().Text($"{f.SousTitre}{(string.IsNullOrWhiteSpace(f.SousTitre) ? "" : " · ")}Édité le {edition:dd/MM/yyyy à HH:mm}")
                                .FontSize(8).Italic().FontColor(GrisSoft);
                        });
                    });

                    // Tableau
                    page.Content().Column(col =>
                    {
                        col.Item().Table(table => f.Dessiner(table));

                        foreach (var note in f.Notes)
                            col.Item().PaddingTop(6).Text(note).FontSize(8).Italic().FontColor(GrisSoft);
                    });

                    // Pied de page
                    page.Footer().PaddingTop(4).BorderTop(0.5f).BorderColor(Bordure).Row(r =>
                    {
                        r.RelativeItem().Text($"Finama — {_entreprise}").FontSize(7).FontColor(GrisSoft);
                        r.RelativeItem().AlignRight().Text(t =>
                        {
                            t.DefaultTextStyle(x => x.FontSize(7).FontColor(GrisSoft));
                            t.Span("Page ");
                            t.CurrentPageNumber();
                            t.Span(" / ");
                            t.TotalPages();
                        });
                    });
                });
            }
        }).GeneratePdf();
    }

    public void Dispose() { }
}

internal sealed class PdfFeuille : IFeuilleEdition
{
    private enum TypeLigne { Donnee, Section, Total, GrandTotal }
    private sealed record Ligne(TypeLigne Type, object?[] Valeurs);

    private static readonly CultureInfo Fr = new("fr-FR");
    private readonly List<Ligne> _lignes = new();

    public string Titre { get; }
    public string? SousTitre { get; }
    public IReadOnlyList<ColonneExcel> Colonnes { get; }
    public List<string> Notes { get; } = new();

    public PdfFeuille(string titre, string? sousTitre, IReadOnlyList<ColonneExcel> colonnes)
    {
        Titre = titre;
        SousTitre = sousTitre;
        Colonnes = colonnes;
    }

    public int LigneCourante => _lignes.Count;

    public IFeuilleEdition AjouterLigne(params object?[] valeurs) { _lignes.Add(new(TypeLigne.Donnee, valeurs)); return this; }

    public IFeuilleEdition AjouterSection(string libelle) { _lignes.Add(new(TypeLigne.Section, [libelle])); return this; }

    public IFeuilleEdition AjouterLigneTotal(params object?[] valeurs) { _lignes.Add(new(TypeLigne.Total, valeurs)); return this; }

    public IFeuilleEdition AjouterSousTotalFormule(string libelle, int depuisLigne, params int[] colonnesASommer)
    {
        if (_lignes.Count <= depuisLigne) return this;
        _lignes.Add(new(TypeLigne.Total, Sommes(libelle, _lignes.Skip(depuisLigne), colonnesASommer)));
        return this;
    }

    public IFeuilleEdition AjouterTotalFormule(string libelle, params int[] colonnesASommer)
    {
        if (!_lignes.Any(l => l.Type == TypeLigne.Donnee)) return this;
        _lignes.Add(new(TypeLigne.GrandTotal, Sommes(libelle, _lignes, colonnesASommer)));
        return this;
    }

    public IFeuilleEdition AjouterNote(string texte) { Notes.Add(texte); return this; }

    public void Terminer(bool filtres = true) { }

    /// <summary>Même logique que SOUS.TOTAL d'Excel : seules les lignes de données sont additionnées.</summary>
    private object?[] Sommes(string libelle, IEnumerable<Ligne> lignes, int[] colonnes)
    {
        var valeurs = new object?[Colonnes.Count];
        valeurs[0] = libelle;
        var donnees = lignes.Where(l => l.Type == TypeLigne.Donnee).ToList();
        foreach (var c in colonnes)
        {
            var i = c - 1;
            valeurs[i] = donnees.Sum(l => i < l.Valeurs.Length ? EnDecimal(l.Valeurs[i]) : 0m);
        }
        return valeurs;
    }

    internal void Dessiner(TableDescriptor table)
    {
        var n = Colonnes.Count;

        table.ColumnsDefinition(cols =>
        {
            foreach (var c in Colonnes) cols.RelativeColumn((float)Math.Max(4, c.Largeur));
        });

        table.Header(h =>
        {
            foreach (var c in Colonnes)
            {
                var cell = h.Cell().Background(PdfEdition.Vert).PaddingVertical(4).PaddingHorizontal(3);
                (EstNumerique(c.Format) ? cell.AlignRight() : cell)
                    .Text(c.Entete).FontColor(Colors.White).Bold().FontSize(7.5f);
            }
        });

        var pair = false;
        foreach (var l in _lignes)
        {
            switch (l.Type)
            {
                case TypeLigne.Section:
                    table.Cell().ColumnSpan((uint)n).Background(PdfEdition.Gris).PaddingVertical(3).PaddingHorizontal(3)
                        .Text(l.Valeurs[0]?.ToString() ?? "").Bold();
                    pair = false;
                    break;

                default:
                    var fond = l.Type switch
                    {
                        TypeLigne.Total or TypeLigne.GrandTotal => PdfEdition.VertClair,
                        _ => (pair = !pair) ? Colors.White.ToString() : "#FAFAFA"
                    };
                    var gras = l.Type != TypeLigne.Donnee;

                    for (var i = 0; i < n; i++)
                    {
                        var col = Colonnes[i];
                        var cell = table.Cell().Background(fond)
                            .BorderBottom(l.Type == TypeLigne.GrandTotal ? 1f : 0.3f).BorderColor(PdfEdition.Bordure)
                            .PaddingVertical(2.5f).PaddingHorizontal(3);
                        var texte = (EstNumerique(col.Format) ? cell.AlignRight() : cell)
                            .Text(Formater(i < l.Valeurs.Length ? l.Valeurs[i] : null, col.Format));
                        if (gras) texte.Bold();
                    }
                    break;
            }
        }
    }

    private static string Formater(object? v, FormatColonne f)
    {
        if (v is null) return "";
        string s = f switch
        {
            FormatColonne.Montant => Montant(EnDecimal(v)),
            FormatColonne.Quantite => EnDecimal(v).ToString("#,##0.###", Fr),
            FormatColonne.Entier => EnDecimal(v).ToString("#,##0", Fr),
            FormatColonne.Pourcentage => (EnDecimal(v)).ToString("P1", Fr),
            FormatColonne.Date => v switch { DateTime d => d.ToString("dd/MM/yyyy"), DateOnly d => d.ToString("dd/MM/yyyy"), _ => v.ToString() ?? "" },
            FormatColonne.DateHeure => v is DateTime dh ? dh.ToString("dd/MM/yyyy HH:mm") : v.ToString() ?? "",
            _ => v switch { bool b => b ? "Oui" : "Non", DateTime d => d.ToString("dd/MM/yyyy"), _ => v.ToString() ?? "" }
        };
        // Espaces insécables fines du français → espace simple (affichage sûr avec toutes les polices)
        return s.Replace('\u202F', ' ').Replace('\u00A0', ' ');
    }

    private static string Montant(decimal m) => m == 0 ? "" : m.ToString("#,##0", Fr);   // zéro vide, comme dans Excel

    private static decimal EnDecimal(object? v) => v switch
    {
        null => 0m,
        decimal d => d,
        double d => (decimal)d,
        float f => (decimal)f,
        int i => i,
        long l => l,
        _ => decimal.TryParse(v.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var r) ? r : 0m
    };

    private static bool EstNumerique(FormatColonne f) =>
        f is FormatColonne.Montant or FormatColonne.Quantite or FormatColonne.Entier or FormatColonne.Pourcentage;
}
