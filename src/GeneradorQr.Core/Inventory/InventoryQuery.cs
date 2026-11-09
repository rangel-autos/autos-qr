using GeneradorQr.Core.Models;

namespace GeneradorQr.Core.Inventory;

/// <summary>
/// Búsqueda y resumen sobre el inventario ya descargado (sin consultas a internet).
/// </summary>
public static class InventoryQuery
{
    /// <summary>Cada palabra del texto debe aparecer en algún campo (marca, modelo, año, motor, etc.).</summary>
    public static IReadOnlyList<VehicleRecord> Search(IEnumerable<VehicleRecord> all, string? text, bool includeSold)
    {
        var terms = (text ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return all
            .Where(v => includeSold || !v.Vendido)
            .Where(v =>
            {
                var haystack = SearchText(v);
                return terms.All(t => haystack.Contains(t, StringComparison.CurrentCultureIgnoreCase));
            })
            .OrderBy(v => v.Vendido)
            .ThenByDescending(v => v.Id)
            .ToList();
    }

    public static InventoryStats Stats(IEnumerable<VehicleRecord> all)
    {
        int disponibles = 0, vendidos = 0;
        decimal valor = 0;
        foreach (var v in all)
        {
            if (v.Vendido)
            {
                vendidos++;
            }
            else
            {
                disponibles++;
                valor += v.Precio;
            }
        }

        return new InventoryStats(disponibles, vendidos, valor);
    }

    public static IReadOnlyList<string> DistinctMarcas(IEnumerable<VehicleRecord> all) => Distinct(all.Select(v => v.Marca));

    public static IReadOnlyList<string> DistinctModelos(IEnumerable<VehicleRecord> all) => Distinct(all.Select(v => v.Modelo));

    private static string SearchText(VehicleRecord v) =>
        $"{v.Marca} {v.Modelo} {v.Anio} {v.Procedencia} {v.MotorLitros} {v.ConfiguracionMotor} {v.Observaciones}";

    private static IReadOnlyList<string> Distinct(IEnumerable<string> values) => values
        .Select(s => s.Trim())
        .Where(s => s.Length > 0)
        .Distinct(StringComparer.CurrentCultureIgnoreCase)
        .Order(StringComparer.CurrentCultureIgnoreCase)
        .ToList();
}
