using SkiaSharp;
using ZXing;
using ZXing.Common;

namespace GeneradorQr.Tests;

/// <summary>
/// Lee un QR desde un SKBitmap con ZXing (sin el paquete de bindings, que exige otra versión de SkiaSharp).
/// </summary>
internal static class QrDecode
{
    public static string? Decode(SKBitmap bitmap)
    {
        using var rgba = bitmap.Copy(SKColorType.Rgba8888);
        var bytes = rgba.Bytes;
        var source = new RGBLuminanceSource(bytes, rgba.Width, rgba.Height, RGBLuminanceSource.BitmapFormat.RGBA32);
        var reader = new BarcodeReaderGeneric
        {
            AutoRotate = false,
            Options = new DecodingOptions
            {
                TryHarder = true,
                PossibleFormats = [BarcodeFormat.QR_CODE],
                CharacterSet = "UTF-8"
            }
        };
        return reader.Decode(source)?.Text;
    }

    public static string? DecodePng(byte[] png)
    {
        using var bitmap = SKBitmap.Decode(png);
        return Decode(bitmap);
    }
}
