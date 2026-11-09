using GeneradorQr.Core.Data;

namespace GeneradorQr.App.ViewModels;

/// <summary>Reintenta cada 5 s hasta que el servidor vuelve a responder.</summary>
public sealed class ConnectionMonitor(TimeProvider clock)
{
    public static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(5);

    public bool IsOnline { get; private set; } = true;

    public void MarkOffline() => IsOnline = false;

    /// <summary>Ejecuta <paramref name="probe"/> cada <see cref="RetryInterval"/> hasta que no falle por red.</summary>
    /// <remarks>
    /// <see cref="AuthRequiredException"/> y la cancelación se propagan; cualquier otro error se
    /// registra con <paramref name="logError"/> y se reintenta, para que el aviso de "sin conexión"
    /// no se quede pegado para siempre por una respuesta inesperada.
    /// </remarks>
    public async Task RunUntilOnlineAsync(Func<Task> probe, CancellationToken ct, Action<Exception>? logError = null)
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
            catch (Exception ex) when (ex is not (AuthRequiredException or OperationCanceledException))
            {
                logError?.Invoke(ex);
            }
        }
    }
}
