using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GeneradorQr.Core;
using GeneradorQr.Core.Backup;
using GeneradorQr.Core.Data;
using GeneradorQr.Core.Formatting;
using GeneradorQr.Core.Inventory;
using GeneradorQr.Core.Models;
using GeneradorQr.Core.Rendering;
using SkiaSharp;

namespace GeneradorQr.App.ViewModels;

/// <summary>
/// Lógica de la pantalla principal, sin dependencias de Avalonia.
/// <paramref name="dispatch"/> lleva al hilo de la interfaz lo que disparan los temporizadores.
/// </summary>
public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan PreviewDelay = TimeSpan.FromMilliseconds(300);

    private readonly IVehicleRepository _repository;
    private readonly BackupService _backup;
    private readonly TimeProvider _clock;
    private readonly Action<Action> _dispatch;
    private readonly Action<Exception, string> _logError;
    private readonly ConnectionMonitor _monitor;
    private readonly ITimer _refreshTimer;
    private readonly SKBitmap _logo;
    private readonly CancellationTokenSource _cts = new();
    private ITimer? _previewTimer;
    private List<VehicleRecord> _all = [];
    private bool _refreshing;

    public MainViewModel(
        IVehicleRepository repository,
        BackupService backup,
        TimeProvider clock,
        Action<Action>? dispatch = null,
        Action<Exception, string>? logError = null)
    {
        _repository = repository;
        _backup = backup;
        _clock = clock;
        _dispatch = dispatch ?? (a => a());
        _logError = logError ?? ((_, _) => { });
        _monitor = new ConnectionMonitor(clock);
        _logo = BrandAssets.LoadLogo();

        Form = new VehicleFormViewModel(clock);
        Form.PropertyChanged += OnFormChanged;

        _refreshTimer = clock.CreateTimer(_ => _dispatch(() => _ = RefreshCoreAsync()), null, RefreshInterval, RefreshInterval);
    }

    public VehicleFormViewModel Form { get; }

    public ObservableCollection<VehicleRecord> Visible { get; } = [];

    public IReadOnlyList<VehicleRecord> All => _all;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _includeSold;
    [ObservableProperty] private InventoryStats _stats = new(0, 0, 0);
    [ObservableProperty] private IReadOnlyList<string> _marcaSuggestions = Brand.MarcasComunes;
    [ObservableProperty] private IReadOnlyList<string> _modeloSuggestions = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(ToggleVendidoCommand), nameof(DeleteCommand))]
    private bool _isOnline = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isBusy;

    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _statusIsError;
    [ObservableProperty] private byte[]? _previewPng;

    /// <summary>Reintento en curso tras perder la conexión (expuesto para pruebas).</summary>
    internal Task? ReconnectTask { get; private set; }

    public event EventHandler? SessionExpired;

    public async Task InitializeAsync()
    {
        if (await TryRunAsync(LoadAllAsync))
        {
            try
            {
                await _backup.WriteAsync(_all);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logError(ex, "Respaldo local");
            }
        }
    }

    public Task OnWindowActivatedAsync() => RefreshCoreAsync();

    /// <summary>Carga un auto del inventario en el formulario (la vista confirma antes si hay cambios sin guardar).</summary>
    public void Edit(VehicleRecord vehicle)
    {
        Form.Load(vehicle);
        SetStatus($"Editando {vehicle.Titulo}.", isError: false);
        UpdatePreview();
    }

    /// <summary>Auto listo para exportar (ficha o QR); null si el formulario no es válido.</summary>
    public VehicleRecord? CurrentVehicle() => Form.TryBuild(out var v, showErrors: true) ? v : null;

    [RelayCommand]
    private void New()
    {
        Form.Clear();
        SetStatus("Captura un vehículo nuevo.", isError: false);
        UpdatePreview();
    }

    [RelayCommand]
    private Task RefreshAsync() => RefreshCoreAsync();

    private bool CanSave() => IsOnline && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        if (!Form.TryBuild(out var vehicle))
        {
            SetStatus("Revisa los campos marcados.", isError: true);
            return;
        }

        IsBusy = true;
        try
        {
            await TryRunAsync(async () =>
            {
                if (vehicle.Id == 0)
                {
                    var saved = await _repository.InsertAsync(vehicle, _cts.Token);
                    _all.Add(saved);
                    Form.Clear();
                    SetStatus($"Guardado: {saved.Titulo}.", isError: false);
                }
                else
                {
                    var saved = await _repository.UpdateAsync(vehicle, _cts.Token);
                    Replace(saved);
                    Form.Load(saved);
                    SetStatus("Cambios guardados.", isError: false);
                }

                ApplyFilter();
                UpdatePreview();
            });
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanModify(VehicleRecord? vehicle) => IsOnline && vehicle is not null;

    [RelayCommand(CanExecute = nameof(CanModify))]
    private async Task ToggleVendidoAsync(VehicleRecord? vehicle)
    {
        if (vehicle is null) return;
        await TryRunAsync(async () =>
        {
            var saved = await _repository.SetVendidoAsync(vehicle.Id, vehicle.Version, !vehicle.Vendido, _cts.Token);
            Replace(saved);
            Form.UpdateEditingMeta(saved);
            ApplyFilter();
            SetStatus(saved.Vendido ? $"{saved.Titulo} marcado como vendido." : $"{saved.Titulo} marcado como disponible.", isError: false);
        });
    }

    [RelayCommand(CanExecute = nameof(CanModify))]
    private async Task DeleteAsync(VehicleRecord? vehicle)
    {
        if (vehicle is null) return;
        await TryRunAsync(async () =>
        {
            await _repository.DeleteAsync(vehicle.Id, vehicle.Version, _cts.Token);
            _all.RemoveAll(v => v.Id == vehicle.Id);
            if (Form.Editing?.Id == vehicle.Id)
            {
                Form.Clear();
                UpdatePreview();
            }

            ApplyFilter();
            SetStatus($"{vehicle.Titulo} eliminado.", isError: false);
        });
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnIncludeSoldChanged(bool value) => ApplyFilter();

    private async Task RefreshCoreAsync()
    {
        if (_refreshing || !IsOnline) return;
        _refreshing = true;
        try
        {
            await TryRunAsync(LoadAllAsync);
        }
        finally
        {
            _refreshing = false;
        }
    }

    private async Task LoadAllAsync()
    {
        _all = (await _repository.GetAllAsync(_cts.Token)).ToList();

        // Si se está editando un auto sin cambios, se muestra lo más reciente del servidor.
        if (Form.Editing is { } editing)
        {
            var current = _all.FirstOrDefault(v => v.Id == editing.Id);
            if (current is null)
            {
                if (!Form.IsDirty) Form.Clear();
            }
            else if (!Form.IsDirty && current.Version != editing.Version)
            {
                Form.Load(current);
                UpdatePreview();
            }
        }

        ApplyFilter();
        MarcaSuggestions = Brand.MarcasComunes.Concat(InventoryQuery.DistinctMarcas(_all))
            .Distinct(StringComparer.CurrentCultureIgnoreCase).Order(StringComparer.CurrentCultureIgnoreCase).ToList();
        ModeloSuggestions = InventoryQuery.DistinctModelos(_all);
    }

    /// <summary>Ejecuta una operación con el manejo común de errores. Devuelve true si terminó bien.</summary>
    private async Task<bool> TryRunAsync(Func<Task> operation)
    {
        try
        {
            await operation();
            return true;
        }
        catch (OfflineException)
        {
            GoOffline();
        }
        catch (ConcurrencyConflictException)
        {
            await ReloadAfterConflictAsync();
        }
        catch (AuthRequiredException)
        {
            SessionExpired?.Invoke(this, EventArgs.Empty);
        }
        catch (ServerUnavailableException ex)
        {
            _logError(ex, "Servidor");
            SetStatus(ex.Message == new ServerUnavailableException().Message ? Messages.ServerHint : ex.Message, isError: true);
        }

        return false;
    }

    private async Task ReloadAfterConflictAsync()
    {
        var editingId = Form.Editing?.Id;
        try
        {
            _all = (await _repository.GetAllAsync(_cts.Token)).ToList();
        }
        catch (OfflineException)
        {
            GoOffline();
            return;
        }

        if (editingId is { } id)
        {
            var current = _all.FirstOrDefault(v => v.Id == id);
            if (current is null) Form.Clear();
            else Form.Load(current);
            UpdatePreview();
        }

        ApplyFilter();
        SetStatus(Messages.Conflict, isError: true);
    }

    private void GoOffline()
    {
        IsOnline = false;
        _monitor.MarkOffline();
        SetStatus(Messages.Offline, isError: true);

        if (ReconnectTask is { IsCompleted: false }) return;
        ReconnectTask = ReconnectAsync();
    }

    private async Task ReconnectAsync()
    {
        try
        {
            await _monitor.RunUntilOnlineAsync(LoadAllAsync, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        _dispatch(() =>
        {
            IsOnline = true;
            SetStatus("Conexión restablecida.", isError: false);
        });
    }

    private void Replace(VehicleRecord saved)
    {
        var index = _all.FindIndex(v => v.Id == saved.Id);
        if (index >= 0) _all[index] = saved;
        else _all.Add(saved);
    }

    private void ApplyFilter()
    {
        var result = InventoryQuery.Search(_all, SearchText, IncludeSold);
        Visible.Clear();
        foreach (var v in result) Visible.Add(v);
        Stats = InventoryQuery.Stats(_all);
    }

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        StatusIsError = isError;
    }

    private void OnFormChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not { } name || !VehicleFormViewModel.FieldNames.Contains(name)) return;

        _previewTimer?.Dispose();
        _previewTimer = _clock.CreateTimer(_ => _dispatch(UpdatePreview), null, PreviewDelay, Timeout.InfiniteTimeSpan);
    }

    private void UpdatePreview()
    {
        if (!Form.TryBuild(out var vehicle, showErrors: false))
        {
            PreviewPng = null;
            return;
        }

        using var qr = QrRenderer.Render(VehicleText.BuildPayload(vehicle), pixelsPerModule: 8, logo: _logo);
        PreviewPng = QrRenderer.EncodePng(qr);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _refreshTimer.Dispose();
        _previewTimer?.Dispose();
        _logo.Dispose();
        _cts.Dispose();
    }
}
