using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text;
using GeneradorQrAutos.Models;
using QRCoder;

namespace GeneradorQrAutos.Services;

public sealed class VehicleQrService
{
    private static readonly CultureInfo Mx = CultureInfo.GetCultureInfo("es-MX");

    public string BuildPayload(VehicleRecord vehicle)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"{Brand.Nombre.ToUpper(Mx)} - {Brand.Ubicacion}");
        builder.AppendLine($"Marca: {vehicle.Marca}");
        builder.AppendLine($"Modelo: {vehicle.Modelo}");
        builder.AppendLine($"Año: {vehicle.Anio}");
        builder.AppendLine($"Kilometraje: {vehicle.Kilometraje.ToString("N0", Mx)} km");
        builder.AppendLine($"Precio: {vehicle.Precio.ToString("C2", Mx)} MXN");
        builder.AppendLine($"Procedencia: {vehicle.Procedencia}");
        builder.AppendLine($"Motor: {vehicle.MotorLitros}");
        builder.AppendLine($"Configuración del motor: {vehicle.ConfiguracionMotor}");

        if (!string.IsNullOrWhiteSpace(vehicle.Observaciones))
        {
            builder.AppendLine($"Observaciones: {vehicle.Observaciones}");
        }

        builder.Append($"Generado: {vehicle.CreadoEn.ToString("dd/MM/yyyy HH:mm", Mx)}");
        return builder.ToString();
    }

    /// <summary>
    /// Genera el QR. Si se pasa un logotipo se coloca al centro y se usa corrección de errores alta (H, ~30 %)
    /// para que el código siga leyéndose aunque el logo tape una parte.
    /// </summary>
    public Bitmap BuildImage(string payload, int pixelsPerModule = 10, Image? logo = null)
    {
        var ecc = logo is null ? QRCodeGenerator.ECCLevel.M : QRCodeGenerator.ECCLevel.H;
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, ecc);
        using var qrCode = new QRCode(data);
        var bitmap = qrCode.GetGraphic(pixelsPerModule, Brand.Ink, Color.White, drawQuietZones: true);

        if (logo is not null)
        {
            DrawCenterLogo(bitmap, logo, pixelsPerModule);
        }

        return bitmap;
    }

    private static void DrawCenterLogo(Bitmap qr, Image logo, int pixelsPerModule)
    {
        using var g = Graphics.FromImage(qr);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var box = qr.Width * 0.22f;
        var rect = new RectangleF((qr.Width - box) / 2, (qr.Height - box) / 2, box, box);
        using (var path = Brand.RoundedRect(rect, pixelsPerModule * 1.5f))
        {
            g.FillPath(Brushes.White, path);
        }

        var inset = pixelsPerModule * 0.5f;
        g.DrawImage(logo, RectangleF.Inflate(rect, -inset, -inset));
    }
}
