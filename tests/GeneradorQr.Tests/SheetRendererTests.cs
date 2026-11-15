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
    public void Png_ConQrSinInternet_LosDosCodigosDecodifican()
    {
        var v = Sample();
        v.Codigo = "a1b2c3d4e5f60718";
        var enlace = VehicleText.BuildQrContent(v);
        var texto = VehicleText.BuildOfflineQrContent(v)!;
        using var logo = BrandAssets.LoadLogo();
        using var qr = QrRenderer.Render(enlace, logo: logo);
        using var offlineQr = QrRenderer.Render(texto);

        var png = SheetRenderer.RenderPng(qr, logo, offlineQr: offlineQr);

        // La franja "sin internet" ocupa la parte baja de la hoja; el QR principal queda arriba de ella.
        using var sheet = SKBitmap.Decode(png);
        const int corte = 1680;
        using var arriba = new SKBitmap();
        using var abajo = new SKBitmap();
        Assert.True(sheet.ExtractSubset(arriba, SKRectI.Create(0, 0, sheet.Width, corte)));
        Assert.True(sheet.ExtractSubset(abajo, SKRectI.Create(0, corte, sheet.Width, sheet.Height - corte)));
        Assert.Equal(enlace, QrDecode.Decode(arriba));
        Assert.Equal(texto, QrDecode.Decode(abajo));
    }

    [Fact]
    public void OfflineQrContent_SinCodigo_NoHaySegundoQr()
    {
        Assert.Null(VehicleText.BuildOfflineQrContent(Sample()));
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
