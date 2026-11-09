using System.Net;
using System.Text.Json;
using GeneradorQr.Cloud;
using GeneradorQr.Core.Data;
using GeneradorQr.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace GeneradorQr.Tests;

public class SupabaseAuthTests
{
    internal static readonly SupabaseOptions Options = new("https://demo.supabase.co", "anon-key");

    internal static string TokenJson(string access, string refresh, int expiresIn = 3600) =>
        JsonSerializer.Serialize(new { access_token = access, refresh_token = refresh, expires_in = expiresIn, token_type = "bearer" });

    internal sealed class MemoryStore : ISessionStore
    {
        public string? Token { get; set; }
        public string? Load() => Token;
        public void Save(string refreshToken) => Token = refreshToken;
        public void Clear() => Token = null;
    }

    private static (SupabaseAuth Auth, FakeHttpHandler Http, MemoryStore Store, FakeTimeProvider Clock) Create()
    {
        var http = new FakeHttpHandler();
        var store = new MemoryStore();
        var clock = new FakeTimeProvider();
        return (new SupabaseAuth(http.Client(), Options, store, clock), http, store, clock);
    }

    [Fact]
    public async Task SignIn_Ok_GuardaRefreshToken()
    {
        var (auth, http, store, _) = Create();
        http.Respond(HttpStatusCode.OK, TokenJson("a1", "r1"));

        await auth.SignInAsync("agencia@rangel.mx", "secreta", default);

        Assert.Equal("r1", store.Token);
        var req = Assert.Single(http.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Equal("/auth/v1/token?grant_type=password", req.PathAndQuery);
        Assert.Equal("anon-key", req.Headers["apikey"]);
        Assert.Contains("\"email\":\"agencia@rangel.mx\"", req.Body);
        Assert.Equal("a1", await auth.GetAccessTokenAsync(default));
    }

    [Fact]
    public async Task SignIn_400_LanzaInvalidCredentials()
    {
        var (auth, http, store, _) = Create();
        http.Respond(HttpStatusCode.BadRequest, """{"error":"invalid_grant","error_description":"Invalid login credentials"}""");

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => auth.SignInAsync("a@b.mx", "mala", default));
        Assert.Null(store.Token);
    }

    [Fact]
    public async Task GetAccessToken_FaltanMenosDe60s_Refresca()
    {
        var (auth, http, store, clock) = Create();
        http.Respond(HttpStatusCode.OK, TokenJson("a1", "r1", expiresIn: 3600))
            .Respond(HttpStatusCode.OK, TokenJson("a2", "r2", expiresIn: 3600));
        await auth.SignInAsync("a@b.mx", "x", default);

        clock.Advance(TimeSpan.FromSeconds(3600 - 59));
        var token = await auth.GetAccessTokenAsync(default);

        Assert.Equal("a2", token);
        Assert.Equal("r2", store.Token);
        Assert.Equal("/auth/v1/token?grant_type=refresh_token", http.Requests[1].PathAndQuery);
        Assert.Contains("\"refresh_token\":\"r1\"", http.Requests[1].Body);
    }

    [Fact]
    public async Task GetAccessToken_TokenVigente_NoLlamaServidor()
    {
        var (auth, http, _, clock) = Create();
        http.Respond(HttpStatusCode.OK, TokenJson("a1", "r1"));
        await auth.SignInAsync("a@b.mx", "x", default);

        clock.Advance(TimeSpan.FromMinutes(30));

        Assert.Equal("a1", await auth.GetAccessTokenAsync(default));
        Assert.Single(http.Requests);
    }

    [Fact]
    public async Task GetAccessToken_SinSesion_LanzaAuthRequired()
    {
        var (auth, _, _, _) = Create();
        await Assert.ThrowsAsync<AuthRequiredException>(() => auth.GetAccessTokenAsync(default));
    }

    [Fact]
    public async Task TryRestore_SinToken_False()
    {
        var (auth, http, _, _) = Create();
        Assert.False(await auth.TryRestoreAsync(default));
        Assert.Empty(http.Requests);
    }

