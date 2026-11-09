using System.Net;
using GeneradorQr.Cloud;
using GeneradorQr.Core.Data;
using GeneradorQr.Core.Models;
using GeneradorQr.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace GeneradorQr.Tests;

public class SupabaseVehicleRepositoryTests
{
    private const string Row = """
        {"id":5,"marca":"Ford","modelo":"Lobo","anio":2020,"kilometraje":45000,"precio":495000.50,
         "procedencia":"Nacional","motor_litros":"5.0 L","configuracion_motor":"V8","observaciones":"",
         "vendido":false,"creado_en":"2026-11-08T20:05:00+00:00","actualizado_en":"2026-11-08T20:05:00+00:00","version":3}
        """;

    /// <summary>Repositorio con sesión iniciada; la primera respuesta del handler ya está consumida por el login.</summary>
    private static async Task<(SupabaseVehicleRepository Repo, FakeHttpHandler Http)> CreateAsync()
    {
        var http = new FakeHttpHandler();
        http.Respond(HttpStatusCode.OK, SupabaseAuthTests.TokenJson("acc", "ref"));
        var client = http.Client();
        var auth = new SupabaseAuth(client, SupabaseAuthTests.Options, new SupabaseAuthTests.MemoryStore(), new FakeTimeProvider());
        await auth.SignInAsync("a@b.mx", "x", default);
        http.Requests.Clear();
        return (new SupabaseVehicleRepository(client, SupabaseAuthTests.Options, auth), http);
    }

    private static VehicleRecord Sample() => new()
    {
        Id = 5,
        Marca = "Ford",
        Modelo = "Lobo",
        Anio = 2020,
        Kilometraje = 45000,
        Precio = 495000.50m,
        Procedencia = "Nacional",
        MotorLitros = "5.0 L",
        ConfiguracionMotor = "V8",
        Version = 3,
        CreadoEn = DateTime.Now
    };

    [Fact]
    public async Task GetAll_PideOrdenYHeaders()
    {
        var (repo, http) = await CreateAsync();
        http.Respond(HttpStatusCode.OK, $"[{Row}]");

        var all = await repo.GetAllAsync(default);

        Assert.Single(all);
        var req = Assert.Single(http.Requests);
        Assert.Equal(HttpMethod.Get, req.Method);
        Assert.Equal("/rest/v1/vehiculos?select=*&order=vendido.asc,id.desc", req.PathAndQuery);
        Assert.Equal("anon-key", req.Headers["apikey"]);
        Assert.Equal("Bearer acc", req.Headers["Authorization"]);
    }

    [Fact]
    public async Task GetAll_ConvierteFechasAHoraLocal()
    {
        var (repo, http) = await CreateAsync();
        http.Respond(HttpStatusCode.OK, $"[{Row}]");

        var v = (await repo.GetAllAsync(default))[0];

        Assert.Equal(DateTimeKind.Local, v.CreadoEn.Kind);
        Assert.Equal(new DateTimeOffset(2026, 11, 8, 20, 5, 0, TimeSpan.Zero).LocalDateTime, v.CreadoEn);
        Assert.Equal(DateTimeKind.Local, v.ActualizadoEn!.Value.Kind);
    }

    [Fact]
    public async Task GetAll_PrecioDecimalExacto()
    {
        var (repo, http) = await CreateAsync();
        http.Respond(HttpStatusCode.OK, $"[{Row}]");

        var v = (await repo.GetAllAsync(default))[0];

        Assert.Equal(495000.50m, v.Precio);
        Assert.Equal(3, v.Version);
        Assert.Equal("5.0 L", v.MotorLitros);
    }

    [Fact]
    public async Task Insert_NoEnviaIdNiVersion_DevuelveFilaDelServidor()
    {
        var (repo, http) = await CreateAsync();
        http.Respond(HttpStatusCode.Created, $"[{Row}]");

        var saved = await repo.InsertAsync(Sample(), default);

        Assert.Equal(5, saved.Id);
        var req = Assert.Single(http.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Equal("/rest/v1/vehiculos", req.PathAndQuery);
        Assert.Equal("return=representation", req.Headers["Prefer"]);
        Assert.DoesNotContain("\"id\"", req.Body);
        Assert.DoesNotContain("\"version\"", req.Body);
        Assert.DoesNotContain("\"creado_en\"", req.Body);
        Assert.DoesNotContain("\"actualizado_en\"", req.Body);
        Assert.Contains("\"precio\":495000.50", req.Body);
    }

    [Fact]
    public async Task Update_FiltraPorIdYVersion()
    {
        var (repo, http) = await CreateAsync();
        http.Respond(HttpStatusCode.OK, $"[{Row.Replace("\"version\":3", "\"version\":4")}]");

        var saved = await repo.UpdateAsync(Sample(), default);

