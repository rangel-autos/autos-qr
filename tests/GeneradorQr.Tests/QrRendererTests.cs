using GeneradorQr.Core.Rendering;

namespace GeneradorQr.Tests;

public class QrRendererTests
{
    private const string Payload = "RANGEL VENTA DE AUTOS - Pénjamo, Gto.\nMarca: Ford\nModelo: Lobo\nAño: 2020";

    [Fact]
    public void Qr_SinLogo_Decodifica()
    {
        using var bmp = QrRenderer.Render(Payload);
        Assert.Equal(Payload, QrDecode.Decode(bmp));
    }

    [Fact]
    public void Qr_ConLogo_Decodifica()
    {
        using var logo = BrandAssets.LoadLogo();
        using var bmp = QrRenderer.Render(Payload, logo: logo);
        Assert.Equal(Payload, QrDecode.Decode(bmp));
    }

    [Fact]
    public void Qr_ConAcentosYEmoji_Decodifica()
    {
        const string text = "Año ñandú 🚗";
        using var logo = BrandAssets.LoadLogo();
        using var bmp = QrRenderer.Render(text, logo: logo);
        Assert.Equal(text, QrDecode.Decode(bmp));
    }

    [Fact]
    public void Qr_ConLogo_UsaEccH()
    {
        using var logo = BrandAssets.LoadLogo();
        using var sinLogo = QrRenderer.Render(Payload);
        using var conLogo = QrRenderer.Render(Payload, logo: logo);
        Assert.True(conLogo.Width > sinLogo.Width);
    }

    [Fact]
    public void EncodePng_DevuelvePngLegible()
    {
        using var bmp = QrRenderer.Render(Payload);
        var png = QrRenderer.EncodePng(bmp);
        Assert.Equal(Payload, QrDecode.DecodePng(png));
    }
}
