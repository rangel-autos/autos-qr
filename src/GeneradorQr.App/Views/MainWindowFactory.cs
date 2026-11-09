using Avalonia.Controls;
using Avalonia.Layout;
using GeneradorQr.Cloud;
using GeneradorQr.Core.Data;

namespace GeneradorQr.App.Views;

/// <summary>
/// Crea la ventana principal. Provisional (Task 8): solo confirma la sesión; la pantalla completa llega en Task 10.
/// </summary>
internal static class MainWindowFactory
{
    public static Window Create(IVehicleRepository repository, SupabaseAuth auth, Action<Window> onSignedOut)
    {
        var window = new Window { Title = "Generador QR", Width = 480, Height = 240, WindowStartupLocation = WindowStartupLocation.CenterScreen };
        var signOut = new Button { Content = "Cerrar sesión", HorizontalAlignment = HorizontalAlignment.Center };
        signOut.Click += async (_, _) =>
        {
            await auth.SignOutAsync(CancellationToken.None);
            onSignedOut(window);
        };
        window.Content = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 12,
            Children = { new TextBlock { Text = "Sesión iniciada.", HorizontalAlignment = HorizontalAlignment.Center }, signOut }
        };
        return window;
    }
}
