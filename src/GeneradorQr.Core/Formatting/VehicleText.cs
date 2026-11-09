using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using GeneradorQr.Core.Models;

namespace GeneradorQr.Core.Formatting;

public static class VehicleText
{
    public static readonly CultureInfo Mx = CultureInfo.GetCultureInfo("es-MX");

    private static readonly Regex MotorRegex = new(@"^\s*(\d+(?:[.,]\d+)?)\s*(?:l|lt|lts|litros?)?\.?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Texto que va dentro del QR. Debe ser idéntico al de la versión WinForms.
    /// </summary>
    public static string BuildPayload(VehicleRecord vehicle)
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

    public static bool TryParsePrecio(string text, out decimal precio)
    {
        var clean = text.Trim()
            .Replace("$", string.Empty)
            .Replace(",", string.Empty)
            .Replace("MXN", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" ", string.Empty);
        return decimal.TryParse(clean, NumberStyles.Number, CultureInfo.InvariantCulture, out precio) && precio >= 0;
    }

    /// <summary>"5" → "5.0 L", "2,5 lts" → "2.5 L". Si no parece un litraje, se deja igual.</summary>
    public static string NormalizeMotor(string text)
    {
        var match = MotorRegex.Match(text);
        if (match.Success &&
            decimal.TryParse(match.Groups[1].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var litros))
        {
            return $"{litros.ToString("0.0", CultureInfo.InvariantCulture)} L";
        }

        return text;
    }
}
