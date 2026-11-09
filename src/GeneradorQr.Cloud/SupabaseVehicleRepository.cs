using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GeneradorQr.Cloud.Dto;
using GeneradorQr.Core.Data;
using GeneradorQr.Core.Models;

namespace GeneradorQr.Cloud;

/// <summary>
/// Inventario en Supabase vía PostgREST. Las modificaciones filtran por id y versión (bloqueo optimista):
/// si el servidor no devuelve filas, otro equipo cambió el auto antes.
/// </summary>
public sealed class SupabaseVehicleRepository(HttpClient http, SupabaseOptions options, SupabaseAuth auth) : IVehicleRepository
{
    private string Table => $"{options.Url}/rest/v1/vehiculos";

    public async Task<IReadOnlyList<VehicleRecord>> GetAllAsync(CancellationToken ct)
    {
        var rows = await SendAsync(HttpMethod.Get, $"{Table}?select=*&order=vendido.asc,id.desc", body: null, ct);
        return rows.Select(r => r.ToRecord()).ToList();
    }

    public Task<VehicleRecord> InsertAsync(VehicleRecord vehicle, CancellationToken ct) =>
        InsertCoreAsync(VehicleWrite.From(vehicle), ct);

    /// <summary>Solo para la migración: conserva la fecha de creación original.</summary>
    public Task<VehicleRecord> InsertMigratedAsync(VehicleRecord vehicle, CancellationToken ct) =>
        InsertCoreAsync(VehicleWrite.From(vehicle, includeCreatedAt: true), ct);

    public async Task<VehicleRecord> UpdateAsync(VehicleRecord vehicle, CancellationToken ct)
    {
        var rows = await SendAsync(HttpMethod.Patch, Filter(vehicle.Id, vehicle.Version), VehicleWrite.From(vehicle), ct);
        return Single(rows);
    }

    public async Task<VehicleRecord> SetVendidoAsync(long id, int expectedVersion, bool vendido, CancellationToken ct)
    {
        var rows = await SendAsync(HttpMethod.Patch, Filter(id, expectedVersion), new { vendido }, ct);
        return Single(rows);
    }

    public async Task DeleteAsync(long id, int expectedVersion, CancellationToken ct)
    {
        var rows = await SendAsync(HttpMethod.Delete, Filter(id, expectedVersion), body: null, ct);
        Single(rows);
    }

    private async Task<VehicleRecord> InsertCoreAsync(VehicleWrite write, CancellationToken ct)
    {
        var rows = await SendAsync(HttpMethod.Post, Table, write, ct);
        return rows.Count > 0
            ? rows[0].ToRecord()
            : throw new ServerUnavailableException("El servidor no confirmó el guardado.");
    }

    private string Filter(long id, int version) => $"{Table}?id=eq.{id}&version=eq.{version}";

    private static VehicleRecord Single(IReadOnlyList<VehicleRow> rows) =>
        rows.Count > 0 ? rows[0].ToRecord() : throw new ConcurrencyConflictException();

    private async Task<IReadOnlyList<VehicleRow>> SendAsync(HttpMethod method, string url, object? body, CancellationToken ct)
    {
        var token = await auth.GetAccessTokenAsync(ct);

        using var request = new HttpRequestMessage(method, url);
        request.Headers.Add("apikey", options.AnonKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (method != HttpMethod.Get)
        {
            request.Headers.Add("Prefer", "return=representation");
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType());
        }

        using var response = await SupabaseHttp.SendAsync(http, request, ct);

        if (SupabaseHttp.IsAuthError(response.StatusCode))
        {
            throw new AuthRequiredException();
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new ServerUnavailableException($"El servidor rechazó la operación: {await ReadMessageAsync(response, ct)}");
        }

        return await response.Content.ReadFromJsonAsync<List<VehicleRow>>(ct) ?? [];
    }

    private static async Task<string> ReadMessageAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var text = await response.Content.ReadAsStringAsync(ct);
        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.TryGetProperty("message", out var message))
            {
                return message.GetString() ?? text;
            }
        }
        catch (JsonException)
        {
            // No era JSON: se muestra tal cual.
        }

        return string.IsNullOrWhiteSpace(text) ? ((int)response.StatusCode).ToString() : text;
    }
}
