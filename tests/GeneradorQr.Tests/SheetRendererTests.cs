using System.Text;
using GeneradorQr.Core.Formatting;
using GeneradorQr.Core.Models;
using GeneradorQr.Core.Rendering;
using SkiaSharp;

namespace GeneradorQr.Tests;

public class SheetRendererTests
{
    private static VehicleRecord Sample(string observaciones = "Único dueño") => new()
    {
        Marca = "Ford",
        Modelo = "Lobo",
        Anio = 2020,
        Kilometraje = 45000,
        Precio = 495000m,
        Procedencia = "Nacional",
        MotorLitros = "5.0 L",
        ConfiguracionMotor = "V8",
        Observaciones = observaciones,
        CreadoEn = new DateTime(2026, 11, 8, 14, 5, 0)
    };

    [Fact]
    public void Png_TamanoCarta()
    {
        var v = Sample();
        using var logo = BrandAssets.LoadLogo();
        using var qr = QrRenderer.Render(VehicleText.BuildPayload(v), logo: logo);

        var png = SheetRenderer.RenderPng(qr, logo);

        using var bmp = SKBitmap.Decode(png);
        Assert.Equal(1700, bmp.Width);
        Assert.Equal(2200, bmp.Height);
    }

    [Fact]
    public void Png_QrDentroDeLaFicha_Decodifica()
    {
        var v = Sample();
        var payload = VehicleText.BuildPayload(v);
        using var logo = BrandAssets.LoadLogo();
        using var qr = QrRenderer.Render(payload, logo: logo);

        var png = SheetRenderer.RenderPng(qr, logo);

        Assert.Equal(payload, QrDecode.DecodePng(png));
    }

    [Fact]
    public void Pdf_EmpiezaConFirmaPdf()
    {
        var v = Sample();
        using var qr = QrRenderer.Render(VehicleText.BuildPayload(v));

        var pdf = SheetRenderer.RenderPdf(qr, logo: null);

        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public void Pdf_PesaMenosDe1MB_ParaMandarPorWhatsApp()
    {
        var v = Sample();
        using var logo = BrandAssets.LoadLogo();
        using var qr = QrRenderer.Render(VehicleText.BuildPayload(v), logo: logo);

        var pdf = SheetRenderer.RenderPdf(qr, logo);

        Assert.True(pdf.Length < 1_000_000, $"El PDF pesa {pdf.Length:N0} bytes");
    }

    [Fact]
    public void Pdf_ConTextoUnicode_NoFalla()
    {
        var v = Sample("ñ á 🚗 " + new string('x', 500));
        v.Vendido = true;
        using var logo = BrandAssets.LoadLogo();
        using var qr = QrRenderer.Render(VehicleText.BuildPayload(v), logo: logo);

        var pdf = SheetRenderer.RenderPdf(qr, logo);
        var png = SheetRenderer.RenderPng(qr, logo);

        Assert.True(pdf.Length > 1000);
        Assert.True(png.Length > 1000);
    }
}
