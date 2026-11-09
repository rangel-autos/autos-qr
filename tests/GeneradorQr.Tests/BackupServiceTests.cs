using System.Text.Json;
using GeneradorQr.Core.Backup;
using GeneradorQr.Core.Models;
using Microsoft.Extensions.Time.Testing;

namespace GeneradorQr.Tests;

public sealed class BackupServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "gqr-backup-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private static FakeTimeProvider ClockAt(DateTime local)
    {
        var clock = new FakeTimeProvider();
        clock.SetLocalTimeZone(TimeZoneInfo.Local);
        clock.SetUtcNow(new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local)).ToUniversalTime());
        return clock;
    }

    [Fact]
    public async Task Backup_EscribeJsonConNombreConFecha()
    {
        var clock = ClockAt(new DateTime(2026, 11, 8, 14, 5, 0));
        var service = new BackupService(_dir, clock);
        var data = new List<VehicleRecord> { new() { Id = 7, Marca = "Ford", Modelo = "Lobo", Precio = 495000.50m } };

        await service.WriteAsync(data);

        var file = Path.Combine(_dir, "inventario-2026-11-08-1405.json");
        Assert.True(File.Exists(file));
        var back = JsonSerializer.Deserialize<List<VehicleRecord>>(await File.ReadAllTextAsync(file))!;
        Assert.Equal(7, back[0].Id);
        Assert.Equal(495000.50m, back[0].Precio);
    }

    [Fact]
    public async Task Backup_ConservaSolo7()
    {
        var clock = ClockAt(new DateTime(2026, 11, 8, 14, 0, 0));
        var service = new BackupService(_dir, clock);

        for (var i = 0; i < 9; i++)
        {
            await service.WriteAsync([]);
            clock.Advance(TimeSpan.FromMinutes(1));
        }

        var names = Directory.GetFiles(_dir).Select(Path.GetFileName).Order().ToArray();
        Assert.Equal(7, names.Length);
        Assert.Equal("inventario-2026-11-08-1402.json", names[0]);
        Assert.Equal("inventario-2026-11-08-1408.json", names[^1]);
    }
}
