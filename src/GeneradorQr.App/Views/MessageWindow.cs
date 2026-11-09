using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace GeneradorQr.App.Views;

/// <summary>Diálogos simples de aviso y confirmación (Avalonia no trae MessageBox).</summary>
internal static class MessageWindow
{
    public static Task ShowAsync(Window? owner, string title, string message) =>
        ShowCoreAsync(owner, title, message, confirm: null);

    /// <summary>Devuelve true si el usuario pulsa el botón de confirmación.</summary>
    public static async Task<bool> ConfirmAsync(Window owner, string title, string message, string confirm) =>
        await ShowCoreAsync(owner, title, message, confirm);

    private static async Task<bool> ShowCoreAsync(Window? owner, string title, string message, string? confirm)
    {
        var result = false;
        var window = new Window
        {
            Title = title,
            Width = 440,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            ShowInTaskbar = owner is null
        };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        if (confirm is not null)
        {
            var cancel = new Button { Content = "Cancelar", IsCancel = true };
            cancel.Click += (_, _) => window.Close();
            var ok = new Button { Content = confirm, IsDefault = true, Classes = { "primary" } };
            ok.Click += (_, _) =>
            {
                result = true;
                window.Close();
            };
            buttons.Children.Add(cancel);
            buttons.Children.Add(ok);
        }
        else
        {
            var ok = new Button { Content = "Aceptar", IsDefault = true, IsCancel = true, Classes = { "primary" } };
            ok.Click += (_, _) => window.Close();
            buttons.Children.Add(ok);
        }

        window.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 16,
            Children =
            {
                new SelectableTextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                buttons
            }
        };

        if (owner is not null)
        {
            await window.ShowDialog(owner);
        }
        else
        {
            var closed = new TaskCompletionSource();
            window.Closed += (_, _) => closed.TrySetResult();
            window.Show();
            await closed.Task;
        }

        return result;
    }
}
