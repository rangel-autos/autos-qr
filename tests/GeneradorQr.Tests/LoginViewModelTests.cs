using System.Net;
using GeneradorQr.App.ViewModels;
using GeneradorQr.Cloud;
using GeneradorQr.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace GeneradorQr.Tests;

public class LoginViewModelTests
{
    private static (LoginViewModel Vm, FakeHttpHandler Http) Create()
    {
        var http = new FakeHttpHandler();
        var auth = new SupabaseAuth(http.Client(), SupabaseAuthTests.Options, new SupabaseAuthTests.MemoryStore(), new FakeTimeProvider());
        return (new LoginViewModel(auth), http);
    }

    [Fact]
    public async Task SignIn_CredencialesMalas_MuestraMensaje()
    {
        var (vm, http) = Create();
        http.Respond(HttpStatusCode.BadRequest, """{"error":"invalid_grant"}""");
        vm.Email = "agencia@rangel.mx";
        vm.Password = "mala";

        await vm.SignInCommand.ExecuteAsync(null);

        Assert.Equal("Correo o contraseña incorrectos.", vm.Error);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task SignIn_SinRed_MuestraSinConexion()
    {
        var (vm, http) = Create();
        http.Throw(new HttpRequestException("sin red"));
        vm.Email = "agencia@rangel.mx";
        vm.Password = "x";

        await vm.SignInCommand.ExecuteAsync(null);

        Assert.Equal("Sin conexión. Revisa el internet e intenta de nuevo.", vm.Error);
    }

    [Fact]
    public async Task SignIn_ServidorCaido_MuestraMensajeServidor()
    {
        var (vm, http) = Create();
        http.Respond(HttpStatusCode.ServiceUnavailable);
        vm.Email = "agencia@rangel.mx";
        vm.Password = "x";

        await vm.SignInCommand.ExecuteAsync(null);

        Assert.StartsWith("No se pudo conectar con el servidor.", vm.Error);
    }

    [Fact]
    public async Task SignIn_Ok_DisparaSignedIn()
    {
        var (vm, http) = Create();
        http.Respond(HttpStatusCode.OK, SupabaseAuthTests.TokenJson("a", "r"));
        var fired = false;
        vm.SignedIn += (_, _) => fired = true;
        vm.Email = " agencia@rangel.mx ";
        vm.Password = "x";

        await vm.SignInCommand.ExecuteAsync(null);

        Assert.True(fired);
        Assert.Null(vm.Error);
        Assert.Equal(string.Empty, vm.Password);
        Assert.Contains("\"email\":\"agencia@rangel.mx\"", http.Requests[0].Body);
    }

    [Fact]
    public async Task SignIn_CamposVacios_NoLlamaServidor()
    {
        var (vm, http) = Create();
        vm.Email = "";
        vm.Password = "";

        await vm.SignInCommand.ExecuteAsync(null);

        Assert.Equal("Escribe el correo y la contraseña.", vm.Error);
        Assert.Empty(http.Requests);
    }
}
