using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using GeneradorQr.Core;
using GeneradorQr.Core.Formatting;
using GeneradorQr.Core.Models;

namespace GeneradorQr.App.ViewModels;

/// <summary>Estado del formulario de captura/edición.</summary>
public sealed partial class VehicleFormViewModel : ObservableObject
{
    /// <summary>Propiedades que son datos del auto (cambian el QR y marcan el formulario como modificado).</summary>
    internal static readonly HashSet<string> FieldNames =
    [
        nameof(Marca), nameof(Modelo), nameof(Anio), nameof(Kilometraje), nameof(PrecioTexto),
        nameof(Procedencia), nameof(MotorLitros), nameof(ConfiguracionMotor), nameof(Observaciones)
    ];

    private readonly TimeProvider _clock;
    private bool _loading;

    public VehicleFormViewModel(TimeProvider clock)
    {
        _clock = clock;
        Reset();
    }

    [ObservableProperty] private string _marca = string.Empty;
    [ObservableProperty] private string _modelo = string.Empty;
    [ObservableProperty] private decimal? _anio;
    [ObservableProperty] private decimal? _kilometraje;
    [ObservableProperty] private string _precioTexto = string.Empty;
    [ObservableProperty] private string _procedencia = Brand.Procedencias[0];
    [ObservableProperty] private string _motorLitros = string.Empty;
    [ObservableProperty] private string _configuracionMotor = string.Empty;
    [ObservableProperty] private string _observaciones = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditing), nameof(Title))]
    private VehicleRecord? _editing;

    [ObservableProperty] private bool _isDirty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ErrorMarca), nameof(ErrorModelo), nameof(ErrorAnio), nameof(ErrorPrecio), nameof(ErrorMotor), nameof(ErrorConfiguracion))]
    private IReadOnlyDictionary<string, string> _errors = new Dictionary<string, string>();

    public bool IsEditing => Editing is not null;
    public string Title => Editing is null ? "Nuevo vehículo" : $"Editando: {Editing.Titulo}";

    public string? ErrorMarca => Errors.GetValueOrDefault(nameof(VehicleInput.Marca));
    public string? ErrorModelo => Errors.GetValueOrDefault(nameof(VehicleInput.Modelo));
    public string? ErrorAnio => Errors.GetValueOrDefault(nameof(VehicleInput.Anio));
    public string? ErrorPrecio => Errors.GetValueOrDefault(nameof(VehicleInput.PrecioTexto));
    public string? ErrorMotor => Errors.GetValueOrDefault(nameof(VehicleInput.MotorLitros));
    public string? ErrorConfiguracion => Errors.GetValueOrDefault(nameof(VehicleInput.ConfiguracionMotor));

    public int AnioMinimo => VehicleValidator.MinYear;
    public int AnioMaximo => _clock.GetLocalNow().Year + 2;

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (!_loading && e.PropertyName is { } name && FieldNames.Contains(name))
        {
            IsDirty = true;
        }
    }

    public void Load(VehicleRecord vehicle)
    {
        _loading = true;
        Marca = vehicle.Marca;
        Modelo = vehicle.Modelo;
        Anio = vehicle.Anio;
        Kilometraje = vehicle.Kilometraje;
        PrecioTexto = FormatPrecio(vehicle.Precio);
        Procedencia = vehicle.Procedencia;
        MotorLitros = vehicle.MotorLitros;
        ConfiguracionMotor = vehicle.ConfiguracionMotor;
        Observaciones = vehicle.Observaciones;
        _loading = false;

        Editing = vehicle.Clone();
        Errors = new Dictionary<string, string>();
        IsDirty = false;
    }

    public void Clear() => Reset();

    /// <summary>Actualiza versión/estado del auto en edición sin tocar lo que el usuario está escribiendo.</summary>
    public void UpdateEditingMeta(VehicleRecord fromServer)
    {
        if (Editing?.Id != fromServer.Id) return;
        var meta = Editing.Clone();
        meta.Version = fromServer.Version;
        meta.Vendido = fromServer.Vendido;
        meta.ActualizadoEn = fromServer.ActualizadoEn;
        Editing = meta;
    }

    /// <summary>
    /// Valida y arma el registro (con Id, CreadoEn, Vendido y Version del auto en edición).
    /// Con <paramref name="showErrors"/> en false no marca errores (para la vista previa).
    /// </summary>
    public bool TryBuild(out VehicleRecord vehicle, bool showErrors = true)
    {
        var input = new VehicleInput(Marca, Modelo, (int)(Anio ?? 0), (long)(Kilometraje ?? 0), PrecioTexto,
            Procedencia, MotorLitros, ConfiguracionMotor, Observaciones);
        var errors = VehicleValidator.Validate(input, out var record, AnioMaximo);

        if (showErrors)
        {
            Errors = errors.ToDictionary(e => e.Field, e => e.Message);
        }

        if (record is null)
        {
            vehicle = new VehicleRecord();
            return false;
        }

        record.Id = Editing?.Id ?? 0;
        record.Version = Editing?.Version ?? 0;
        record.Vendido = Editing?.Vendido ?? false;
        record.CreadoEn = Editing?.CreadoEn ?? _clock.GetLocalNow().DateTime;
        record.ActualizadoEn = Editing?.ActualizadoEn;
        vehicle = record;
        return true;
    }

    /// <summary>Al salir del campo: precio con separador de miles y litraje normalizado.</summary>
    public void NormalizeOnBlur(string field)
    {
        if (field == nameof(VehicleInput.PrecioTexto) && VehicleText.TryParsePrecio(PrecioTexto, out var precio))
        {
            var formatted = FormatPrecio(precio);
            if (formatted != PrecioTexto) PrecioTexto = formatted;
        }
        else if (field == nameof(VehicleInput.MotorLitros))
        {
            var normalized = VehicleText.NormalizeMotor(MotorLitros);
            if (normalized != MotorLitros) MotorLitros = normalized;
        }
    }

    private static string FormatPrecio(decimal precio) => precio.ToString("#,0.##", CultureInfo.InvariantCulture);

    private void Reset()
    {
        _loading = true;
        Marca = string.Empty;
        Modelo = string.Empty;
        Anio = _clock.GetLocalNow().Year;
        Kilometraje = 0;
        PrecioTexto = string.Empty;
        Procedencia = Brand.Procedencias[0];
        MotorLitros = string.Empty;
        ConfiguracionMotor = string.Empty;
        Observaciones = string.Empty;
        _loading = false;

        Editing = null;
        Errors = new Dictionary<string, string>();
        IsDirty = false;
    }
}
