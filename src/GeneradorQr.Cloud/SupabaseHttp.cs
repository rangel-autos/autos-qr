using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GeneradorQr.Core.Data;

namespace GeneradorQr.Cloud;

/// <summary>Envío HTTP con el mapeo común de errores de red y de servidor.</summary>
internal static class SupabaseHttp
{
    public static async Task<HttpResponseMessage> SendAsync(HttpClient http, HttpRequestMessage request, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new OfflineException(inner: ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            // Timeout del HttpClient (no una cancelación pedida por la app).
            throw new OfflineException(inner: ex);
        }

        if ((int)response.StatusCode >= 500)
        {
            response.Dispose();
            throw new ServerUnavailableException();
        }

        return response;
    }

    /// <summary>
    /// Solo 401 es sesión vencida. Un 403 es falta de permisos en la tabla (grants/RLS) y
    /// pedir de nuevo la contraseña no lo arregla: se trata como error del servidor.
    /// </summary>
    public static bool IsAuthError(HttpStatusCode status) => status is HttpStatusCode.Unauthorized;

    /// <summary>
    /// Lee el JSON de una respuesta correcta. Si no es JSON (p. ej. la página de inicio de sesión
    /// de un Wi-Fi de hotel, que responde 200 con HTML), se trata como sin conexión.
    /// </summary>
    public static async Task<T?> ReadJsonAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(ct);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new OfflineException(inner: ex);
        }
    }
}
