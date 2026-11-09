using System.Text.Json;
using GeneradorQr.Core.Models;

namespace GeneradorQr.Core.Backup;

/// <summary>
/// Copia local del inventario en JSON. El plan gratuito de Supabase no incluye respaldos,
/// así que la app guarda los últimos <see cref="Keep"/> cada vez que abre.
/// </summary>
public sealed class BackupService(string directory, TimeProvider clock)
{
    public const int Keep = 7;
    private const string Prefix = "inventario-";

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public string Directory { get; } = directory;

    public async Task WriteAsync(IReadOnlyList<VehicleRecord> all, CancellationToken ct = default)
    {
        System.IO.Directory.CreateDirectory(Directory);
        var now = clock.GetLocalNow();
        var path = Path.Combine(Directory, $"{Prefix}{now:yyyy-MM-dd-HHmm}.json");

        await using (var stream = File.Create(path))
        {
            await JsonSerializer.SerializeAsync(stream, all, Options, ct);
        }

        // Los nombres llevan fecha ordenable, así que el orden alfabético es el cronológico.
        var old = System.IO.Directory.GetFiles(Directory, $"{Prefix}*.json")
            .OrderDescending(StringComparer.Ordinal)
            .Skip(Keep);
        foreach (var file in old)
        {
            File.Delete(file);
        }
    }
}
