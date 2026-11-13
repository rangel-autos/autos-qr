using GeneradorQr.Core.Formatting;
using SkiaSharp;

namespace GeneradorQr.Core.Rendering;

/// <summary>
/// Ficha imprimible tamaño carta con el logotipo, la leyenda "ESCANÉAME" y el QR.
/// No lleva datos del auto: todo se lee al escanear el código.
/// Las medidas son relativas al ancho de la página (u = ancho / 100), igual que en la versión WinForms.
/// </summary>
public static class SheetRenderer
{
    private const float LetterWidthPt = 612;
    private const float LetterHeightPt = 792;

    public static byte[] RenderPng(SKBitmap qr, SKBitmap? logo, int dpi = 200)
    {
        var width = (int)(8.5 * dpi);
        var height = 11 * dpi;
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);
        Draw(canvas, SKRect.Create(0, 0, width, height), qr, logo);
        canvas.Flush();

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    public static byte[] RenderPdf(SKBitmap qr, SKBitmap? logo)
    {
        // El logo original es muy grande para un PDF que se manda por WhatsApp; ~300 dpi a su tamaño impreso basta.
        using var pdfLogo = logo is null ? null : Downscale(logo, 500);
        logo = pdfLogo;

        using var stream = new MemoryStream();
        using (var document = SKDocument.CreatePdf(stream))
        {
            var canvas = document.BeginPage(LetterWidthPt, LetterHeightPt);
            canvas.Clear(SKColors.White);
            Draw(canvas, SKRect.Create(0, 0, LetterWidthPt, LetterHeightPt), qr, logo);
            document.EndPage();
            document.Close();
        }

        return stream.ToArray();
    }

    private static SKBitmap Downscale(SKBitmap source, int maxSide)
    {
        var scale = Math.Min(1f, (float)maxSide / Math.Max(source.Width, source.Height));
        var info = new SKImageInfo(
            Math.Max(1, (int)(source.Width * scale)),
            Math.Max(1, (int)(source.Height * scale)),
            SKColorType.Rgba8888,
            SKAlphaType.Premul);
        return source.Resize(info, SKFilterQuality.High) ?? source.Copy();
    }

    private static void Draw(SKCanvas g, SKRect page, SKBitmap qr, SKBitmap? logo)
    {
        var mx = VehicleText.Mx;
        var u = page.Width / 100f;
        var width = page.Width - 14 * u;
        var y = page.Top;

        using var fill = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };

        // Franja superior negra con línea roja.
        fill.Color = Brand.Ink;
        g.DrawRect(page.Left, y, page.Width, 2.2f * u, fill);
        fill.Color = Brand.Red;
        g.DrawRect(page.Left, y + 2.2f * u, page.Width, 0.8f * u, fill);
        y += 6 * u;

        using var imagePaint = new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.High };
        if (logo is not null)
        {
            var logoHeight = 17 * u;
            var logoWidth = logoHeight * logo.Width / logo.Height;
            g.DrawBitmap(logo, SKRect.Create(page.Left + (page.Width - logoWidth) / 2, y, logoWidth, logoHeight), imagePaint);
            y += logoHeight + 2.5f * u;
        }

        // Pie de página.
        var footerHeight = 6.5f * u;
        var footerTop = page.Bottom - footerHeight;
        var bottom = footerTop - 3 * u;

        // Bloque central: "ESCANÉAME" en píldora roja, el QR y la leyenda, centrado en el espacio libre.
        const string headline = "ESCANÉAME";
        var headlineSize = SheetText.Fit(headline, SheetText.Black, 8f * u, width * 0.8f);
        var headlineHeight = SheetText.LineHeight(SheetText.Black, headlineSize);
        var pillWidth = SheetText.Measure(headline, SheetText.Black, headlineSize) + 10 * u;
        var pillHeight = headlineHeight + 2.5f * u;
        var pillGap = 5 * u;
        var captionGap = 3.5f * u;
        var captionHeight = 9 * u;

        var available = bottom - y;
        var qrSize = Math.Max(10 * u, Math.Min(62 * u, available - pillHeight - pillGap - captionGap - captionHeight));
        var blockHeight = pillHeight + pillGap + qrSize + captionGap + captionHeight;
        y += Math.Max(0, (available - blockHeight) / 2);

        var pill = SKRect.Create(page.MidX - pillWidth / 2, y, pillWidth, pillHeight);
        fill.Color = Brand.Red;
        g.DrawRoundRect(pill, pillHeight / 2, pillHeight / 2, fill);
        SheetText.DrawCentered(g, headline, pill.MidX, pill.MidY - headlineHeight / 2, SheetText.Black, headlineSize, SKColors.White);
        y += pillHeight + pillGap;

        var qrRect = SKRect.Create(page.MidX - qrSize / 2, y, qrSize, qrSize);
        using (var qrPaint = new SKPaint { IsAntialias = false, FilterQuality = SKFilterQuality.None })
        {
            g.DrawBitmap(qr, qrRect, qrPaint);
        }

        using (var frame = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 0.3f * u, Color = Brand.Border })
        {
            var frameRect = SKRect.Inflate(qrRect, 1.2f * u, 1.2f * u);
            g.DrawRoundRect(frameRect, 2 * u, 2 * u, frame);
        }

        var captionY = qrRect.Bottom + captionGap;
        SheetText.DrawCentered(g, "Escanea el código con la cámara de tu celular", page.MidX, captionY, SheetText.Bold, 3.2f * u, Brand.Ink);
        SheetText.DrawCentered(g, "para ver todos los datos del vehículo", page.MidX, captionY + 4.6f * u, SheetText.Regular, 2.8f * u, Brand.Muted);

        // Pie negro con línea roja.
        fill.Color = Brand.Red;
        g.DrawRect(page.Left, footerTop, page.Width, 0.6f * u, fill);
        fill.Color = Brand.Ink;
        g.DrawRect(page.Left, footerTop + 0.6f * u, page.Width, footerHeight - 0.6f * u, fill);
        var footer = $"{Brand.Nombre.ToUpper(mx)}   ·   {Brand.Ubicacion.ToUpper(mx)}";
        var footerSize = 2.6f * u;
        var footerTextTop = footerTop + 0.6f * u + (footerHeight - 0.6f * u - SheetText.LineHeight(SheetText.Bold, footerSize)) / 2;
        SheetText.DrawCentered(g, footer, page.MidX, footerTextTop, SheetText.Bold, footerSize, SKColors.White);
    }
}
