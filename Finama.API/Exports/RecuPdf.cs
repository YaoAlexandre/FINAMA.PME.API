using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Finama.API.Exports;

/// <summary>Reçu de caisse en PDF, au format ticket (80 mm, hauteur selon le contenu), pour WhatsApp ou e-mail.</summary>
public static class RecuPdf
{
    public sealed record Donnees(
        string Entreprise, byte[]? Logo, string? EntrepriseAdresse, string? EntrepriseTelephone, string? Nif,
        string Magasin, string? MagasinAdresse, string? MagasinTelephone, string Caisse,
        string NumeroTicket, DateTime DateVente, string? Client, bool EstAnnule,
        IReadOnlyList<Ligne> Lignes, IReadOnlyList<Reglement> Paiements,
        decimal TotalHT, decimal TotalTVA, decimal TotalRemise, decimal TotalTTC, decimal Rendu, string Devise);

    public sealed record Ligne(string Libelle, decimal Quantite, decimal PrixUnitaire, decimal Remise, decimal Montant);
    public sealed record Reglement(string Mode, decimal Montant, string? Reference);

    private static readonly CultureInfo Fr = new("fr-FR");
    private static string M(decimal v, string devise) => (v.ToString("#,##0", Fr) + " " + devise).Replace('\u202F', ' ').Replace('\u00A0', ' ');

    public static byte[] Generer(Donnees d)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(doc => doc.Page(page =>
        {
            page.ContinuousSize(80, Unit.Millimetre);
            page.Margin(4, Unit.Millimetre);
            page.DefaultTextStyle(x => x.FontSize(8).FontColor("#111111"));

            page.Content().Column(c =>
            {
                c.Spacing(2);

                if (d.Logo is not null)
                    c.Item().AlignCenter().Height(16, Unit.Millimetre).Image(d.Logo).FitArea();

                c.Item().AlignCenter().Text(d.Entreprise).Bold().FontSize(11);
                foreach (var info in new[] { d.MagasinAdresse ?? d.EntrepriseAdresse, d.MagasinTelephone ?? d.EntrepriseTelephone, string.IsNullOrWhiteSpace(d.Nif) ? null : $"NIF : {d.Nif}" })
                    if (!string.IsNullOrWhiteSpace(info)) c.Item().AlignCenter().Text(info!).FontSize(7);

                c.Item().PaddingVertical(2).LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);

                c.Item().Row(r =>
                {
                    r.RelativeItem().Text($"Ticket {d.NumeroTicket}").Bold();
                    r.RelativeItem().AlignRight().Text(d.DateVente.ToString("dd/MM/yyyy HH:mm"));
                });
                c.Item().Text($"{d.Magasin} · {d.Caisse}").FontSize(7);
                if (!string.IsNullOrWhiteSpace(d.Client)) c.Item().Text($"Client : {d.Client}").FontSize(7);
                if (d.EstAnnule) c.Item().AlignCenter().Text("*** TICKET ANNULÉ ***").Bold().FontColor(Colors.Red.Darken2);

                c.Item().PaddingVertical(2).LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);

                foreach (var l in d.Lignes)
                {
                    c.Item().Text(l.Libelle).SemiBold();
                    c.Item().Row(r =>
                    {
                        r.RelativeItem().Text($"{l.Quantite.ToString("0.###", Fr)} x {M(l.PrixUnitaire, "")}{(l.Remise > 0 ? $"  (remise -{M(l.Remise, "")})" : "")}").FontSize(7);
                        r.ConstantItem(26, Unit.Millimetre).AlignRight().Text(M(l.Montant, d.Devise));
                    });
                }

                c.Item().PaddingVertical(2).LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);

                void Total(string libelle, string valeur, bool gras = false)
                    => c.Item().Row(r =>
                    {
                        var a = r.RelativeItem().Text(libelle);
                        var b = r.ConstantItem(30, Unit.Millimetre).AlignRight().Text(valeur);
                        if (gras) { a.Bold().FontSize(10); b.Bold().FontSize(10); }
                    });

                if (d.TotalRemise > 0) Total("Remises", "-" + M(d.TotalRemise, d.Devise));
                Total("Total HT", M(d.TotalHT, d.Devise));
                Total("TVA", M(d.TotalTVA, d.Devise));
                Total("TOTAL TTC", M(d.TotalTTC, d.Devise), gras: true);

                c.Item().PaddingTop(3).Text("Règlement").SemiBold();
                foreach (var p in d.Paiements)
                    Total($"{p.Mode}{(string.IsNullOrWhiteSpace(p.Reference) ? "" : $" ({p.Reference})")}", M(p.Montant, d.Devise));
                if (d.Rendu > 0) Total("Rendu monnaie", M(d.Rendu, d.Devise));

                c.Item().PaddingTop(6).AlignCenter().Text("Merci de votre visite !").Bold();
                c.Item().AlignCenter().Text("Reçu émis avec Finama").FontSize(6).FontColor(Colors.Grey.Darken1);
            });
        })).GeneratePdf();
    }
}
