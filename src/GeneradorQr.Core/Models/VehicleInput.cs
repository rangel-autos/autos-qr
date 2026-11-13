namespace GeneradorQr.Core.Models;

/// <summary>
/// Valores tal como vienen del formulario, antes de validar.
/// </summary>
public sealed record VehicleInput(
    string Marca,
    string Modelo,
    int Anio,
    long Kilometraje,
    string PrecioTexto,
    string Procedencia,
    string MotorLitros,
    string ConfiguracionMotor,
    string Observaciones,
    string UnidadDistancia = Models.UnidadDistancia.Km);
