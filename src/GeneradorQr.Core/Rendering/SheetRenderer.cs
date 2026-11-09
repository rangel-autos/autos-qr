using GeneradorQr.Core.Formatting;
using GeneradorQr.Core.Models;
using SkiaSharp;

namespace GeneradorQr.Core.Rendering;

/// <summary>
/// Ficha imprimible tamaño carta con logotipo, datos principales y QR.
/// Las medidas son relativas al ancho de la página (u = ancho / 100), igual que en la versión WinForms.
/// </summary>
public static class SheetRenderer
{
    private const float LetterWidthPt = 612;
    private const float LetterHeightPt = 792;

    public static byte[] RenderPng(VehicleRecord vehicle, SKBitmap qr, SKBitmap? logo, int dpi = 200)
    {
        var width = (int)(8.5 * dpi);
        var height = 11 * dpi;
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);
        Draw(canvas, SKRect.Create(0, 0, width, height), vehicle, qr, logo);
        canvas.Flush();

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    public static byte[] RenderPdf(VehicleRecord vehicle, SKBitmap qr, SKBitmap? logo)
    {
        // El logo original es muy grande para un PDF que se manda por WhatsApp; ~300 dpi a su tamaño impreso basta.
        using var pdfLogo = logo is null ? null : Downscale(logo, 500);
        logo = pdfLogo;

        using var stream = new MemoryStream();
        using (var document = SKDocument.CreatePdf(stream))
        {
            var canvas = document.BeginPage(LetterWidthPt, LetterHeightPt);
            canvas.Clear(SKColors.White);
            Draw(canvas, SKRect.Create(0, 0, LetterWidthPt, LetterHeightPt), vehicle, qr, logo);
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

    private static void Draw(SKCanvas g, SKRect page, VehicleRecord v, SKBitmap qr, SKBitmap? logo)
    {
        var mx = VehicleText.Mx;
        var u = page.Width / 100f;
        var left = page.Left + 7 * u;
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

        // Marca y modelo.
        var title = v.Titulo.ToUpper(mx);
        var titleSize = SheetText.Fit(title, SheetText.Black, 8.5f * u, width);
        SheetText.DrawCentered(g, title, page.MidX, y, SheetText.Black, titleSize, Brand.Ink);
        y += SheetText.LineHeight(SheetText.Black, titleSize) * 1.15f;

        var subtitle = $"{v.Anio}   ·   {v.Kilometraje.ToString("N0", mx)} km";
        var subtitleSize = 3.6f * u;
        SheetText.DrawCentered(g, subtitle, page.MidX, y, SheetText.Regular, subtitleSize, Brand.Muted);
        y += SheetText.LineHeight(SheetText.Regular, subtitleSize) * 1.15f + 1.5f * u;

        // Precio en una píldora roja.
        var price = $"{v.Precio.ToString("C0", mx)} MXN";
        var priceSize = SheetText.Fit(price, SheetText.Black, 8f * u, width * 0.8f);
        var priceWidth = SheetText.Measure(price, SheetText.Black, priceSize);
        var priceHeight = SheetText.LineHeight(SheetText.Black, priceSize);
        var pillWidth = priceWidth + 8 * u;
        var pillHeight = priceHeight + 2.5f * u;
        var pill = SKRect.Create(page.Left + (page.Width - pillWidth) / 2, y, pillWidth, pillHeight);
        fill.Color = Brand.Red;
        g.DrawRoundRect(pill, pillHeight / 2, pillHeight / 2, fill);
        SheetText.DrawCentered(g, price, pill.MidX, pill.MidY - priceHeight / 2, SheetText.Black, priceSize, SKColors.White);
        y += pillHeight + 4 * u;

        // Pie de página.
        var footerHeight = 6.5f * u;
        var footerTop = page.Bottom - footerHeight;
        var bottom = footerTop - 3 * u;

        // Columna izquierda: especificaciones. Columna derecha: QR.
        const float columnGap = 5;
        var leftWidth = width * 0.52f;
        var rightX = left + leftWidth + columnGap * u;
        var rightWidth = width - leftWidth - columnGap * u;

        var specs = new (string Label, string Value)[]
        {
            ("KILOMETRAJE", $"{v.Kilometraje.ToString("N0", mx)} km"),
            ("PROCEDENCIA", v.Procedencia),
            ("MOTOR", v.MotorLitros),
            ("CONFIGURACIÓN", v.ConfiguracionMotor)
        };

        var rowHeight = 8.5f * u;
        var rowGap = 1.6f * u;
        var labelSize = 2.1f * u;
        var specY = y;
        foreach (var (label, value) in specs)
        {
            var row = SKRect.Create(left, specY, leftWidth, rowHeight);
            fill.Color = Brand.Surface;
            g.DrawRoundRect(row, 1.2f * u, 1.2f * u, fill);
            fill.Color = Brand.Red;
            g.DrawRect(row.Left, row.Top + 1.6f * u, 0.7f * u, row.Height - 3.2f * u, fill);
            SheetText.Draw(g, label, row.Left + 3 * u, row.Top + 1.2f * u, SheetText.Bold, labelSize, Brand.Muted);

            var valueSize = SheetText.Fit(value, SheetText.Bold, 3.6f * u, leftWidth - 5 * u);
            SheetText.Draw(g, value, row.Left + 3 * u, row.Top + 3.9f * u, SheetText.Bold, valueSize, Brand.Ink);
            specY += rowHeight + rowGap;
        }

        if (!string.IsNullOrWhiteSpace(v.Observaciones) && bottom - specY > 8 * u)
        {
            SheetText.Draw(g, "OBSERVACIONES", left, specY + 1 * u, SheetText.Bold, labelSize, Brand.Muted);
            var notesSize = 2.7f * u;
            var lineHeight = SheetText.LineHeight(SheetText.Regular, notesSize) * 1.1f;
            var notesTop = specY + 4 * u;
            var maxLines = (int)((bottom - notesTop) / lineHeight);
            var lines = SheetText.Wrap(v.Observaciones, SheetText.Regular, notesSize, leftWidth, maxLines);
            foreach (var line in lines)
            {
                SheetText.Draw(g, line, left, notesTop, SheetText.Regular, notesSize, Brand.Ink);
                notesTop += lineHeight;
            }
        }

        // QR con leyenda.
        var captionHeight = 7 * u;
        var qrSize = Math.Max(10 * u, Math.Min(rightWidth, bottom - y - captionHeight));
        var qrRect = SKRect.Create(rightX + (rightWidth - qrSize) / 2, y, qrSize, qrSize);
        using (var qrPaint = new SKPaint { IsAntialias = false, FilterQuality = SKFilterQuality.None })
        {
            g.DrawBitmap(qr, qrRect, qrPaint);
        }

        using (var frame = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 0.3f * u, Color = Brand.Border })
        {
            var frameRect = SKRect.Inflate(qrRect, 0.8f * u, 0.8f * u);
            g.DrawRoundRect(frameRect, 1.5f * u, 1.5f * u, frame);
        }

        var captionY = qrRect.Bottom + 1.8f * u;
        var captionX = rightX + rightWidth / 2;
        SheetText.DrawCentered(g, "Escanea con tu celular", captionX, captionY, SheetText.Bold, 2.8f * u, Brand.Ink);
        SheetText.DrawCentered(g, "para ver todos los datos", captionX, captionY + 3.6f * u, SheetText.Regular, 2.3f * u, Brand.Muted);

        // Pie negro con línea roja.
        fill.Color = Brand.Red;
        g.DrawRect(page.Left, footerTop, page.Width, 0.6f * u, fill);
        fill.Color = Brand.Ink;
        g.DrawRect(page.Left, footerTop + 0.6f * u, page.Width, footerHeight - 0.6f * u, fill);
        var footer = $"{Brand.Nombre.ToUpper(mx)}   ·   {Brand.Ubicacion.ToUpper(mx)}";
        var footerSize = 2.6f * u;
        var footerTextTop = footerTop + 0.6f * u + (footerHeight - 0.6f * u - SheetText.LineHeight(SheetText.Bold, footerSize)) / 2;
        SheetText.DrawCentered(g, footer, page.MidX, footerTextTop, SheetText.Bold, footerSize, SKColors.White);

        if (v.Vendido)
        {
            DrawSoldStamp(g, page, u);
        }
    }

    private static void DrawSoldStamp(SKCanvas g, SKRect page, float u)
    {
        const string text = "VENDIDO";
        g.Save();
        g.Translate(page.MidX, page.Top + page.Height * 0.48f);
        g.RotateDegrees(-20);

        var size = 15 * u;
        var textWidth = SheetText.Measure(text, SheetText.Black, size);
        var textHeight = SheetText.LineHeight(SheetText.Black, size);
        var rect = SKRect.Create(-textWidth / 2 - 4 * u, -textHeight / 2 - 1 * u, textWidth + 8 * u, textHeight + 2 * u);

        using (var background = new SKPaint { IsAntialias = true, Color = SKColors.White.WithAlpha(225) })
        using (var pen = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.2f * u, Color = Brand.Red.WithAlpha(230) })
        {
            g.DrawRoundRect(rect, 2 * u, 2 * u, background);
            g.DrawRoundRect(rect, 2 * u, 2 * u, pen);
        }

        SheetText.DrawCentered(g, text, 0, -textHeight / 2, SheetText.Black, size, Brand.Red.WithAlpha(235));
        g.Restore();
    }
}
