using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using GeneradorQr.App.Services;
using GeneradorQr.App.ViewModels;
using GeneradorQr.Core.Models;

namespace GeneradorQr.App.Views;

public partial class MainWindow : Window
{
    private readonly IFileActions _files;
    private bool _closeConfirmed;

    // Requerido por el diseñador de Avalonia.
    public MainWindow() : this(FileActions.CreateDefault())
    {
    }

    public MainWindow(IFileActions files)
    {
        _files = files;
        InitializeComponent();

        PrecioBox.LostFocus += (_, _) => Vm?.Form.NormalizeOnBlur(nameof(VehicleInput.PrecioTexto));
        MotorBox.LostFocus += (_, _) => Vm?.Form.NormalizeOnBlur(nameof(VehicleInput.MotorLitros));

        PdfButton.Click += async (_, _) => await ExportAsync(v => _files.OpenPdfAsync(v), "Ficha abierta en el visor de PDF. Desde ahí puedes imprimir o compartir.");
        PngButton.Click += async (_, _) => await ExportSavedAsync(v => _files.SavePngAsync(v, this));
        SaveQrButton.Click += async (_, _) => await ExportSavedAsync(v => _files.SaveQrAsync(v, this));
        CopyQrButton.Click += async (_, _) => await ExportAsync(v => _files.CopyQrAsync(v, this), "QR copiado. Pégalo en WhatsApp, Word, etc.");
        NewButton.Click += async (_, _) => await NewAsync();

        Grid.DoubleTapped += async (_, _) => await EditSelectedAsync();
        Grid.KeyDown += OnGridKeyDown;
        Grid.LoadingRow += (_, e) => e.Row.Classes.Set("sold", e.Row.DataContext is VehicleRecord { Vendido: true });
        EditItem.Click += async (_, _) => await EditSelectedAsync();
        ToggleSoldItem.Click += async (_, _) =>
        {
            if (Selected is { } v && Vm is { } vm) await vm.ToggleVendidoCommand.ExecuteAsync(v);
        };
        DeleteItem.Click += async (_, _) => await DeleteSelectedAsync();

        Activated += async (_, _) =>
        {
            if (Vm is { } vm) await vm.OnWindowActivatedAsync();
        };
        Closing += OnClosing;
        Opened += (_, _) =>
        {
            SetupShortcuts();
            MarcaBox.Focus();
        };
    }

    /// <summary>Lo dispara "Cerrar sesión" en el menú.</summary>
    public event EventHandler? SignOutRequested;

    private MainViewModel? Vm => DataContext as MainViewModel;

    /// <summary>El próximo cierre no pregunta por cambios sin guardar (por ejemplo, cuando venció la sesión).</summary>
    public void AllowCloseWithoutPrompt() => _closeConfirmed = true;

    private VehicleRecord? Selected => Grid.SelectedItem as VehicleRecord;

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        SignOutItem.Click += async (_, _) =>
        {
            if (await ConfirmDiscardAsync())
            {
                _closeConfirmed = true;
                SignOutRequested?.Invoke(this, EventArgs.Empty);
            }
        };
    }

    private void SetupShortcuts()
    {
        var mod = PlatformSettings?.HotkeyConfiguration.CommandModifiers ?? KeyModifiers.Control;
        var key = mod == KeyModifiers.Meta ? "⌘" : "Ctrl+";

        void Bind(Key k, KeyModifiers extra, Func<Task> action) =>
            KeyBindings.Add(new KeyBinding
            {
                Gesture = new KeyGesture(k, mod | extra),
                Command = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(action)
            });

        Bind(Key.S, KeyModifiers.None, async () =>
        {
            if (Vm is { } vm && vm.SaveCommand.CanExecute(null)) await vm.SaveCommand.ExecuteAsync(null);
        });
        Bind(Key.N, KeyModifiers.None, NewAsync);
        Bind(Key.P, KeyModifiers.None, () => ExportAsync(v => _files.OpenPdfAsync(v), "Ficha abierta en el visor de PDF."));
        Bind(Key.E, KeyModifiers.None, () => ExportSavedAsync(v => _files.SavePngAsync(v, this)));
        Bind(Key.C, KeyModifiers.Shift, () => ExportAsync(v => _files.CopyQrAsync(v, this), "QR copiado."));
        Bind(Key.F, KeyModifiers.None, () =>
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            return Task.CompletedTask;
        });

        SearchBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && Vm is { } vm && vm.SearchText.Length > 0)
            {
                vm.SearchText = string.Empty;
                e.Handled = true;
            }
        };

        ShortcutsHint.Text = $"{key}S guardar · {key}N nuevo · {key}P ficha PDF · {key}F buscar";
    }

    private async void OnGridKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await EditSelectedAsync();
        }
        else if (e.Key is Key.Delete or Key.Back && e.KeyModifiers == KeyModifiers.None)
        {
            e.Handled = true;
            await DeleteSelectedAsync();
        }
    }

    private async Task EditSelectedAsync()
    {
        if (Selected is not { } v || Vm is not { } vm) return;
        if (vm.Form.Editing?.Id == v.Id && !vm.Form.IsDirty) return;
        if (await ConfirmDiscardAsync()) vm.Edit(v);
    }

    private async Task DeleteSelectedAsync()
    {
        if (Selected is not { } v || Vm is not { } vm) return;
        var ok = await MessageWindow.ConfirmAsync(this, "Eliminar vehículo",
            $"¿Eliminar {v.Titulo} {v.Anio}? Se borrará para los dos equipos y no se puede deshacer.", "Eliminar");
        if (ok) await vm.DeleteCommand.ExecuteAsync(v);
    }

    private async Task NewAsync()
    {
        if (Vm is { } vm && await ConfirmDiscardAsync())
        {
            vm.NewCommand.Execute(null);
            MarcaBox.Focus();
        }
    }

    private async Task<bool> ConfirmDiscardAsync()
    {
        if (Vm is not { Form.IsDirty: true }) return true;
        return await MessageWindow.ConfirmAsync(this, "Cambios sin guardar",
            "Hay cambios sin guardar en el formulario. ¿Descartarlos?", "Descartar");
    }

    private async Task ExportAsync(Func<VehicleRecord, Task> action, string done)
    {
        if (Vm is not { } vm) return;
        var vehicle = vm.CurrentVehicle();
        if (vehicle is null)
        {
            vm.Notify("Completa los campos marcados para generar el QR.", isError: true);
            return;
        }

        try
        {
            await action(vehicle);
            if (done.Length > 0) vm.Notify(done);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException
                                       or System.ComponentModel.Win32Exception)
        {
            Log.Error(ex, "Exportar");
            vm.Notify($"No se pudo completar: {ex.Message}", isError: true);
        }
    }

    private Task ExportSavedAsync(Func<VehicleRecord, Task<string?>> save) => ExportAsync(async v =>
    {
        var path = await save(v);
        if (path is not null) Vm?.Notify($"Guardado en {path}");
    }, done: string.Empty);

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeConfirmed || Vm is not { Form.IsDirty: true }) return;

        e.Cancel = true;
        if (await ConfirmDiscardAsync())
        {
            _closeConfirmed = true;
            Close();
        }
    }
}
