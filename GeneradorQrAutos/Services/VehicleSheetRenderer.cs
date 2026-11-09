using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using GeneradorQrAutos.Models;

namespace GeneradorQrAutos.Services;

/// <summary>
/// Dibuja la ficha imprimible (tamaño carta) con logotipo, datos principales y el código QR.
/// Todas las medidas son relativas al ancho de la página, así que funciona igual para PNG o impresora.
/// </summary>
public static class VehicleSheetRenderer
{
    private static readonly CultureInfo Mx = CultureInfo.GetCultureInfo("es-MX");
    private static readonly FontFamily Regular = new("Segoe UI");
    private static readonly FontFamily? Black = TryFamily("Segoe UI Black");

    public static Bitmap RenderBitmap(VehicleRecord vehicle, Image qr, Image? logo, int dpi = 200)
    {
        var width = (int)(8.5 * dpi);
        var height = 11 * dpi;
        var bitmap = new Bitmap(width, height);
        bitmap.SetResolution(dpi, dpi);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(Color.White);
        Draw(g, new RectangleF(0, 0, width, height), vehicle, qr, logo);
        return bitmap;
    }

    public static void Draw(Graphics g, RectangleF page, VehicleRecord v, Image qr, Image? logo)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var u = page.Width / 100f;
        var left = page.Left + 7 * u;
        var width = page.Width - 14 * u;
        var y = page.Top;