        Assert.Equal(4, saved.Version);
        var req = Assert.Single(http.Requests);
        Assert.Equal(HttpMethod.Patch, req.Method);
        Assert.Equal("/rest/v1/vehiculos?id=eq.5&version=eq.3", req.PathAndQuery);
        Assert.Equal("return=representation", req.Headers["Prefer"]);
    }

    [Fact]
    public async Task Update_RespuestaVacia_LanzaConflicto()
    {
        var (repo, http) = await CreateAsync();
        http.Respond(HttpStatusCode.OK, "[]");
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => repo.UpdateAsync(Sample(), default));
    }

    [Fact]
    public async Task Delete_RespuestaVacia_LanzaConflicto()
    {
        var (repo, http) = await CreateAsync();
        http.Respond(HttpStatusCode.OK, "[]");

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => repo.DeleteAsync(5, 3, default));
        Assert.Equal("/rest/v1/vehiculos?id=eq.5&version=eq.3", http.Requests[0].PathAndQuery);
        Assert.Equal(HttpMethod.Delete, http.Requests[0].Method);
    }

    [Fact]
    public async Task Delete_Ok()
    {
        var (repo, http) = await CreateAsync();
        http.Respond(HttpStatusCode.OK, $"[{Row}]");
        await repo.DeleteAsync(5, 3, default);
        Assert.Equal("return=representation", http.Requests[0].Headers["Prefer"]);
    }

    [Fact]
    public async Task SetVendido_EnviaSoloVendido()
    {
        var (repo, http) = await CreateAsync();
        http.Respond(HttpStatusCode.OK, $"[{Row.Replace("\"vendido\":false", "\"vendido\":true")}]");

        var saved = await repo.SetVendidoAsync(5, 3, true, default);

        Assert.True(saved.Vendido);
        var req = Assert.Single(http.Requests);
        Assert.Equal(HttpMethod.Patch, req.Method);
        Assert.Equal("/rest/v1/vehiculos?id=eq.5&version=eq.3", req.PathAndQuery);
        Assert.Equal("""{"vendido":true}""", req.Body);
    }

    [Fact]
    public async Task Http401_LanzaAuthRequired()
    {
        var (repo, http) = await CreateAsync();
        http.Respond(HttpStatusCode.Unauthorized, """{"message":"JWT expired"}""");
        await Assert.ThrowsAsync<AuthRequiredException>(() => repo.GetAllAsync(default));
    }

    [Fact]
    public async Task Http5xx_LanzaServerUnavailable()
    {
        var (repo, http) = await CreateAsync();
        http.Respond(HttpStatusCode.BadGateway);
        await Assert.ThrowsAsync<ServerUnavailableException>(() => repo.GetAllAsync(default));
    }

    [Fact]
    public async Task HttpRequestException_LanzaOffline()
    {
        var (repo, http) = await CreateAsync();
        http.Throw(new HttpRequestException("dns"));
        await Assert.ThrowsAsync<OfflineException>(() => repo.GetAllAsync(default));
    }

    [Fact]
    public async Task Http400_LanzaServerUnavailableConDetalle()
    {
        var (repo, http) = await CreateAsync();
        http.Respond(HttpStatusCode.BadRequest, """{"message":"new row violates check constraint"}""");
        var ex = await Assert.ThrowsAsync<ServerUnavailableException>(() => repo.InsertAsync(Sample(), default));
        Assert.Contains("check constraint", ex.Message);
    }

    [Fact]
    public async Task RespuestaNoJson_PortalCautivo_LanzaOffline()
    {
        var (repo, http) = await CreateAsync();
        http.Respond(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>Inicia sesión en el Wi-Fi</html>", System.Text.Encoding.UTF8, "text/html") });
        await Assert.ThrowsAsync<OfflineException>(() => repo.GetAllAsync(default));
    }

    [Fact]
    public async Task Http403_LanzaServerUnavailableNoSesionVencida()
    {
        var (repo, http) = await CreateAsync();
        http.Respond(HttpStatusCode.Forbidden, """{"message":"permission denied for table vehiculos"}""");
        var ex = await Assert.ThrowsAsync<ServerUnavailableException>(() => repo.GetAllAsync(default));
        Assert.Contains("permission denied", ex.Message);
    }

    [Fact]
    public async Task InsertManyMigrated_UnSoloPostConArreglo()
    {
        var (repo, http) = await CreateAsync();
        http.Respond(HttpStatusCode.Created, $"[{Row},{Row.Replace("\"id\":5", "\"id\":6")}]");
        var a = Sample();
        var b = Sample();
        b.Modelo = "Ranger";

        var count = await repo.InsertManyMigratedAsync([a, b], default);

        Assert.Equal(2, count);
        var req = Assert.Single(http.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.StartsWith("[", req.Body!.TrimStart());
        Assert.Contains("\"creado_en\"", req.Body);
        Assert.Contains("Ranger", req.Body);
    }
}
