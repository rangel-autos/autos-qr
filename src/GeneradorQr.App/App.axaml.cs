using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using GeneradorQr.App.Services;
using GeneradorQr.App.ViewModels;
using GeneradorQr.App.Views;
using GeneradorQr.Cloud;
using GeneradorQr.Core.Data;

namespace GeneradorQr.App;

public sealed class App : Application
{
    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private SupabaseOptions? _options;
    private SupabaseAuth? _auth;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _desktop = desktop;
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            Dispatcher.UIThread.UnhandledException += (_, e) =>
            {
                Log.Error(e.Exception, "Error en la interfaz");
                e.Handled = true;
                _ = MessageWindow.ShowAsync(desktop.MainWindow, "Error",
                    $"Ocurrió un error inesperado:\n{e.Exception.Message}\n\nSe guardó el detalle en:\n{AppPaths.LogFile}");
            };

            _ = StartSafelyAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// El arranque corre sin que nadie lo espere: si falla algo inesperado, se registra y se avisa,
    /// en vez de dejar un proceso vivo sin ninguna ventana.
    /// </summary>
    private async Task StartSafelyAsync()
    {
        try
        {
            await StartAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Arranque");
            await MessageWindow.ShowAsync(null, "Error al iniciar",
                $"No se pudo abrir la aplicación:\n{ex.Message}\n\nSe guardó el detalle en:\n{AppPaths.LogFile}");

            if (_auth is not null && _desktop?.MainWindow is null)
            {
                ShowLogin();
            }
            else if (_desktop?.MainWindow is null)
            {
                _desktop?.Shutdown(1);
            }
        }
    }

    private async Task StartAsync()
    {
        if (DemoVehicleRepository.IsEnabled)
        {
            // Modo demo: inventario en memoria, sin Supabase ni login.
            _options = new SupabaseOptions("https://demo.invalid", "demo");
            _auth = new SupabaseAuth(_http, _options, new DemoSessionStore(), TimeProvider.System);
            ShowMain(new DemoVehicleRepository());
            return;
        }

        var configPath = AppPaths.ConfigFile;
        try
        {
            _options = SupabaseOptions.Load(configPath);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.Text.Json.JsonException)
        {
            Log.Error(ex, "Configuración");
            await MessageWindow.ShowAsync(null, "Falta la configuración",
                $"No se encontró una configuración válida de Supabase.\n\nColoca el archivo supabase.json en:\n{configPath}");
            _desktop!.Shutdown(1);
            return;
        }

        _auth = new SupabaseAuth(_http, _options, new FileSessionStore(AppPaths.SessionFile), TimeProvider.System);

        bool restored;
        try
        {
            restored = await _auth.TryRestoreAsync(CancellationToken.None);
        }
        catch (Exception ex) when (ex is OfflineException or ServerUnavailableException)
        {
            // Hay sesión guardada pero no hay red: se abre la pantalla principal, que avisa y reintenta.
            restored = _auth.IsSignedIn;
        }

        if (restored)
        {
            ShowMain();
        }
        else
        {
            ShowLogin();
        }
    }

    private void ShowLogin()
    {
        var vm = new LoginViewModel(_auth!);
        var window = new LoginWindow { DataContext = vm };
        vm.SignedIn += (_, _) =>
        {
            ShowMain();
            window.Close();
        };
        window.Closed += (_, _) =>
        {
            if (_desktop!.MainWindow == window) _desktop.Shutdown();
        };
        SetMainWindow(window);
    }

    private void ShowMain(IVehicleRepository? repository = null)
    {
        repository ??= new SupabaseVehicleRepository(_http, _options!, _auth!);
        var window = MainWindowFactory.Create(repository, _auth!, OnSignedOut);
        window.Closed += (_, _) =>
        {
            if (_desktop!.MainWindow == window) _desktop.Shutdown();
        };
        SetMainWindow(window);
    }

    private void OnSignedOut(Window mainWindow)
    {
        ShowLogin();
        mainWindow.Close();
    }

    private void SetMainWindow(Window window)
    {
        _desktop!.MainWindow = window;
        window.Show();
    }
}

internal sealed class DemoSessionStore : ISessionStore
{
    public string? Load() => null;
    public void Save(string refreshToken) { }
    public void Clear() { }
}
