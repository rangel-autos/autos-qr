using GeneradorQr.Core.Data;
using GeneradorQr.Core.Models;

namespace GeneradorQr.Migrate;

/// <summary>
/// Sube los autos locales a la nube una sola vez, todos en un solo envío. Se niega si la nube ya tiene datos (para no duplicar),
/// salvo que se pida explícitamente con --force.
/// </summary>
public sealed class Migrator(
    IVehicleRepository repository,
    Func<IReadOnlyList<VehicleRecord>, CancellationToken, Task<int>> insertManyMigrated)
{
    public async Task<int> RunAsync(IReadOnlyList<VehicleRecord> local, bool force, CancellationToken ct)
    {
        var remote = await repository.GetAllAsync(ct);
        if (remote.Count > 0 && !force)
        {
            throw new InvalidOperationException(
                $"La base en la nube ya tiene {remote.Count} autos. Usa --force para migrar de todos modos.");
        }

        // Un solo envío: PostgREST inserta el arreglo completo en una transacción,
        // así que si se corta la red no quedan autos a medias que luego se dupliquen.
        return await insertManyMigrated(local.OrderBy(v => v.Id).ToList(), ct);
    }
}
