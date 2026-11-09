using Avalonia;

namespace GeneradorQr.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Moneda y números como en México ($495,000) sin importar el idioma del sistema.
        var mx = System.Globalization.CultureInfo.GetCultureInfo("es-MX");
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = mx;
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = mx;

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex) Log.Error(ex, "Excepción no controlada");
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error(e.Exception, "Tarea con excepción no observada");
            e.SetObserved();
        };

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
