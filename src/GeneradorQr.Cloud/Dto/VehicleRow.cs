using System.Text.Json.Serialization;
using GeneradorQr.Core.Models;

namespace GeneradorQr.Cloud.Dto;

/// <summary>Fila tal como la devuelve PostgREST (snake_case).</summary>
internal sealed record VehicleRow(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("marca")] string Marca,
    [property: JsonPropertyName("modelo")] string Modelo,
    [property: JsonPropertyName("anio")] int Anio,
    [property: JsonPropertyName("kilometraje")] long Kilometraje,
    [property: JsonPropertyName("precio")] decimal Precio,
    [property: JsonPropertyName("procedencia")] string Procedencia,
    [property: JsonPropertyName("motor_litros")] string MotorLitros,
    [property: JsonPropertyName("configuracion_motor")] string ConfiguracionMotor,
    [property: JsonPropertyName("observaciones")] string? Observaciones,
    [property: JsonPropertyName("vendido")] bool Vendido,
    [property: JsonPropertyName("creado_en")] DateTimeOffset CreadoEn,
    [property: JsonPropertyName("actualizado_en")] DateTimeOffset? ActualizadoEn,
    [property: JsonPropertyName("version")] int Version)
{
    public VehicleRecord ToRecord() => new()
    {
        Id = Id,
        Marca = Marca,
        Modelo = Modelo,
        Anio = Anio,
        Kilometraje = Kilometraje,
        Precio = Precio,
        Procedencia = Procedencia,
        MotorLitros = MotorLitros,
        ConfiguracionMotor = ConfiguracionMotor,
        Observaciones = Observaciones ?? string.Empty,
        Vendido = Vendido,
        CreadoEn = CreadoEn.LocalDateTime,
        ActualizadoEn = ActualizadoEn?.LocalDateTime,
        Version = Version
    };
}

/// <summary>
/// Campos que la app escribe. Id, versión y fechas los pone el servidor
/// (excepto <see cref="CreadoEn"/> al migrar datos existentes).
/// </summary>
internal sealed record VehicleWrite(
    [property: JsonPropertyName("marca")] string Marca,
    [property: JsonPropertyName("modelo")] string Modelo,
    [property: JsonPropertyName("anio")] int Anio,
    [property: JsonPropertyName("kilometraje")] long Kilometraje,
    [property: JsonPropertyName("precio")] decimal Precio,
    [property: JsonPropertyName("procedencia")] string Procedencia,
    [property: JsonPropertyName("motor_litros")] string MotorLitros,
    [property: JsonPropertyName("configuracion_motor")] string ConfiguracionMotor,
    [property: JsonPropertyName("observaciones")] string Observaciones,
    [property: JsonPropertyName("vendido")] bool Vendido,
    [property: JsonPropertyName("creado_en"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? CreadoEn)
{
    public static VehicleWrite From(VehicleRecord v, bool includeCreatedAt = false) => new(
        v.Marca, v.Modelo, v.Anio, v.Kilometraje, v.Precio, v.Procedencia, v.MotorLitros, v.ConfiguracionMotor,
        v.Observaciones, v.Vendido,
        // Una fecha sin zona (Unspecified) se interpreta como hora local, que es como la guardaba la app WinForms.
        includeCreatedAt ? new DateTimeOffset(v.CreadoEn) : null);
}
