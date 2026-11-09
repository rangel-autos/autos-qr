using GeneradorQr.Core.Data;
using GeneradorQr.Core.Models;

namespace GeneradorQr.Migrate;

/// <summary>
/// Sube los autos locales a la nube una sola vez. Se niega si la nube ya tiene datos (para no duplicar),
/// salvo que se pida explícitamente con --force.
/// </summary>
public sealed class Migrator(
    IVehicleRepository repository,
    Func<VehicleRecord, CancellationToken, Task<VehicleRecord>> insertMigrated)
{
    public async Task<int> RunAsync(IReadOnlyList<VehicleRecord> local, bool force, CancellationToken ct)
    {
        var remote = await repository.GetAllAsync(ct);
        if (remote.Count > 0 && !force)
        {
            throw new InvalidOperationException(
                $"La base en la nube ya tiene {remote.Count} autos. Usa --force para migrar de todos modos.");
        }

        var count = 0;
        foreach (var vehicle in local.OrderBy(v => v.Id))
        {
            await insertMigrated(vehicle, ct);
            count++;
        }

        return count;
    }
}
