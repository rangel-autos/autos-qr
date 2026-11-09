using Avalonia.Controls;
using Avalonia.Threading;
using GeneradorQr.App.Services;
using GeneradorQr.App.ViewModels;
using GeneradorQr.Cloud;
using GeneradorQr.Core.Backup;
using GeneradorQr.Core.Data;

namespace GeneradorQr.App.Views;

/// <summary>Arma la ventana principal con su ViewModel y la conecta con la sesión.</summary>
internal static class MainWindowFactory
{
    public static Window Create(IVehicleRepository repository, SupabaseAuth auth, Action<Window> onSignedOut)
    {
        var vm = new MainViewModel(
            repository,
            new BackupService(AppPaths.BackupDir, TimeProvider.System),
            TimeProvider.System,
            dispatch: action => Dispatcher.UIThread.Post(action),
            logError: Log.Error);

        var window = new MainWindow(FileActions.CreateDefault()) { DataContext = vm };

        window.Opened += async (_, _) => await vm.InitializeAsync();
        window.Closed += (_, _) => vm.Dispose();

        window.SignOutRequested += async (_, _) =>
        {
            await auth.SignOutAsync(CancellationToken.None);
            onSignedOut(window);
        };

        vm.SessionExpired += async (_, _) =>
        {
            await MessageWindow.ShowAsync(window, "Sesión vencida", "Tu sesión venció. Inicia sesión de nuevo.");
            await auth.SignOutAsync(CancellationToken.None);
            window.AllowCloseWithoutPrompt();
            onSignedOut(window);
        };

        return window;
    }
}
