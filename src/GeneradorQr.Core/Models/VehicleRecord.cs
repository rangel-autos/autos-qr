namespace GeneradorQr.Core.Models;

/// <summary>
/// Información que se captura y que también se incluye dentro del código QR.
/// </summary>
public sealed class VehicleRecord
{
    public long Id { get; set; }
    public string Marca { get; set; } = string.Empty;
    public string Modelo { get; set; } = string.Empty;
    public int Anio { get; set; }
    public long Kilometraje { get; set; }
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
    public string Motor => $"{MotorLitros} {ConfiguracionMotor}".Trim();
    public string Estado => Vendido ? "Vendido" : "Disponible";

    public VehicleRecord Clone() => (VehicleRecord)MemberwiseClone();
}
