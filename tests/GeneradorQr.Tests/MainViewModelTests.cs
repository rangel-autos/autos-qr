using GeneradorQr.App.ViewModels;
using GeneradorQr.Core.Backup;
using GeneradorQr.Core.Data;
using GeneradorQr.Core.Models;
using GeneradorQr.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace GeneradorQr.Tests;

public sealed class MainViewModelTests : IDisposable
{
    private readonly string _backupDir = Path.Combine(Path.GetTempPath(), "gqr-main-" + Guid.NewGuid().ToString("N"));
    private readonly FakeRepository _repo = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 8, 20, 0, 0, TimeSpan.Zero));

    public void Dispose()
    {
        if (Directory.Exists(_backupDir)) Directory.Delete(_backupDir, recursive: true);
    }

    private MainViewModel Create() => new(_repo, new BackupService(_backupDir, _clock), _clock);

    private static VehicleRecord Lobo() => new()
    {
        Marca = "Ford", Modelo = "Lobo", Anio = 2020, Kilometraje = 45000, Precio = 495000m,
        Procedencia = "Nacional", MotorLitros = "5.0 L", ConfiguracionMotor = "V8", CreadoEn = DateTime.Now
    };

    private static void FillValid(VehicleFormViewModel form)
    {
        form.Marca = "Nissan";
        form.Modelo = "Versa";
        form.Anio = 2021;
        form.Kilometraje = 10000;
        form.PrecioTexto = "250000";
        form.Procedencia = "Nacional";
        form.MotorLitros = "1.6 L";
        form.ConfiguracionMotor = "4 cilindros";
    }

    [Fact]
    public async Task Initialize_CargaInventarioYEscribeRespaldo()
    {
        _repo.Seed(Lobo());
        var vm = Create();

        await vm.InitializeAsync();

        Assert.Single(vm.Visible);
        Assert.Equal(1, vm.Stats.Disponibles);
        Assert.True(vm.IsOnline);
        Assert.Single(Directory.GetFiles(_backupDir));
    }

    [Fact]
    public async Task Guardar_Nuevo_InsertaYLimpiaFormulario()
    {
        var vm = Create();
        await vm.InitializeAsync();
        FillValid(vm.Form);

        await vm.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(_repo.Rows);
        Assert.Equal("Versa", saved.Modelo);
        Assert.Equal(250000m, saved.Precio);
        Assert.Single(vm.Visible);
        Assert.Equal(string.Empty, vm.Form.Marca);
        Assert.Null(vm.Form.Editing);
        Assert.False(vm.Form.IsDirty);
    }

    [Fact]
    public async Task Guardar_Invalido_MarcaErroresYNoLlamaRepo()
    {
        var vm = Create();
        await vm.InitializeAsync();
        vm.Form.Marca = "Ford";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(_repo.Rows);
        Assert.Equal("Escribe el modelo.", vm.Form.Errors[nameof(VehicleInput.Modelo)]);
        Assert.Equal("Revisa los campos marcados.", vm.StatusMessage);
    }

    [Fact]
    public async Task Guardar_Edicion_ActualizaConVersion()
    {
        var seeded = _repo.Seed(Lobo());
        var vm = Create();
        await vm.InitializeAsync();
        vm.Edit(vm.Visible[0]);
        vm.Form.PrecioTexto = "480000";

        await vm.SaveCommand.ExecuteAsync(null);

        var row = Assert.Single(_repo.Rows);
        Assert.Equal(480000m, row.Precio);
        Assert.Equal(seeded.Version + 1, row.Version);
        Assert.Equal(row.Version, vm.Form.Editing!.Version);
        Assert.Equal("Cambios guardados.", vm.StatusMessage);
    }

    [Fact]
    public async Task Guardar_SinRed_ConservaFormulario()
    {
        var vm = Create();
        await vm.InitializeAsync();
        FillValid(vm.Form);
        _repo.Offline = true;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.False(vm.IsOnline);
        Assert.Equal("Sin conexión — reintentando…", vm.StatusMessage);
        Assert.Equal("Nissan", vm.Form.Marca);
        Assert.True(vm.Form.IsDirty);
        Assert.False(vm.SaveCommand.CanExecute(null));

        _repo.Offline = false;
        _clock.Advance(TimeSpan.FromSeconds(5));
        await vm.ReconnectTask!;

        Assert.True(vm.IsOnline);
        Assert.True(vm.SaveCommand.CanExecute(null));
        Assert.Equal("Nissan", vm.Form.Marca);
    }

    [Fact]
    public async Task Guardar_Conflicto_RecargaYAvisa()
    {
        var seeded = _repo.Seed(Lobo());
        var vm = Create();
        await vm.InitializeAsync();
        vm.Edit(vm.Visible[0]);
        vm.Form.PrecioTexto = "480000";
        _repo.TouchFromOtherDevice(seeded.Id, r => r.Precio = 470000m);

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Otro equipo modificó este auto. Se recargaron los datos más recientes.", vm.StatusMessage);
        Assert.Equal(470000m, _repo.Rows[0].Precio);
        Assert.Equal("470,000", vm.Form.PrecioTexto);
        Assert.Equal(_repo.Rows[0].Version, vm.Form.Editing!.Version);
        Assert.False(vm.Form.IsDirty);
    }

    [Fact]
    public async Task AuthRequired_DisparaEventoSesionVencida()
    {
        var vm = Create();
        var fired = false;
        vm.SessionExpired += (_, _) => fired = true;
        _repo.FailNext = new AuthRequiredException();

        await vm.InitializeAsync();

        Assert.True(fired);
    }

    [Fact]
    public async Task ServidorCaido_MuestraMensajeServidor()
    {
        var vm = Create();
        _repo.FailNext = new ServerUnavailableException();

        await vm.InitializeAsync();

        Assert.StartsWith("No se pudo conectar con el servidor.", vm.StatusMessage);
        Assert.Contains("Supabase", vm.StatusMessage);
        Assert.True(vm.StatusIsError);
    }

    [Fact]
    public async Task Busqueda_FiltraEnMemoria_SinLlamarRepo()
    {
        _repo.Seed(Lobo());
        var nissan = Lobo();
        nissan.Marca = "Nissan";
        nissan.Modelo = "Versa";
        _repo.Seed(nissan);
        var vm = Create();
        await vm.InitializeAsync();
        var calls = _repo.GetAllCalls;

        vm.SearchText = "versa";

        Assert.Equal(["Versa"], vm.Visible.Select(v => v.Modelo));
        Assert.Equal(calls, _repo.GetAllCalls);
    }

    [Fact]
    public async Task Busqueda_IncluirVendidos()
    {
        var vendido = Lobo();
        vendido.Vendido = true;
        _repo.Seed(vendido);
        var vm = Create();
        await vm.InitializeAsync();

        Assert.Empty(vm.Visible);
        vm.IncludeSold = true;
        Assert.Single(vm.Visible);
    }

    [Fact]
    public async Task Recarga_Cada60s_ConFakeTimeProvider()
    {
        var vm = Create();
        await vm.InitializeAsync();
        var calls = _repo.GetAllCalls;
        _repo.Seed(Lobo());

        _clock.Advance(TimeSpan.FromSeconds(60));

        Assert.Equal(calls + 1, _repo.GetAllCalls);
        Assert.Single(vm.Visible);
    }

    [Fact]
    public async Task OnWindowActivated_Recarga()
    {
        var vm = Create();
        await vm.InitializeAsync();
        _repo.Seed(Lobo());

        await vm.OnWindowActivatedAsync();

        Assert.Single(vm.Visible);
    }

    [Fact]
    public async Task Recarga_EditandoSinCambios_ActualizaVersionDelFormulario()
    {
        var seeded = _repo.Seed(Lobo());
        var vm = Create();
        await vm.InitializeAsync();
        vm.Edit(vm.Visible[0]);
        _repo.TouchFromOtherDevice(seeded.Id, r => r.Kilometraje = 46000);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(46000m, vm.Form.Kilometraje);
        Assert.Equal(_repo.Rows[0].Version, vm.Form.Editing!.Version);
    }

    [Fact]
    public async Task Preview_SeActualizaConFormularioValido_ConDebounce300ms()
    {
        var vm = Create();
        await vm.InitializeAsync();

        FillValid(vm.Form);
        Assert.Null(vm.PreviewPng);

        _clock.Advance(TimeSpan.FromMilliseconds(299));
        Assert.Null(vm.PreviewPng);

        _clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.NotNull(vm.PreviewPng);

        vm.Form.Marca = "";
        _clock.Advance(TimeSpan.FromMilliseconds(300));
        Assert.Null(vm.PreviewPng);
    }

    [Fact]
    public async Task ToggleVendido_CambiaEstadoYResumen()
    {
        _repo.Seed(Lobo());
        var vm = Create();
        await vm.InitializeAsync();

        await vm.ToggleVendidoCommand.ExecuteAsync(vm.Visible[0]);

        Assert.True(_repo.Rows[0].Vendido);
        Assert.Equal(0, vm.Stats.Disponibles);
        Assert.Equal(1, vm.Stats.Vendidos);
    }

    [Fact]
    public async Task Delete_Conflicto_RecargaYAvisa()
    {
        var seeded = _repo.Seed(Lobo());
        var vm = Create();
        await vm.InitializeAsync();
        var stale = vm.Visible[0];
        _repo.TouchFromOtherDevice(seeded.Id, r => r.Precio = 1m);

        await vm.DeleteCommand.ExecuteAsync(stale);

        Assert.Single(_repo.Rows);
        Assert.Equal("Otro equipo modificó este auto. Se recargaron los datos más recientes.", vm.StatusMessage);
        Assert.Equal(1m, vm.Visible[0].Precio);
    }

    [Fact]
    public async Task Delete_DelAutoEnEdicion_LimpiaFormulario()
    {
        _repo.Seed(Lobo());
        var vm = Create();
        await vm.InitializeAsync();
        vm.Edit(vm.Visible[0]);

        await vm.DeleteCommand.ExecuteAsync(vm.Visible[0]);

        Assert.Empty(_repo.Rows);
        Assert.Empty(vm.Visible);
        Assert.Null(vm.Form.Editing);
    }

    [Fact]
    public void Form_NormalizaPrecioYMotorAlSalir()
    {
        var vm = Create();
        vm.Form.PrecioTexto = "495000";
        vm.Form.MotorLitros = "5";

        vm.Form.NormalizeOnBlur(nameof(VehicleInput.PrecioTexto));
        vm.Form.NormalizeOnBlur(nameof(VehicleInput.MotorLitros));

        Assert.Equal("495,000", vm.Form.PrecioTexto);
        Assert.Equal("5.0 L", vm.Form.MotorLitros);
    }

    [Fact]
    public async Task Reconexion_AuthRequired_DisparaSesionVencida()
    {
        var vm = Create();
        await vm.InitializeAsync();
        var fired = 0;
        vm.SessionExpired += (_, _) => fired++;
        _repo.Offline = true;
        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.SaveCommand.ExecuteAsync(null);
        FillValid(vm.Form);
        await vm.SaveCommand.ExecuteAsync(null);

        _repo.Offline = false;
        _repo.FailNext = new AuthRequiredException();
        _clock.Advance(TimeSpan.FromSeconds(5));
        await vm.ReconnectTask!;

        Assert.Equal(1, fired);
    }

    [Fact]
    public async Task Reconexion_ErrorInesperado_SigueReintentando()
    {
        var vm = Create();
        await vm.InitializeAsync();
        FillValid(vm.Form);
        _repo.Offline = true;
        await vm.SaveCommand.ExecuteAsync(null);

        _repo.Offline = false;
        _repo.FailNext = new InvalidOperationException("respuesta rara");
        _clock.Advance(TimeSpan.FromSeconds(5));
        _clock.Advance(TimeSpan.FromSeconds(5));
        await vm.ReconnectTask!;

        Assert.True(vm.IsOnline);
    }

    [Fact]
    public async Task SesionVencida_SoloSeAvisaUnaVez()
    {
        var vm = Create();
        await vm.InitializeAsync();
        var fired = 0;
        vm.SessionExpired += (_, _) => fired++;

        _repo.FailNext = new AuthRequiredException();
        await vm.RefreshCommand.ExecuteAsync(null);
        _repo.FailNext = new AuthRequiredException();
        _clock.Advance(TimeSpan.FromSeconds(60));
        _repo.FailNext = new AuthRequiredException();
        await vm.OnWindowActivatedAsync();

        Assert.Equal(1, fired);
    }

    [Fact]
    public async Task ConflictoEnOtroAuto_NoBorraLoQueSeEstaEscribiendo()
    {
        var a = _repo.Seed(Lobo());
        var otro = Lobo();
        otro.Modelo = "Ranger";
        var b = _repo.Seed(otro);
        var vm = Create();
        await vm.InitializeAsync();
        vm.Edit(vm.All.Single(v => v.Id == a.Id));
        vm.Form.PrecioTexto = "123456";
        var staleB = vm.All.Single(v => v.Id == b.Id);
        _repo.TouchFromOtherDevice(b.Id, r => r.Precio = 1m);

        await vm.ToggleVendidoCommand.ExecuteAsync(staleB);

        Assert.Equal("123456", vm.Form.PrecioTexto);
        Assert.True(vm.Form.IsDirty);
        Assert.Equal(a.Id, vm.Form.Editing!.Id);
        Assert.Equal(1m, vm.All.Single(v => v.Id == b.Id).Precio);
    }
}
