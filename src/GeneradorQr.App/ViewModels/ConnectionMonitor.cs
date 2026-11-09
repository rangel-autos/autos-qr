using GeneradorQr.Core.Data;

namespace GeneradorQr.App.ViewModels;

/// <summary>Reintenta cada 5 s hasta que el servidor vuelve a responder.</summary>
public sealed class ConnectionMonitor(TimeProvider clock)
{
    public static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(5);

    public bool IsOnline { get; private set; } = true;

    public void MarkOffline() => IsOnline = false;

    /// <summary>Ejecuta <paramref name="probe"/> cada <see cref="RetryInterval"/> hasta que no falle por red.</summary>
    public async Task RunUntilOnlineAsync(Func<Task> probe, CancellationToken ct)
    {
        IsOnline = false;
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(RetryInterval, clock, ct);
            try
            {
                await probe();
                IsOnline = true;
                return;
            }
            catch (Exception ex) when (ex is OfflineException or ServerUnavailableException)
            {
                // Sigue sin conexión: se reintenta.
            }
        }
    }
}