        using var redBrush = new SolidBrush(Brand.Red);
        using var inkBrush = new SolidBrush(Brand.Ink);
        using var mutedBrush = new SolidBrush(Brand.Muted);
        using var softBrush = new SolidBrush(Brand.Surface);
        using var centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near };
        using var centeredBoth = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        // Franja superior negra con línea roja.
        g.FillRectangle(inkBrush, page.Left, y, page.Width, 2.2f * u);
        g.FillRectangle(redBrush, page.Left, y + 2.2f * u, page.Width, 0.8f * u);
        y += 6 * u;

        if (logo is not null)
        {
            var logoHeight = 17 * u;
            var logoWidth = logoHeight * logo.Width / logo.Height;
            g.DrawImage(logo, page.Left + (page.Width - logoWidth) / 2, y, logoWidth, logoHeight);
            y += logoHeight + 2.5f * u;
        }

        // Marca y modelo.
        var title = v.Titulo.ToUpper(Mx);
        using (var font = FitFont(g, title, size => Heavy(size), 8.5f * u, width))
        {
            var size = g.MeasureString(title, font);
            g.DrawString(title, font, inkBrush, new RectangleF(page.Left, y, page.Width, size.Height), centered);
            y += size.Height;
        }

        var subtitle = $"{v.Anio}   ·   {v.Kilometraje.ToString("N0", Mx)} km";
        using (var font = new Font(Regular, 3.6f * u, FontStyle.Regular, GraphicsUnit.World))
        {
            var size = g.MeasureString(subtitle, font);
            g.DrawString(subtitle, font, mutedBrush, new RectangleF(page.Left, y, page.Width, size.Height), centered);
            y += size.Height + 1.5f * u;
        }

        // Precio en una píldora roja.
        var price = $"{v.Precio.ToString("C0", Mx)} MXN";
        using (var font = FitFont(g, price, size => Heavy(size), 8f * u, width * 0.8f))
        {
            var size = g.MeasureString(price, font);
            var pillWidth = size.Width + 8 * u;
            var pillHeight = size.Height + 1.5f * u;
            var pill = new RectangleF(page.Left + (page.Width - pillWidth) / 2, y, pillWidth, pillHeight);
            using (var path = Brand.RoundedRect(pill, pillHeight / 2))
            {
                g.FillPath(redBrush, path);
            }
            g.DrawString(price, font, Brushes.White, pill, centeredBoth);
            y += pillHeight + 4 * u;
        }

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
            ("KILOMETRAJE", $"{v.Kilometraje.ToString("N0", Mx)} km"),
            ("PROCEDENCIA", v.Procedencia),
            ("MOTOR", v.MotorLitros),
            ("CONFIGURACIÓN", v.ConfiguracionMotor)
        };

        var rowHeight = 8.5f * u;
        var rowGap = 1.6f * u;
        var specY = y;
        using (var labelFont = new Font(Regular, 2.1f * u, FontStyle.Bold, GraphicsUnit.World))
        {
            foreach (var (label, value) in specs)
            {
                var row = new RectangleF(left, specY, leftWidth, rowHeight);
                using (var path = Brand.RoundedRect(row, 1.2f * u))
                {
                    g.FillPath(softBrush, path);
                }
                g.FillRectangle(redBrush, row.Left, row.Top + 1.6f * u, 0.7f * u, row.Height - 3.2f * u);
                g.DrawString(label, labelFont, mutedBrush, row.Left + 3 * u, row.Top + 1.2f * u);

                using var valueFont = FitFont(g, value, size => new Font(Regular, size, FontStyle.Bold, GraphicsUnit.World), 3.6f * u, leftWidth - 5 * u);
                g.DrawString(value, valueFont, inkBrush, row.Left + 3 * u, row.Top + 3.7f * u);
                specY += rowHeight + rowGap;
            }

            if (!string.IsNullOrWhiteSpace(v.Observaciones) && bottom - specY > 8 * u)
            {
                g.DrawString("OBSERVACIONES", labelFont, mutedBrush, left, specY + 1 * u);
                using var notesFont = new Font(Regular, 2.7f * u, FontStyle.Regular, GraphicsUnit.World);
                using var wrap = new StringFormat { Trimming = StringTrimming.EllipsisWord };
                var notesRect = new RectangleF(left, specY + 4 * u, leftWidth, bottom - (specY + 4 * u));
                g.DrawString(v.Observaciones, notesFont, inkBrush, notesRect, wrap);
            }
        }

        // QR con leyenda.
        var captionHeight = 7 * u;
        var qrSize = Math.Max(10 * u, Math.Min(rightWidth, bottom - y - captionHeight));
        var qrRect = new RectangleF(rightX + (rightWidth - qrSize) / 2, y, qrSize, qrSize);
        g.DrawImage(qr, qrRect);
        using (var framePen = new Pen(Brand.Border, 0.3f * u))
        using (var framePath = Brand.RoundedRect(RectangleF.Inflate(qrRect, 0.8f * u, 0.8f * u), 1.5f * u))
        {
            g.DrawPath(framePen, framePath);
        }

        using (var captionFont = new Font(Regular, 2.8f * u, FontStyle.Bold, GraphicsUnit.World))
        using (var captionSmall = new Font(Regular, 2.3f * u, FontStyle.Regular, GraphicsUnit.World))
        {
            var captionY = qrRect.Bottom + 1.8f * u;
            g.DrawString("Escanea con tu celular", captionFont, inkBrush,
                new RectangleF(rightX, captionY, rightWidth, 4 * u), centered);
            g.DrawString("para ver todos los datos", captionSmall, mutedBrush,
                new RectangleF(rightX, captionY + 3.6f * u, rightWidth, 4 * u), centered);
        }

        // Pie negro con línea roja.
        g.FillRectangle(redBrush, page.Left, footerTop, page.Width, 0.6f * u);
        g.FillRectangle(inkBrush, page.Left, footerTop + 0.6f * u, page.Width, footerHeight - 0.6f * u);
        using (var footerFont = new Font(Regular, 2.6f * u, FontStyle.Bold, GraphicsUnit.World))
        {
            var footer = $"{Brand.Nombre.ToUpper(Mx)}   ·   {Brand.Ubicacion.ToUpper(Mx)}";
            g.DrawString(footer, footerFont, Brushes.White,
                new RectangleF(page.Left, footerTop + 0.6f * u, page.Width, footerHeight - 0.6f * u), centeredBoth);
        }

        if (v.Vendido)
        {
            DrawSoldStamp(g, page, u);
        }
    }

    private static void DrawSoldStamp(Graphics g, RectangleF page, float u)
    {
        const string text = "VENDIDO";
        var state = g.Save();
        g.TranslateTransform(page.Left + page.Width / 2, page.Top + page.Height * 0.48f);
        g.RotateTransform(-20);

        using var font = Heavy(15 * u);
        var size = g.MeasureString(text, font);
        var rect = new RectangleF(-size.Width / 2 - 4 * u, -size.Height / 2 - 1 * u, size.Width + 8 * u, size.Height + 2 * u);

        using (var background = new SolidBrush(Color.FromArgb(225, Color.White)))
        using (var path = Brand.RoundedRect(rect, 2 * u))
        using (var pen = new Pen(Color.FromArgb(230, Brand.Red), 1.2f * u))
        {
            g.FillPath(background, path);
            g.DrawPath(pen, path);
        }

        using (var brush = new SolidBrush(Color.FromArgb(235, Brand.Red)))
        using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
        {
            g.DrawString(text, font, brush, rect, format);
        }

        g.Restore(state);
    }

    private static Font Heavy(float size) => Black is not null
        ? new Font(Black, size, FontStyle.Regular, GraphicsUnit.World)
        : new Font(Regular, size, FontStyle.Bold, GraphicsUnit.World);

    /// <summary>Reduce el tamaño de letra hasta que el texto quepa en el ancho indicado.</summary>
    private static Font FitFont(Graphics g, string text, Func<float, Font> create, float size, float maxWidth)
    {
        while (true)
        {
            var font = create(size);
            if (size <= 1 || g.MeasureString(text, font).Width <= maxWidth)
            {
                return font;
            }

            font.Dispose();
            size *= 0.92f;
        }
    }

    private static FontFamily? TryFamily(string name)
    {
        try
        {
            return new FontFamily(name);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
