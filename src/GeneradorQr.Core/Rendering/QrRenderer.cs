using QRCoder;
using SkiaSharp;

namespace GeneradorQr.Core.Rendering;

public static class QrRenderer
{
    /// <summary>
    /// Genera el QR. Con logotipo se coloca al centro y se usa corrección de errores alta (H, ~30 %)
    /// para que se siga leyendo aunque el logo tape una parte. Quien llama libera el bitmap.
    /// </summary>
    public static SKBitmap Render(string payload, int pixelsPerModule = 10, SKBitmap? logo = null)
    {
        var ecc = logo is null ? QRCodeGenerator.ECCLevel.M : QRCodeGenerator.ECCLevel.H;
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, ecc, forceUtf8: true);

        // ModuleMatrix ya incluye la zona de silencio (4 módulos por lado).
        var matrix = data.ModuleMatrix;
        var modules = matrix.Count;
        var size = modules * pixelsPerModule;

        var bitmap = new SKBitmap(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        using (var ink = new SKPaint { Color = Brand.Ink, IsAntialias = false, Style = SKPaintStyle.Fill })
        {
            for (var y = 0; y < modules; y++)
            {
                var row = matrix[y];
                for (var x = 0; x < modules; x++)
                {
                    if (row[x])
                    {
                        canvas.DrawRect(x * pixelsPerModule, y * pixelsPerModule, pixelsPerModule, pixelsPerModule, ink);
                    }
                }
            }
        }

        if (logo is not null)
        {
            DrawCenterLogo(canvas, size, logo, pixelsPerModule);
        }

        canvas.Flush();
        return bitmap;
    }

    public static byte[] EncodePng(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static void DrawCenterLogo(SKCanvas canvas, int size, SKBitmap logo, int pixelsPerModule)
    {
        var box = size * 0.22f;
        var rect = SKRect.Create((size - box) / 2, (size - box) / 2, box, box);
        var radius = pixelsPerModule * 1.5f;

        using (var white = new SKPaint { Color = SKColors.White, IsAntialias = true })
        {
            canvas.DrawRoundRect(rect, radius, radius, white);
        }

        var inset = pixelsPerModule * 0.5f;
        var target = SKRect.Inflate(rect, -inset, -inset);
        using var paint = new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.High };
        canvas.DrawBitmap(logo, Fit(logo, target), paint);
    }

    /// <summary>Encaja la imagen dentro del rectángulo sin deformarla.</summary>
    internal static SKRect Fit(SKBitmap image, SKRect target)
    {
        var scale = Math.Min(target.Width / image.Width, target.Height / image.Height);
        var w = image.Width * scale;
        var h = image.Height * scale;
        return SKRect.Create(target.MidX - w / 2, target.MidY - h / 2, w, h);
    }
}
