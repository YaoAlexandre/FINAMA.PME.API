using Finama.Core.DTOs;
using Finama.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Finama.Infrastructure.Services.Commercials;

public interface IDevisPdfService
{
    Task<byte[]> GenererAsync(DevisDto devis, CancellationToken ct = default);
}

/// <summary>PDF du devis : logo, coordonnées, lignes avec remises, totaux, conditions et zone « Bon pour accord ».</summary>
public class DevisPdfService : IDevisPdfService
{
    private const string Vert = "#198754";
    private const string VertClair = "#E8F5EE";
    private const string GrisTexte = "#374151";
    private const string GrisLeger = "#F9FAFB";
    private const string GrisBordure = "#E5E7EB";
    private const string GrisSoft = "#6B7280";

    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;

    public DevisPdfService(AppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<byte[]> GenererAsync(DevisDto d, CancellationToken ct = default)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var e = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == _tenant.TenantId)
            .Select(t => new { t.Nom, t.Adresse, t.Telephone, t.Email, t.NumeroFiscal, t.LogoDataUrl, t.BanqueNom, t.BanqueBIC, Symbole = t.Pays.DeviseSymbole })
            .FirstOrDefaultAsync(ct);

        var nom = e?.Nom ?? "";
        var symbole = string.IsNullOrWhiteSpace(e?.Symbole) ? "FCFA" : e!.Symbole;
        var logo = LogoHelper.Decoder(e?.LogoDataUrl);
        var avecRemiseLigne = d.Lignes.Any(l => l.RemisePourcent > 0);
        string M(decimal v) => $"{v:N0} {symbole}".Replace('\u202F', ' ').Replace('\u00A0', ' ');

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(15, Unit.Millimetre);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(GrisTexte));

                page.Content().Column(col =>
                {
                    // En-tête : entreprise / devis
                    col.Item().Row(row =>
                    {
                        if (logo is not null)
                        {
                            row.ConstantItem(70).Height(60).AlignMiddle().Image(logo).FitArea();
                            row.ConstantItem(10);
                        }
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(nom).FontSize(16).Bold().FontColor(Vert);
                            if (!string.IsNullOrWhiteSpace(e?.Adresse)) c.Item().Text(e!.Adresse).FontSize(8).FontColor(GrisSoft);
                            var contact = string.Join("  |  ", new[] { e?.Telephone, e?.Email }.Where(x => !string.IsNullOrWhiteSpace(x)));
                            if (contact.Length > 0) c.Item().Text(contact).FontSize(8).FontColor(GrisSoft);
                            if (!string.IsNullOrWhiteSpace(e?.NumeroFiscal)) c.Item().Text($"NIF : {e!.NumeroFiscal}").FontSize(8).FontColor(GrisSoft);
                        });
                        row.RelativeItem().AlignRight().Column(c =>
                        {
                            c.Item().AlignRight().Text("DEVIS").FontSize(26).Bold().FontColor(Vert);
                            c.Item().AlignRight().Text($"N° {d.Numero}{(d.Version > 1 ? $" (v{d.Version})" : "")}").FontSize(10);
                            c.Item().AlignRight().Text($"Date : {d.DateCreation.ToLocalTime():dd/MM/yyyy}").FontColor(GrisSoft);
                            if (d.DateExpiration.HasValue)
                                c.Item().AlignRight().Text($"Valable jusqu'au : {d.DateExpiration:dd/MM/yyyy}").Bold();
                        });
                    });

                    col.Item().PaddingVertical(6).LineHorizontal(1).LineColor(VertClair);

                    // Client + objet
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Background(VertClair).Padding(8).Column(c =>
                        {
                            c.Item().Text("CLIENT").FontSize(7).Bold().FontColor(Vert);
                            c.Item().Text(d.TiersNom).FontSize(11).Bold();
                            var contact = string.Join("  |  ", new[] { d.TiersTelephone, d.TiersEmail }.Where(x => !string.IsNullOrWhiteSpace(x)));
                            if (contact.Length > 0) c.Item().Text(contact).FontSize(8).FontColor(GrisSoft);
                        });
                        row.ConstantItem(10);
                        row.RelativeItem().Border(0.5f).BorderColor(GrisBordure).Padding(8).Column(c =>
                        {
                            c.Item().Text("OBJET").FontSize(7).Bold().FontColor(Vert);
                            c.Item().Text(d.Libelle).FontSize(10);
                            if (!string.IsNullOrWhiteSpace(d.CreeParNom))
                                c.Item().Text($"Votre interlocuteur : {d.CreeParNom}").FontSize(8).FontColor(GrisSoft);
                        });
                    });

                    col.Item().Height(10);

                    // Lignes
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(5);
                            c.RelativeColumn(1.2f);
                            c.RelativeColumn(2);
                            if (avecRemiseLigne) c.RelativeColumn(1.2f);
                            c.RelativeColumn(1.2f);
                            c.RelativeColumn(2.2f);
                        });

                        static IContainer Entete(IContainer c) =>
                            c.Background(Vert).Padding(5).DefaultTextStyle(x => x.FontColor(Colors.White).Bold().FontSize(8));

                        table.Header(h =>
                        {
                            h.Cell().Element(Entete).Text("Désignation");
                            h.Cell().Element(Entete).AlignRight().Text("Qté");
                            h.Cell().Element(Entete).AlignRight().Text("P.U. HT");
                            if (avecRemiseLigne) h.Cell().Element(Entete).AlignRight().Text("Remise");
                            h.Cell().Element(Entete).AlignRight().Text("TVA");
                            h.Cell().Element(Entete).AlignRight().Text("Total HT");
                        });

                        var pair = false;
                        foreach (var l in d.Lignes)
                        {
                            pair = !pair;
                            var fond = pair ? Colors.White.ToString() : GrisLeger;
                            IContainer C(IContainer c) => c.Background(fond).BorderBottom(0.3f).BorderColor(GrisBordure).PaddingVertical(5).PaddingHorizontal(5);

                            table.Cell().Element(C).Text(l.Designation);
                            table.Cell().Element(C).AlignRight().Text(l.Quantite.ToString("0.###"));
                            table.Cell().Element(C).AlignRight().Text(M(l.PrixUnitaireHT));
                            if (avecRemiseLigne) table.Cell().Element(C).AlignRight().Text(l.RemisePourcent > 0 ? $"{l.RemisePourcent:0.##} %" : "");
                            table.Cell().Element(C).AlignRight().Text($"{l.TauxTVA:0.##} %");
                            table.Cell().Element(C).AlignRight().Text(M(l.MontantHT));
                        }
                    });

                    col.Item().Height(8);

                    // Totaux
                    col.Item().AlignRight().Width(240).Column(c =>
                    {
                        void Ligne(string libelle, string valeur, bool gras = false)
                            => c.Item().BorderBottom(0.3f).BorderColor(GrisBordure).PaddingVertical(4).Row(r =>
                            {
                                var t1 = r.RelativeItem().AlignRight().Text(libelle);
                                r.ConstantItem(10);
                                var t2 = r.ConstantItem(110).AlignRight().Text(valeur);
                                if (gras) { t1.Bold(); t2.Bold(); }
                            });

                        if (d.RemiseGlobale > 0)
                        {
                            Ligne("Sous-total HT", M(d.SousTotalHT));
                            Ligne($"Remise globale ({d.RemiseGlobalePourcent:0.##} %)", $"- {M(d.RemiseGlobale)}");
                        }
                        Ligne("Total HT", M(d.TotalHT), gras: true);
                        Ligne("TVA", M(d.TotalTVA));

                        c.Item().Background(Vert).Padding(8).Row(r =>
                        {
                            r.RelativeItem().AlignRight().Text("TOTAL TTC").Bold().FontColor(Colors.White).FontSize(11);
                            r.ConstantItem(10);
                            r.ConstantItem(110).AlignRight().Text(M(d.TotalTTC)).Bold().FontColor(Colors.White).FontSize(12);
                        });
                    });

                    // Conditions / notes
                    if (!string.IsNullOrWhiteSpace(d.Conditions) || !string.IsNullOrWhiteSpace(e?.BanqueNom))
                    {
                        col.Item().Height(10);
                        col.Item().Background(GrisLeger).Padding(8).Column(c =>
                        {
                            c.Item().Text("Conditions").FontSize(7.5f).Bold().FontColor(GrisSoft);
                            if (!string.IsNullOrWhiteSpace(d.Conditions)) c.Item().Text(d.Conditions!);
                            if (!string.IsNullOrWhiteSpace(e?.BanqueNom))
                                c.Item().Text($"Règlement : {e!.BanqueNom}{(string.IsNullOrWhiteSpace(e.BanqueBIC) ? "" : $" — {e.BanqueBIC}")}").FontSize(8);
                        });
                    }
                    if (!string.IsNullOrWhiteSpace(d.Notes))
                    {
                        col.Item().Height(6);
                        col.Item().Background(GrisLeger).Padding(8).Column(c =>
                        {
                            c.Item().Text("Notes").FontSize(7.5f).Bold().FontColor(GrisSoft);
                            c.Item().Text(d.Notes!);
                        });
                    }

                    // Bon pour accord
                    col.Item().Height(14);
                    col.Item().AlignRight().Width(240).Border(0.5f).BorderColor(GrisBordure).Padding(8).Column(c =>
                    {
                        c.Item().Text("Bon pour accord").Bold();
                        c.Item().Text("Date, signature et cachet du client").FontSize(7.5f).FontColor(GrisSoft);
                        c.Item().Height(45);
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.DefaultTextStyle(x => x.FontSize(7).FontColor(GrisSoft));
                    t.Span($"{nom} — Devis {d.Numero} — Page ");
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                    t.Span(" — Généré par Finama");
                });
            });
        }).GeneratePdf();
    }
}
