using System.Drawing.Drawing2D;

namespace GeneradorQrAutos;

/// <summary>
/// Colores y utilidades de dibujo basados en el logotipo de Rangel Venta de Autos (rojo, negro y blanco).
/// </summary>
internal static class Brand
{
    public const string Nombre = "Rangel Venta de Autos";
    public const string Ubicacion = "Pénjamo, Gto.";

    public static readonly Color Red = Color.FromArgb(214, 24, 31);
    public static readonly Color RedDark = Color.FromArgb(172, 15, 21);
    public static readonly Color RedPressed = Color.FromArgb(140, 10, 16);
    public static readonly Color RedTint = Color.FromArgb(253, 236, 237);
    public static readonly Color Ink = Color.FromArgb(22, 22, 25);
    public static readonly Color InkSoft = Color.FromArgb(52, 52, 58);
    public static readonly Color Muted = Color.FromArgb(108, 112, 120);
    public static readonly Color Surface = Color.FromArgb(243, 244, 246);
    public static readonly Color Border = Color.FromArgb(221, 223, 228);
    public static readonly Color Success = Color.FromArgb(21, 128, 61);
    public static readonly Color SuccessTint = Color.FromArgb(225, 245, 233);
    public static readonly Color Warning = Color.FromArgb(180, 95, 6);
    public static readonly Color WarningTint = Color.FromArgb(254, 243, 219);

    public static GraphicsPath RoundedRect(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        var d = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
        if (d <= 0)
        {
            path.AddRectangle(rect);
            return path;
        }

        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
