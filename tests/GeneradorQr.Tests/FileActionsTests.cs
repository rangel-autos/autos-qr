using System.Text;
using GeneradorQr.App.Services;
using GeneradorQr.Core.Models;

namespace GeneradorQr.Tests;

public sealed class FileActionsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "gqr-files-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private static VehicleRecord Lobo() => new()
    {
        Marca = "Ford", Modelo = "Lobo", Anio = 2020, Kilometraje = 45000, Precio = 495000m,
        Procedencia = "Nacional", MotorLitros = "5.0 L", ConfiguracionMotor = "V8", CreadoEn = DateTime.Now
    };

    [Theory]
    [InlineData("Ford Lobo 2020/ñ", "Ford-Lobo-2020-ñ")]
    [InlineData("  RAM: 1500 * ", "RAM-1500")]
    [InlineData("///", "vehiculo")]
    public void NombreArchivo_SinCaracteresInvalidos(string input, string expected)
    {
        Assert.Equal(expected, FileActions.SafeFileName(input));
    }

    [Fact]
    public async Task OpenPdf_EscribeEnTempYLlamaAbrir()
    {
        string? opened = null;
        var actions = new FileActions(path => opened = path, _dir);

        await actions.OpenPdfAsync(Lobo());

        Assert.NotNull(opened);
        Assert.Equal(Path.Combine(_dir, "Ficha-Ford-Lobo-2020.pdf"), opened);
        var bytes = await File.ReadAllBytesAsync(opened);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public async Task OpenPdf_DosVeces_SobrescribeSinError()
    {
        var actions = new FileActions(_ => { }, _dir);
        await actions.OpenPdfAsync(Lobo());
        await actions.OpenPdfAsync(Lobo());
        Assert.Single(Directory.GetFiles(_dir));
    }

    [Fact]
    public void QrPng_ContieneElTextoDelAuto()
    {
        var png = FileActions.RenderQrPng(Lobo());
        Assert.Contains("Modelo: Lobo", QrDecode.DecodePng(png));
    }
}
