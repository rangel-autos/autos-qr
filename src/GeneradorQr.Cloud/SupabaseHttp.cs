using System.Net;
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

    public static bool IsAuthError(HttpStatusCode status) =>
        status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;
}
