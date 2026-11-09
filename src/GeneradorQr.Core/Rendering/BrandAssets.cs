using SkiaSharp;

namespace GeneradorQr.Core.Rendering;

public static class BrandAssets
{
    private const string LogoResource = "GeneradorQr.Core.Assets.rangel-logo.png";

    /// <summary>Carga el logotipo de Rangel. Quien llama libera el bitmap.</summary>
    public static SKBitmap LoadLogo()
    {
        using var stream = OpenResource(LogoResource);
        return SKBitmap.Decode(stream)
            ?? throw new InvalidOperationException("No se pudo leer el logotipo.");
    }

    internal static Stream OpenResource(string name) =>
        typeof(BrandAssets).Assembly.GetManifestResourceStream(name)
        ?? throw new InvalidOperationException($"Recurso no encontrado: {name}");
}
