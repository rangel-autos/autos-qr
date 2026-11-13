using GeneradorQr.Core.Data;
using GeneradorQr.Core.Models;

namespace GeneradorQr.App.Services;

/// <summary>
/// Inventario en memoria para el modo demo (variable de entorno GENERADORQR_DEMO=1):
/// sirve para mostrar la app sin cuenta de Supabase. Nada se guarda al cerrar.
/// </summary>
internal sealed class DemoVehicleRepository : IVehicleRepository
{
    private readonly List<VehicleRecord> _rows = [];
    private readonly Lock _gate = new();
    private long _nextId = 1;

    public static bool IsEnabled => Environment.GetEnvironmentVariable("GENERADORQR_DEMO") == "1";

    public DemoVehicleRepository()
    {
        Add(new VehicleRecord { Marca = "Ford", Modelo = "Lobo Lariat", Anio = 2020, Kilometraje = 45000, Precio = 495000m,
            Procedencia = "Americano legalizado", MotorLitros = "5.0 L", ConfiguracionMotor = "V8", Observaciones = "Único dueño" });
        Add(new VehicleRecord { Marca = "Nissan", Modelo = "Versa", Anio = 2021, Kilometraje = 18000, Precio = 255000m,
            Procedencia = "Nacional", MotorLitros = "1.6 L", ConfiguracionMotor = "4 cilindros" });
        Add(new VehicleRecord { Marca = "Chevrolet", Modelo = "Silverado", Anio = 2018, Kilometraje = 61000, UnidadDistancia = "mi", Precio = 420000m,
            Procedencia = "Americano no legalizado", MotorLitros = "5.3 L", ConfiguracionMotor = "V8", Vendido = true });
    }

    public Task<IReadOnlyList<VehicleRecord>> GetAllAsync(CancellationToken ct)
    {
        lock (_gate)
        {
            IReadOnlyList<VehicleRecord> copy = _rows.Select(r => r.Clone()).ToList();
            return Task.FromResult(copy);
        }
    }

    public Task<VehicleRecord> InsertAsync(VehicleRecord vehicle, CancellationToken ct)
    {
        lock (_gate)
        {
            return Task.FromResult(Add(vehicle).Clone());
        }
    }

    public Task<VehicleRecord> UpdateAsync(VehicleRecord vehicle, CancellationToken ct)
    {
        lock (_gate)
        {
            var i = Find(vehicle.Id, vehicle.Version);
            var copy = vehicle.Clone();
            copy.Version++;
            copy.ActualizadoEn = DateTime.Now;
            _rows[i] = copy;
            return Task.FromResult(copy.Clone());
        }
    }

    public Task<VehicleRecord> SetVendidoAsync(long id, int expectedVersion, bool vendido, CancellationToken ct)
    {
        lock (_gate)
        {
            var i = Find(id, expectedVersion);
            _rows[i].Vendido = vendido;
            _rows[i].Version++;
            return Task.FromResult(_rows[i].Clone());
        }
    }

    public Task DeleteAsync(long id, int expectedVersion, CancellationToken ct)
    {
        lock (_gate)
        {
            _rows.RemoveAt(Find(id, expectedVersion));
            return Task.CompletedTask;
        }
    }

    private VehicleRecord Add(VehicleRecord v)
    {
        var copy = v.Clone();
        copy.Id = _nextId++;
        copy.Version = 1;
        if (copy.Codigo.Length == 0) copy.Codigo = VehicleRecord.NuevoCodigo();
        copy.CreadoEn = copy.CreadoEn == default ? DateTime.Now : copy.CreadoEn;
        _rows.Add(copy);
        return copy;
    }

    private int Find(long id, int version)
    {
        var i = _rows.FindIndex(r => r.Id == id && r.Version == version);
        return i >= 0 ? i : throw new ConcurrencyConflictException();
    }
}
