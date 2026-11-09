using System.Net.Http.Json;
using System.Text.Json.Serialization;
using GeneradorQr.Core.Data;

namespace GeneradorQr.Cloud;

/// <summary>
/// Sesión de la cuenta compartida de la agencia contra Supabase Auth (GoTrue).
/// Guarda solo el refresh token y renueva el access token cuando le falta menos de un minuto.
/// </summary>
public sealed class SupabaseAuth(HttpClient http, SupabaseOptions options, ISessionStore store, TimeProvider clock)
{
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromSeconds(60);

    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _accessToken;
    private string? _refreshToken;
    private DateTimeOffset _expiresAt;

    public bool IsSignedIn => _refreshToken is not null;

    public async Task SignInAsync(string email, string password, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var session = await RequestTokenAsync("password", new { email, password }, ct)
                ?? throw new InvalidCredentialsException();
            Apply(session);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Intenta reanudar la sesión guardada. Devuelve false si no hay o si ya no es válida.</summary>
    public async Task<bool> TryRestoreAsync(CancellationToken ct)
    {
        var saved = store.Load();
        if (saved is null)
        {
            return false;
        }

        await _lock.WaitAsync(ct);
        try
        {
            var session = await RequestTokenAsync("refresh_token", new { refresh_token = saved }, ct);
            if (session is null)
            {
                ClearSession();
                return false;
            }

            Apply(session);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (_refreshToken is null)
            {
                throw new AuthRequiredException();
            }

            if (_accessToken is not null && clock.GetUtcNow() < _expiresAt - RefreshMargin)
            {
                return _accessToken;
            }

            var session = await RequestTokenAsync("refresh_token", new { refresh_token = _refreshToken }, ct);
            if (session is null)
            {
                ClearSession();
                throw new AuthRequiredException();
            }

            Apply(session);
            return _accessToken!;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Revoca la sesión de este equipo en Supabase (scope=local: el otro equipo sigue conectado)
    /// y borra el token guardado. Si no hay red, se borra igual el token local.
    /// </summary>
    public async Task SignOutAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (_accessToken is not null)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{options.Url}/auth/v1/logout?scope=local");
                request.Headers.Add("apikey", options.AnonKey);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
                try
                {
                    using var _ = await SupabaseHttp.SendAsync(http, request, ct);
                }
                catch (Exception ex) when (ex is OfflineException or ServerUnavailableException)
                {
                    // Sin red o servidor caído: no se puede revocar ahora; el token local se borra igual.
                }
            }
        }
        finally
        {
            ClearSession();
            _lock.Release();
        }
    }

    /// <summary>Devuelve null si Supabase rechaza las credenciales o el refresh token (4xx).</summary>
    private async Task<TokenResponse?> RequestTokenAsync(string grantType, object body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{options.Url}/auth/v1/token?grant_type={grantType}")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("apikey", options.AnonKey);

        using var response = await SupabaseHttp.SendAsync(http, request, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(ct);
        return token is { AccessToken.Length: > 0, RefreshToken.Length: > 0 } ? token : null;
    }

    private void Apply(TokenResponse session)
    {
        _accessToken = session.AccessToken;
        _refreshToken = session.RefreshToken;
        _expiresAt = clock.GetUtcNow().AddSeconds(session.ExpiresIn);
        store.Save(session.RefreshToken);
    }

    private void ClearSession()
    {
        _accessToken = null;
        _refreshToken = null;
        store.Clear();
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string RefreshToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
