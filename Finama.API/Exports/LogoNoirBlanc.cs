using DocumentFormat.OpenXml.Spreadsheet;
using SkiaSharp;

namespace Finama.API.Exports;

/// <summary>Logo des tickets de caisse : niveaux de gris sur fond blanc (impression thermique noir et blanc).</summary>
public static class LogoNoirBlanc
{
    // Luminance (0,299 R + 0,587 G + 0,114 B) appliquée aux trois couleurs ; la transparence est conservée
    private static readonly float[] MatriceGris =
    [
        0.299f, 0.587f, 0.114f, 0, 0,
        0.299f, 0.587f, 0.114f, 0, 0,
        0.299f, 0.587f, 0.114f, 0, 0,
        0,      0,      0,      1, 0
    ];

    public static byte[]? Convertir(byte[]? logo)
    {
        if (logo is null || logo.Length == 0) return logo;
        try
        {
            using var source = SKBitmap.Decode(logo);
            if (source is null) return logo;

            using var surface = SKSurface.Create(new SKImageInfo(source.Width, source.Height));
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.White);   // fond transparent → blanc
            using var pinceau = new SKPaint { ColorFilter = SKColorFilter.CreateColorMatrix(MatriceGris) };
            canvas.DrawBitmap(source, 0, 0, pinceau);

            using var image = surface.Snapshot();
            using var png = image.Encode(SKEncodedImageFormat.Png, 100);
            return png.ToArray();
        }
        catch { return logo; }   // format non lu : on garde le logo d'origine plutôt que rien
    }
}