    [Fact]
    public async Task TryRestore_TokenGuardado_RenuevaYTrue()
    {
        var (auth, http, store, _) = Create();
        store.Token = "r0";
        http.Respond(HttpStatusCode.OK, TokenJson("a1", "r1"));

        Assert.True(await auth.TryRestoreAsync(default));
        Assert.Equal("r1", store.Token);
        Assert.Equal("a1", await auth.GetAccessTokenAsync(default));
    }

    [Fact]
    public async Task TryRestore_TokenRevocado_LimpiaYFalse()
    {
        var (auth, http, store, _) = Create();
        store.Token = "viejo";
        http.Respond(HttpStatusCode.BadRequest, """{"error":"invalid_grant","error_description":"Invalid Refresh Token"}""");

        Assert.False(await auth.TryRestoreAsync(default));
        Assert.Null(store.Token);
    }

    [Fact]
    public async Task TryRestore_SinRed_ConservaTokenParaReintentar()
    {
        var (auth, http, store, _) = Create();
        store.Token = "r0";
        http.Throw(new HttpRequestException("sin red"))
            .Respond(HttpStatusCode.OK, TokenJson("a1", "r1"));

        await Assert.ThrowsAsync<OfflineException>(() => auth.TryRestoreAsync(default));

        Assert.True(auth.IsSignedIn);
        Assert.Equal("r0", store.Token);
        Assert.Equal("a1", await auth.GetAccessTokenAsync(default));
        Assert.Contains("\"refresh_token\":\"r0\"", http.Requests[1].Body);
    }

    [Fact]
    public async Task SinRed_LanzaOffline()
    {
        var (auth, http, _, _) = Create();
        http.Throw(new HttpRequestException("no route"));
        await Assert.ThrowsAsync<OfflineException>(() => auth.SignInAsync("a@b.mx", "x", default));

        http.Throw(new TaskCanceledException("timeout"));
        await Assert.ThrowsAsync<OfflineException>(() => auth.SignInAsync("a@b.mx", "x", default));
    }

    [Fact]
    public async Task Servidor5xx_LanzaServerUnavailable()
    {
        var (auth, http, _, _) = Create();
        http.Respond(HttpStatusCode.ServiceUnavailable);
        await Assert.ThrowsAsync<ServerUnavailableException>(() => auth.SignInAsync("a@b.mx", "x", default));
    }

    [Fact]
    public async Task SignOut_RevocaEnServidorYLimpiaSesion()
    {
        var (auth, http, store, _) = Create();
        http.Respond(HttpStatusCode.OK, TokenJson("a1", "r1"))
            .Respond(HttpStatusCode.NoContent);
        await auth.SignInAsync("a@b.mx", "x", default);

        await auth.SignOutAsync(default);

        var req = http.Requests[1];
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Equal("/auth/v1/logout?scope=local", req.PathAndQuery);
        Assert.Equal("Bearer a1", req.Headers["Authorization"]);
        Assert.Equal("anon-key", req.Headers["apikey"]);
        Assert.Null(store.Token);
        await Assert.ThrowsAsync<AuthRequiredException>(() => auth.GetAccessTokenAsync(default));
    }

    [Fact]
    public async Task SignOut_SinRed_LimpiaSesionLocalDeTodosModos()
    {
        var (auth, http, store, _) = Create();
        http.Respond(HttpStatusCode.OK, TokenJson("a1", "r1"))
            .Throw(new HttpRequestException("sin red"));
        await auth.SignInAsync("a@b.mx", "x", default);

        await auth.SignOutAsync(default);

        Assert.Null(store.Token);
        Assert.False(auth.IsSignedIn);
    }

    [Fact]
    public async Task RespuestaNoJson_EnLogin_LanzaOffline()
    {
        var (auth, http, _, _) = Create();
        http.Respond(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>portal</html>") });
        await Assert.ThrowsAsync<OfflineException>(() => auth.SignInAsync("a@b.mx", "x", default));
    }

    [Fact]
    public async Task TryRestore_ArchivoDeSesionIlegible_False()
    {
        var http = new FakeHttpHandler();
        var auth = new SupabaseAuth(http.Client(), Options, new BrokenStore(), new FakeTimeProvider());
        Assert.False(await auth.TryRestoreAsync(default));
    }

    private sealed class BrokenStore : ISessionStore
    {
        public string? Load() => throw new IOException("bloqueado");
        public void Save(string refreshToken) => throw new IOException("bloqueado");
        public void Clear() { }
    }
}
