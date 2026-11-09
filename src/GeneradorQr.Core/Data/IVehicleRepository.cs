using GeneradorQr.Core.Models;

namespace GeneradorQr.Core.Data;

/// <summary>
/// Inventario compartido. Las operaciones que modifican un auto existente reciben la versión que se cargó
/// y lanzan <see cref="ConcurrencyConflictException"/> si otro equipo lo cambió antes.
/// Errores: <see cref="OfflineException"/>, <see cref="ServerUnavailableException"/>, <see cref="AuthRequiredException"/>.
/// </summary>
public interface IVehicleRepository
{
    Task<IReadOnlyList<VehicleRecord>> GetAllAsync(CancellationToken ct);

    Task<VehicleRecord> InsertAsync(VehicleRecord vehicle, CancellationToken ct);

    /// <summary>Usa <see cref="VehicleRecord.Version"/> como versión esperada.</summary>
    Task<VehicleRecord> UpdateAsync(VehicleRecord vehicle, CancellationToken ct);

    Task<VehicleRecord> SetVendidoAsync(long id, int expectedVersion, bool vendido, CancellationToken ct);

    Task DeleteAsync(long id, int expectedVersion, CancellationToken ct);
}
