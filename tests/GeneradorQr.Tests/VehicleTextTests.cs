using GeneradorQr.Core.Formatting;
using GeneradorQr.Core.Models;

namespace GeneradorQr.Tests;

public class VehicleTextTests
{
    private static VehicleRecord Sample(string observaciones = "Único dueño") => new()
    {
        Marca = "Ford",
        Modelo = "Lobo",
        Anio = 2020,
        Kilometraje = 45000,
        Precio = 495000.5m,
        Procedencia = "Nacional",
        MotorLitros = "5.0 L",
        ConfiguracionMotor = "V8",
        Observaciones = observaciones,
        CreadoEn = new DateTime(2026, 11, 8, 14, 5, 0)
    };

    [Fact]
    public void Payload_IgualQueVersionWindows()
    {
        Assert.Equal(
            "RANGEL VENTA DE AUTOS - Pénjamo, Gto.\nMarca: Ford\nModelo: Lobo\nAño: 2020\nKilometraje: 45,000 km\n" +
            "Precio: $495,000.50 MXN\nProcedencia: Nacional\nMotor: 5.0 L\nConfiguración del motor: V8\n" +
            "Observaciones: Único dueño\nGenerado: 08/11/2026 14:05",
            VehicleText.BuildPayload(Sample()).Replace("\r\n", "\n"));
    }

    [Fact]
    public void Payload_EnMillas_DiceMillaje()
    {
        var vehicle = Sample();
        vehicle.UnidadDistancia = UnidadDistancia.Mi;

        var payload = VehicleText.BuildPayload(vehicle);

        Assert.Contains("Millaje: 45,000 mi", payload);
        Assert.DoesNotContain("Kilometraje", payload);
    }

    [Theory]
    [InlineData(null, "km")]
    [InlineData("", "km")]
    [InlineData("otra", "km")]
    [InlineData("km", "km")]
    [InlineData(" MI ", "mi")]
    public void UnidadDistancia_Normalize(string? unidad, string expected)
    {
        Assert.Equal(expected, UnidadDistancia.Normalize(unidad));
    }

    [Fact]
    public void QrContent_ConCodigo_EsElEnlaceALaFicha()
    {
        var vehicle = Sample();
        vehicle.Codigo = "a1b2c3d4e5f60718";

        Assert.Equal("https://rangel-autos.github.io/?a=a1b2c3d4e5f60718", VehicleText.BuildQrContent(vehicle));
    }

    [Fact]
    public void QrContent_SinCodigo_LlevaElTexto()
    {
        Assert.Equal(VehicleText.BuildPayload(Sample()), VehicleText.BuildQrContent(Sample()));
    }

    [Fact]
    public void NuevoCodigo_16CaracteresYDistintos()
    {
        var a = VehicleRecord.NuevoCodigo();
        var b = VehicleRecord.NuevoCodigo();

        Assert.Matches("^[a-f0-9]{16}$", a);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Payload_SinObservaciones_OmiteLinea()
    {
        Assert.DoesNotContain("Observaciones:", VehicleText.BuildPayload(Sample("  ")));
    }

    [Theory]
    [InlineData("$495,000 MXN", 495000)]
    [InlineData("495000.50", 495000.50)]
    public void TryParsePrecio_Validos(string text, decimal expected)
    {
        Assert.True(VehicleText.TryParsePrecio(text, out var precio));
        Assert.Equal(expected, precio);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("-5")]
    public void TryParsePrecio_Invalidos(string text)
    {
        Assert.False(VehicleText.TryParsePrecio(text, out _));
    }

    [Theory]
    [InlineData("5", "5.0 L")]
    [InlineData("2,5 lts", "2.5 L")]
    [InlineData("1.6l", "1.6 L")]
    [InlineData("turbo", "turbo")]
    public void NormalizeMotor(string input, string expected)
    {
        Assert.Equal(expected, VehicleText.NormalizeMotor(input));
    }
}
