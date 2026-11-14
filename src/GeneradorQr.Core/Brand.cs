using SkiaSharp;

namespace GeneradorQr.Core;

/// <summary>
/// Identidad de Rangel Venta de Autos (rojo, negro y blanco) y catálogos del formulario.
/// </summary>
public static class Brand
{
    public const string Nombre = "Rangel Venta de Autos";
    public const string Ubicacion = "Pénjamo, Gto.";

    /// <summary>
    /// Página pública (GitHub Pages, carpeta web/) que muestra la ficha del auto al escanear el QR.
    /// El QR lleva esta dirección más el código del auto: https://…/?a=codigo
    /// </summary>
    public const string FichaUrl = "https://rangelautos.github.io/";

    public static readonly SKColor Red = new(214, 24, 31);
    public static readonly SKColor RedDark = new(172, 15, 21);
    public static readonly SKColor RedPressed = new(140, 10, 16);
    public static readonly SKColor RedTint = new(253, 236, 237);
    public static readonly SKColor Ink = new(22, 22, 25);
    public static readonly SKColor InkSoft = new(52, 52, 58);
    public static readonly SKColor Muted = new(108, 112, 120);
    public static readonly SKColor Surface = new(243, 244, 246);
    public static readonly SKColor Border = new(221, 223, 228);
    public static readonly SKColor Success = new(21, 128, 61);
    public static readonly SKColor SuccessTint = new(225, 245, 233);
    public static readonly SKColor Warning = new(180, 95, 6);
    public static readonly SKColor WarningTint = new(254, 243, 219);

    public static readonly IReadOnlyList<string> Procedencias =
        ["Nacional", "Americano legalizado", "Americano no legalizado"];

    public static readonly IReadOnlyList<string> MarcasComunes =
    [
        "Acura", "Audi", "BMW", "Buick", "Cadillac", "Chevrolet", "Chrysler", "Dodge", "Fiat", "Ford",
        "GMC", "Honda", "Hyundai", "Infiniti", "Jeep", "Kia", "Lincoln", "Mazda", "Mercedes-Benz", "MG",
        "Mitsubishi", "Nissan", "Peugeot", "RAM", "Renault", "Seat", "Subaru", "Suzuki", "Toyota", "Volkswagen", "Volvo"
    ];

    public static readonly IReadOnlyList<string> Configuraciones =
    [
        "3 cilindros", "4 cilindros", "4 cilindros turbo", "V6", "V6 turbo", "6 en línea", "V8", "V10",
        "Híbrido", "Eléctrico"
    ];
}
