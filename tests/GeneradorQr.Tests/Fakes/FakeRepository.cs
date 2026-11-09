using GeneradorQr.Core.Data;
using GeneradorQr.Core.Models;

namespace GeneradorQr.Tests.Fakes;

/// <summary>
/// Repositorio en memoria que imita al servidor (ids, versión) y puede simular fallas.
/// </summary>
internal sealed class FakeRepository : IVehicleRepository
{
    private long _nextId = 1;

    public List<VehicleRecord> Rows { get; } = [];

    /// <summary>Si no es null, la siguiente llamada lanza esta excepción (y se limpia).</summary>
    public Exception? FailNext { get; set; }

    /// <summary>Si es true, todas las llamadas lanzan OfflineException.</summary>
    public bool Offline { get; set; }

    public int GetAllCalls { get; private set; }

    public VehicleRecord Seed(VehicleRecord v)
    {
        var copy = v.Clone();
        copy.Id = _nextId++;
        copy.Version = copy.Version == 0 ? 1 : copy.Version;
        Rows.Add(copy);
        return copy.Clone();
    }

    /// <summary>Simula que otro equipo editó el auto.</summary>
    public void TouchFromOtherDevice(long id, Action<VehicleRecord> change)
    {
        var row = Rows.Single(r => r.Id == id);
        change(row);
        row.Version++;
    }

    public Task<IReadOnlyList<VehicleRecord>> GetAllAsync(CancellationToken ct)
    {
        Check();
        GetAllCalls++;
        IReadOnlyList<VehicleRecord> result = Rows.Select(r => r.Clone()).ToList();
        return Task.FromResult(result);
    }

    public Task<VehicleRecord> InsertAsync(VehicleRecord vehicle, CancellationToken ct)
    {
        Check();
        var copy = vehicle.Clone();
        copy.Id = _nextId++;
        copy.Version = 1;
        copy.CreadoEn = vehicle.CreadoEn == default ? DateTime.Now : vehicle.CreadoEn;
        Rows.Add(copy);
        return Task.FromResult(copy.Clone());
    }

    public Task<VehicleRecord> UpdateAsync(VehicleRecord vehicle, CancellationToken ct)
    {
        Check();
        var index = Find(vehicle.Id, vehicle.Version);
        var copy = vehicle.Clone();
        copy.Version++;
        copy.CreadoEn = Rows[index].CreadoEn;
        copy.ActualizadoEn = DateTime.Now;
        Rows[index] = copy;
        return Task.FromResult(copy.Clone());
    }

    public Task<VehicleRecord> SetVendidoAsync(long id, int expectedVersion, bool vendido, CancellationToken ct)
    {
        Check();
        var index = Find(id, expectedVersion);
        Rows[index].Vendido = vendido;
        Rows[index].Version++;
        return Task.FromResult(Rows[index].Clone());
    }

    public Task DeleteAsync(long id, int expectedVersion, CancellationToken ct)
    {
        Check();
        Rows.RemoveAt(Find(id, expectedVersion));
        return Task.CompletedTask;
    }

    private int Find(long id, int version)
    {
        var index = Rows.FindIndex(r => r.Id == id && r.Version == version);
        return index >= 0 ? index : throw new ConcurrencyConflictException();
    }

    private void Check()
    {
        if (Offline)
        {
            throw new OfflineException();
        }

        if (FailNext is { } ex)
        {
            FailNext = null;
            throw ex;
        }
    }
}
