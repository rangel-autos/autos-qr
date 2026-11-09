using Avalonia.Controls;

namespace GeneradorQr.App.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        Opened += (_, _) => EmailBox.Focus();
    }
}
