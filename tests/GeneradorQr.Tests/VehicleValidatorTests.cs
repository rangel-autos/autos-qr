using GeneradorQr.Core.Formatting;
using GeneradorQr.Core.Models;

namespace GeneradorQr.Tests;

public class VehicleValidatorTests
{
    [Fact]
    public void Validate_CamposVacios_DevuelveCincoErrores()
    {
        var input = new VehicleInput("", " ", 2020, 0, "", "Nacional", "", "", "");

        var errors = VehicleValidator.Validate(input, out var record);

        Assert.Null(record);
        Assert.Equal(
            new[]
            {
                new ValidationError(nameof(VehicleInput.Marca), "Escribe la marca."),
                new ValidationError(nameof(VehicleInput.Modelo), "Escribe el modelo."),
                new ValidationError(nameof(VehicleInput.PrecioTexto), "Escribe un precio válido, por ejemplo 495000."),
                new ValidationError(nameof(VehicleInput.MotorLitros), "Escribe el litraje, por ejemplo 2.5 L."),
                new ValidationError(nameof(VehicleInput.ConfiguracionMotor), "Elige o escribe la configuración, por ejemplo V6.")
            },
            errors);
    }

    [Fact]
    public void Validate_Valido_ArmaRecordConTrim()
    {
        var input = new VehicleInput(" Ford ", " Lobo ", 2020, 45000, "$495,000", "Nacional", " 5.0 L ", " V8 ", " Único dueño ");

        var errors = VehicleValidator.Validate(input, out var record);

        Assert.Empty(errors);
        Assert.NotNull(record);
        Assert.Equal("Ford", record.Marca);
        Assert.Equal("Lobo", record.Modelo);
        Assert.Equal(2020, record.Anio);
        Assert.Equal(45000, record.Kilometraje);
        Assert.Equal(495000m, record.Precio);
        Assert.Equal("Nacional", record.Procedencia);
        Assert.Equal("5.0 L", record.MotorLitros);
        Assert.Equal("V8", record.ConfiguracionMotor);
        Assert.Equal("Único dueño", record.Observaciones);
    }
}
