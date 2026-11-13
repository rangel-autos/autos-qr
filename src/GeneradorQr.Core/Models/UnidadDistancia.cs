namespace GeneradorQr.Core.Models;

/// <summary>
/// Unidad en la que se capturó el recorrido del auto: kilómetros o millas (autos americanos).
/// El número se guarda tal cual se escribió; no se convierte entre unidades.
/// </summary>
public static class UnidadDistancia
{
    public const string Km = "km";
    public const string Mi = "mi";

    public static readonly IReadOnlyList<string> Todas = [Km, Mi];

    /// <summary>Cualquier valor desconocido o vacío (autos guardados antes de existir la unidad) cuenta como km.</summary>
    public static string Normalize(string? unidad) =>
        string.Equals(unidad?.Trim(), Mi, StringComparison.OrdinalIgnoreCase) ? Mi : Km;

    /// <summary>"Kilometraje" o "Millaje".</summary>
    public static string Etiqueta(string? unidad) => Normalize(unidad) == Mi ? "Millaje" : "Kilometraje";
}
