using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace GeneradorQr.App.Views;

/// <summary>Convierte los bytes PNG de la vista previa en una imagen de Avalonia.</summary>
public sealed class PngBytesToBitmapConverter : IValueConverter
{
    public static readonly PngBytesToBitmapConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is byte[] { Length: > 0 } bytes ? new Bitmap(new MemoryStream(bytes)) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
