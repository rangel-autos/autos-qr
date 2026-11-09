using System.Diagnostics;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using GeneradorQr.Core.Formatting;
using GeneradorQr.Core.Models;
using GeneradorQr.Core.Rendering;

namespace GeneradorQr.App.Services;

public interface IFileActions
{
    /// <summary>Genera la ficha en PDF y la abre con el visor del sistema (Vista Previa en Mac) para imprimir o compartir.</summary>
    Task OpenPdfAsync(VehicleRecord vehicle);

    Task<string?> SavePngAsync(VehicleRecord vehicle, Window owner);

    Task<string?> SaveQrAsync(VehicleRecord vehicle, Window owner);

    Task CopyQrAsync(VehicleRecord vehicle, Window owner);
}

public sealed class FileActions(Action<string> launcher, string tempDir) : IFileActions
{
    public static FileActions CreateDefault() =>
        new(OpenWithSystem, Path.Combine(Path.GetTempPath(), "GeneradorQR"));

    public async Task OpenPdfAsync(VehicleRecord vehicle)
    {
        Directory.CreateDirectory(tempDir);
        var path = Path.Combine(tempDir, $"Ficha-{SafeFileName($"{vehicle.Titulo} {vehicle.Anio}")}.pdf");
        await File.WriteAllBytesAsync(path, RenderSheet(vehicle, pdf: true));
        launcher(path);
    }

    public Task<string?> SavePngAsync(VehicleRecord vehicle, Window owner) =>
        SaveAsync(owner, $"Ficha-{SafeFileName($"{vehicle.Titulo} {vehicle.Anio}")}.png", () => RenderSheet(vehicle, pdf: false));

    public Task<string?> SaveQrAsync(VehicleRecord vehicle, Window owner) =>
        SaveAsync(owner, $"QR-{SafeFileName($"{vehicle.Titulo} {vehicle.Anio}")}.png", () => RenderQrPng(vehicle));

    /// <summary>
    /// Copia el QR como imagen y, de respaldo, como archivo PNG (que WhatsApp, Finder y el Explorador aceptan al pegar).
    /// </summary>
    public async Task CopyQrAsync(VehicleRecord vehicle, Window owner)
    {
        var clipboard = TopLevel.GetTopLevel(owner)?.Clipboard
            ?? throw new InvalidOperationException("No hay portapapeles disponible.");

        Directory.CreateDirectory(tempDir);
        var png = RenderQrPng(vehicle);
        var path = Path.Combine(tempDir, $"QR-{SafeFileName($"{vehicle.Titulo} {vehicle.Anio}")}.png");
        await File.WriteAllBytesAsync(path, png);

        // No se libera aquí: el sistema puede leer la imagen del portapapeles después.
        var bitmap = new Bitmap(new MemoryStream(png));
        var item = new DataTransferItem();
        item.SetBitmap(bitmap);
        var file = await owner.StorageProvider.TryGetFileFromPathAsync(path);
        if (file is not null)
        {
            item.SetFile(file);
        }

        var data = new DataTransfer();
        data.Add(item);
        await clipboard.SetDataAsync(data);
    }

    public static byte[] RenderQrPng(VehicleRecord vehicle)
    {
        using var logo = BrandAssets.LoadLogo();
        using var qr = QrRenderer.Render(VehicleText.BuildPayload(vehicle), pixelsPerModule: 12, logo: logo);
        return QrRenderer.EncodePng(qr);
    }

    /// <summary>Nombre de archivo válido en Windows y macOS: sin caracteres reservados, espacios como guiones.</summary>
    public static string SafeFileName(string text)
    {
        var invalid = new HashSet<char>(Path.GetInvalidFileNameChars()) { '/', '\\', ':', '*', '?', '"', '<', '>', '|' };
        var builder = new StringBuilder();
        foreach (var c in text.Trim())
        {
            builder.Append(invalid.Contains(c) || char.IsWhiteSpace(c) ? '-' : c);
        }

        var result = string.Join('-', builder.ToString().Split('-', StringSplitOptions.RemoveEmptyEntries));
        return result.Length == 0 ? "vehiculo" : result;
    }

    private static byte[] RenderSheet(VehicleRecord vehicle, bool pdf)
    {
        using var logo = BrandAssets.LoadLogo();
        using var qr = QrRenderer.Render(VehicleText.BuildPayload(vehicle), pixelsPerModule: 10, logo: logo);
        return pdf ? SheetRenderer.RenderPdf(vehicle, qr, logo) : SheetRenderer.RenderPng(vehicle, qr, logo);
    }

    private static async Task<string?> SaveAsync(Window owner, string suggestedName, Func<byte[]> render)
    {
        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Guardar imagen",
            SuggestedFileName = suggestedName,
            DefaultExtension = "png",
            ShowOverwritePrompt = true,
            FileTypeChoices = [new FilePickerFileType("Imagen PNG") { Patterns = ["*.png"] }]
        });

        if (file is null)
        {
            return null;
        }

        var bytes = render();
        await using var stream = await file.OpenWriteAsync();
        stream.SetLength(0);
        await stream.WriteAsync(bytes);
        return file.TryGetLocalPath() ?? file.Name;
    }

    private static void OpenWithSystem(string path)
    {
        if (OperatingSystem.IsMacOS())
        {
            Process.Start("open", [path]);
        }
        else
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
    }
}
