namespace GeneradorQr.Core.Models;

/// <summary>
/// Información que se captura y que también se incluye dentro del código QR.
/// </summary>
public sealed class VehicleRecord
{
    public long Id { get; set; }

    /// <summary>
    /// Código aleatorio que identifica al auto en la ficha pública (va dentro del QR).
    /// No se usa el Id para que nadie pueda recorrer el inventario cambiando un número.
    /// </summary>
    public string Codigo { get; set; } = string.Empty;
    public string Marca { get; set; } = string.Empty;
    public string Modelo { get; set; } = string.Empty;
    public int Anio { get; set; }
    /// <summary>Recorrido del auto, en la unidad que indica <see cref="UnidadDistancia"/>.</summary>
    public long Kilometraje { get; set; }

    /// <summary>"km" o "mi". Ver <see cref="Models.UnidadDistancia"/>.</summary>
    public string UnidadDistancia { get; set; } = Models.UnidadDistancia.Km;
    public decimal Precio { get; set; }
    public string Procedencia { get; set; } = string.Empty;
    public string MotorLitros { get; set; } = string.Empty;
    public string ConfiguracionMotor { get; set; } = string.Empty;
    public string Observaciones { get; set; } = string.Empty;
    public bool Vendido { get; set; }
    public DateTime CreadoEn { get; set; }
    public DateTime? ActualizadoEn { get; set; }

    /// <summary>Versión del registro en el servidor; se usa para detectar ediciones simultáneas.</summary>
    public int Version { get; set; }

    public string Titulo => $"{Marca} {Modelo}".Trim();
    /// <summary>"Kilometraje" o "Millaje", según la unidad.</summary>
    public string EtiquetaRecorrido => Models.UnidadDistancia.Etiqueta(UnidadDistancia);

    /// <summary>"45,000 km" o "45,000 mi".</summary>
    public string Recorrido =>
        $"{Kilometraje.ToString("N0", Formatting.VehicleText.Mx)} {Models.UnidadDistancia.Normalize(UnidadDistancia)}";

    public string Motor => $"{MotorLitros} {ConfiguracionMotor}".Trim();
    public string Estado => Vendido ? "Vendido" : "Disponible";

    public static string NuevoCodigo() => Guid.NewGuid().ToString("N")[..16];

    public VehicleRecord Clone() => (VehicleRecord)MemberwiseClone();
}
