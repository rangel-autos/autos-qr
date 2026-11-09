using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GeneradorQr.Cloud;
using GeneradorQr.Core.Data;

namespace GeneradorQr.App.ViewModels;

public sealed partial class LoginViewModel(SupabaseAuth auth) : ObservableObject
{
    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string? _error;

    [ObservableProperty]
    private bool _isBusy;

    public event EventHandler? SignedIn;

    [RelayCommand]
    private async Task SignInAsync()
    {
        var email = Email.Trim();
        if (email.Length == 0 || Password.Length == 0)
        {
            Error = Messages.EmptyLogin;
            return;
        }

        IsBusy = true;
        Error = null;
        try
        {
            await auth.SignInAsync(email, Password, CancellationToken.None);
            Password = string.Empty;
            SignedIn?.Invoke(this, EventArgs.Empty);
        }
        catch (InvalidCredentialsException)
        {
            Error = Messages.InvalidCredentials;
        }
        catch (OfflineException)
        {
            Error = Messages.OfflineLogin;
        }
        catch (ServerUnavailableException)
        {
            Error = Messages.ServerHint;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
