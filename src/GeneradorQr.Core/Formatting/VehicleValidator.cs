using GeneradorQr.Core.Models;

namespace GeneradorQr.Core.Formatting;

public sealed record ValidationError(string Field, string Message);

public static class VehicleValidator
{
    /// <summary>
    /// Valida el formulario. Si no hay errores, <paramref name="record"/> trae los datos limpios;
    /// Id, CreadoEn, Vendido y Version los asigna quien llama.
    /// </summary>
    public static IReadOnlyList<ValidationError> Validate(VehicleInput input, out VehicleRecord? record)
    {
        var errors = new List<ValidationError>();
        var marca = input.Marca.Trim();
        var modelo = input.Modelo.Trim();
        var motor = input.MotorLitros.Trim();
        var configuracion = input.ConfiguracionMotor.Trim();

        if (marca.Length == 0) errors.Add(new(nameof(VehicleInput.Marca), "Escribe la marca."));
        if (modelo.Length == 0) errors.Add(new(nameof(VehicleInput.Modelo), "Escribe el modelo."));
        if (!VehicleText.TryParsePrecio(input.PrecioTexto, out var precio))
            errors.Add(new(nameof(VehicleInput.PrecioTexto), "Escribe un precio válido, por ejemplo 495000."));
        if (motor.Length == 0) errors.Add(new(nameof(VehicleInput.MotorLitros), "Escribe el litraje, por ejemplo 2.5 L."));
        if (configuracion.Length == 0)
            errors.Add(new(nameof(VehicleInput.ConfiguracionMotor), "Elige o escribe la configuración, por ejemplo V6."));

        if (errors.Count > 0)
        {
            record = null;
            return errors;
        }

        record = new VehicleRecord
        {
            Marca = marca,
            Modelo = modelo,
            Anio = input.Anio,
            Kilometraje = input.Kilometraje,
            Precio = precio,
            Procedencia = input.Procedencia,
            MotorLitros = motor,
            ConfiguracionMotor = configuracion,
            Observaciones = input.Observaciones.Trim()
        };
        return errors;
    }
}
