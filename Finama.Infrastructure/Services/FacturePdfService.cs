using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Finama.Core.DTOs;

namespace Finama.Infrastructure.Services;

public interface IFacturePdfService
{
    byte[] Generer(FactureDetailDto facture);
}

public class FacturePdfService : IFacturePdfService
{
    private const string BleuFinama = "#185FA5";
    private const string BleuClair = "#E6F1FB";
    private const string GrisTexte = "#374151";
    private const string GrisLeger = "#F9FAFB";
    private const string GrisBordure = "#E5E7EB";
    private const string GrisSoft = "#6B7280";

    public byte[] Generer(FactureDetailDto f)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(15, Unit.Millimetre);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(GrisTexte));

                page.Content().Column(col =>
                {
                    // ── En-tête ───────────────────────────────────────────
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(f.EntrepriseNom)
                                .FontSize(18).Bold().FontColor(BleuFinama);
                            c.Item().Text(f.EntrepriseAdresse ?? "").FontSize(8).FontColor(GrisSoft);
                            c.Item().Text($"NIF : {f.EntrepriseNumeroFiscal}  |  {f.EntrepriseTelephone}")
                                .FontSize(8).FontColor(GrisSoft);
                            c.Item().Text(f.EntrepriseEmail).FontSize(8).FontColor(GrisSoft);
                        });
                        row.RelativeItem().AlignRight().Column(c =>
                        {
                            c.Item().Text("FACTURE").FontSize(26).Bold().FontColor(BleuFinama);
                            c.Item().Text($"N° {f.Numero}").FontSize(10);
                            c.Item().Text($"Date : {f.DateFacture:dd MMMM yyyy}").FontSize(9).FontColor(GrisSoft);
                            if (f.DateEcheance.HasValue)
                                c.Item().Text($"Échéance : {f.DateEcheance:dd MMMM yyyy}")
                                    .FontSize(9).FontColor(GrisSoft);
                        });
                    });

                    col.Item().PaddingVertical(5).LineHorizontal(1).LineColor(BleuClair);

                    // ── Bloc client ───────────────────────────────────────
                    col.Item().Background(BleuFinama).Padding(5)
                        .Text("  FACTURÉ À").FontColor(Colors.White).Bold().FontSize(8);

                    col.Item().Background(BleuClair).Padding(8).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(f.TiersNom).Bold().FontSize(10);
                            if (!string.IsNullOrEmpty(f.TiersAdresse))
                                c.Item().Text(f.TiersAdresse).FontSize(8).FontColor(GrisSoft);
                            if (!string.IsNullOrEmpty(f.TiersNumeroFiscal))
                                c.Item().Text($"NIF : {f.TiersNumeroFiscal}  |  {f.TiersTelephone}")
                                    .FontSize(8).FontColor(GrisSoft);
                        });
                        row.ConstantItem(80).AlignRight()
                            .Text(f.TiersCode).FontSize(8).FontColor(GrisSoft);
                    });

                    col.Item().Height(8);

                    // ── Tableau des lignes ────────────────────────────────
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(4);
                            cols.RelativeColumn(1);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(1);
                            cols.RelativeColumn(2);
                        });

                        static IContainer HeaderCell(IContainer c) =>
                            c.Background(BleuFinama).Padding(6)
                             .DefaultTextStyle(x => x.FontColor(Colors.White).Bold().FontSize(8.5f));

                        table.Header(h =>
                        {
                            h.Cell().Element(HeaderCell).Text("Description");
                            h.Cell().Element(HeaderCell).AlignCenter().Text("Qté");
                            h.Cell().Element(HeaderCell).AlignRight().Text("Prix unit. HT");
                            h.Cell().Element(HeaderCell).AlignRight().Text("TVA");
                            h.Cell().Element(HeaderCell).AlignRight().Text("Total HT");
                        });

                        var estPair = false;
                        foreach (var ligne in f.Lignes)
                        {
                            estPair = !estPair;
                            var bg = estPair ? Colors.White.ToString() : GrisLeger;

                            IContainer Cell(IContainer c) =>
                                c.Background(bg).BorderBottom(0.3f).BorderColor(GrisBordure)
                                 .PaddingVertical(6).PaddingHorizontal(6);

                            table.Cell().Element(Cell).Text(ligne.Description).FontSize(8.5f);
                            table.Cell().Element(Cell).AlignRight()
                                .Text(ligne.Quantite.ToString("N2")).FontSize(8.5f);
                            table.Cell().Element(Cell).AlignRight()
                                .Text(FormatMontant(ligne.PrixUnitaireHT, f.EntrepriseDeviseSymbole)).FontSize(8.5f);
                            table.Cell().Element(Cell).AlignRight()
                                .Text($"{ligne.TauxTVA:N0}%").FontSize(8.5f);
                            table.Cell().Element(Cell).AlignRight()
                                .Text(FormatMontant(ligne.MontantHT, f.EntrepriseDeviseSymbole)).FontSize(8.5f);
                        }
                    });

                    col.Item().Height(6);

                    // ── Totaux ────────────────────────────────────────────
                    col.Item().AlignRight().Width(230).Column(c =>
                    {
                        LigneTotaux(c, "Sous-total HT", FormatMontant(f.TotalHT, f.EntrepriseDeviseSymbole));
                        LigneTotaux(c, $"TVA ({f.Lignes.FirstOrDefault()?.TauxTVA:N0}%)",
                            FormatMontant(f.TotalTVA, f.EntrepriseDeviseSymbole));

                        if (f.MontantRegle > 0)
                            LigneTotaux(c, "Déjà réglé",
                                $"- {FormatMontant(f.MontantRegle, f.EntrepriseDeviseSymbole)}");

                        // Grand total TTC
                        c.Item().Background(BleuFinama).Padding(8).Row(r =>
                        {
                            r.RelativeItem().AlignRight()
                                .Text("TOTAL TTC").Bold().FontColor(Colors.White).FontSize(11);
                            r.ConstantItem(10);
                            r.ConstantItem(110).AlignRight()
                                .Text(FormatMontant(f.TotalTTC, f.EntrepriseDeviseSymbole))
                                .Bold().FontColor(Colors.White).FontSize(12);
                        });

                        if (f.Solde > 0 && f.MontantRegle > 0)
                        {
                            c.Item().Background("#FEF3C7").Padding(6).Row(r =>
                            {
                                r.RelativeItem().AlignRight()
                                    .Text("Reste à payer").Bold().FontColor("#92400E").FontSize(9);
                                r.ConstantItem(10);
                                r.ConstantItem(110).AlignRight()
                                    .Text(FormatMontant(f.Solde, f.EntrepriseDeviseSymbole))
                                    .Bold().FontColor("#92400E").FontSize(10);
                            });
                        }
                    });

                    col.Item().Height(8);

                    // ── Pied informations paiement ────────────────────────
                    var afficherPaiement = !string.IsNullOrWhiteSpace(f.EntrepriseBanqueNom)
                                || !string.IsNullOrWhiteSpace(f.EntrepriseBanqueBIC);

                    if (afficherPaiement)
                    {
                        col.Item().Background(GrisLeger).Border(0.5f).BorderColor(GrisBordure)
                            .Padding(8).Row(row =>
                            {
                                row.RelativeItem().Column(c =>
                                {
                                    c.Item().Text("Mode de paiement").FontSize(7.5f).FontColor(GrisSoft);
                                    c.Item().Text("Virement bancaire / Mobile Money").FontSize(8.5f);
                                });

                                row.ConstantItem(1).Background(GrisBordure);
                                row.ConstantItem(10);

                                row.RelativeItem().Column(c =>
                                {
                                    c.Item().Text("Détails de règlement").FontSize(7.5f).FontColor(GrisSoft);

                                    if (!string.IsNullOrWhiteSpace(f.EntrepriseBanqueNom))
                                        c.Item().Text(f.EntrepriseBanqueNom).FontSize(8.5f);

                                    if (!string.IsNullOrWhiteSpace(f.EntrepriseBanqueBIC))
                                        c.Item().Text($"Référence / Compte : {f.EntrepriseBanqueBIC}").FontSize(8.5f);
                                });
                            });
                    }

                    if (!string.IsNullOrEmpty(f.Notes))
                    {
                        col.Item().Height(5);
                        col.Item().Background(GrisLeger).Padding(8).Column(c =>
                        {
                            c.Item().Text("Notes").FontSize(7.5f).FontColor(GrisSoft);
                            c.Item().Text(f.Notes!).FontSize(8.5f);
                        });
                    }
                });

                // ── Pied de page ──────────────────────────────────────────
                page.Footer().AlignCenter().Column(c =>
                {
                    c.Item().LineHorizontal(0.5f).LineColor(GrisBordure);
                    c.Item().Height(4);
                    c.Item().Text(
                        $"{f.EntrepriseNom}  —  {f.EntrepriseAdresse}  —  NIF : {f.EntrepriseNumeroFiscal}  —  Généré par Finama")
                        .FontSize(7).FontColor(GrisSoft);
                });
            });
        }).GeneratePdf();
    }

    private static void LigneTotaux(ColumnDescriptor col, string label, string valeur)
    {
        col.Item().BorderBottom(0.3f).BorderColor(GrisBordure).PaddingVertical(4).Row(r =>
        {
            r.RelativeItem().AlignRight().Text(label).FontSize(9).FontColor(GrisTexte);
            r.ConstantItem(10);
            r.ConstantItem(110).AlignRight().Text(valeur).FontSize(9).FontColor(GrisTexte);
        });
    }

    private static string FormatMontant(decimal montant, string symbole)
        => $"{montant:N0} {symbole}";
}