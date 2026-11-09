using System.Diagnostics;
using System.Drawing.Printing;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using GeneradorQrAutos.Data;
using GeneradorQrAutos.Models;
using GeneradorQrAutos.Services;

namespace GeneradorQrAutos;

public partial class Form1 : Form
{
    private enum ButtonKind { Primary, Secondary, Dark }

    private enum StatusKind { Info, Success, Warning, Error }

    private static readonly CultureInfo Mx = CultureInfo.GetCultureInfo("es-MX");

    private static readonly string[] MarcasComunes =
    [
        "Acura", "Audi", "BMW", "Buick", "Cadillac", "Chevrolet", "Chrysler", "Dodge", "Fiat", "Ford",
        "GMC", "Honda", "Hyundai", "Infiniti", "Jeep", "Kia", "Lincoln", "Mazda", "Mercedes-Benz", "MG",
        "Mitsubishi", "Nissan", "Peugeot", "RAM", "Renault", "Seat", "Subaru", "Suzuki", "Toyota", "Volkswagen", "Volvo"
    ];

    private static readonly string[] Configuraciones =
    [
        "3 cilindros", "4 cilindros", "4 cilindros turbo", "V6", "V6 turbo", "6 en línea", "V8", "V10",
        "Híbrido", "Eléctrico"
    ];

    private static readonly Regex MotorRegex = new(@"^\s*(\d+(?:[.,]\d+)?)\s*(?:l|lt|lts|litros?)?\.?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly VehicleRepository _repository;
    private readonly VehicleQrService _qrService = new();
    private readonly Bitmap? _logo = LoadLogo();
    private readonly Bitmap _placeholder;
    private readonly ErrorProvider _errors;
    private readonly ToolTip _toolTip = new() { InitialDelay = 350 };
    private readonly System.Windows.Forms.Timer _previewTimer = new() { Interval = 280 };
    private readonly System.Windows.Forms.Timer _statusTimer = new() { Interval = 6000 };

    // Captura
    private readonly Label _formTitle = new();
    private readonly TextBox _marcaTextBox = CreateTextBox();
    private readonly TextBox _modeloTextBox = CreateTextBox();
    private readonly NumericUpDown _anioNumeric = CreateNumeric(1900, DateTime.Today.Year + 2, DateTime.Today.Year, thousands: false);
    private readonly NumericUpDown _kilometrajeNumeric = CreateNumeric(0, 2_000_000, 0, increment: 1000);
    private readonly TextBox _precioTextBox = CreateTextBox();
    private readonly ComboBox _procedenciaCombo = CreateComboBox(ComboBoxStyle.DropDownList);
    private readonly TextBox _motorLitrosTextBox = CreateTextBox();
    private readonly ComboBox _configuracionCombo = CreateComboBox(ComboBoxStyle.DropDown);
    private readonly TextBox _observacionesTextBox = CreateTextBox(multiline: true);
    private readonly Button _saveButton = new();
    private readonly Button _newButton = new();
    private readonly Button _soldButton = new();

    // Vista previa
    private readonly PictureBox _qrPictureBox = new();
    private readonly Label _badgeLabel = new();
    private readonly Label _previewTitle = new();
    private readonly Label _previewSubtitle = new();
    private readonly Label _previewPrice = new();
    private readonly TextBox _payloadTextBox = new();
    private readonly Button _savePngButton = new();
    private readonly Button _copyButton = new();
    private readonly Button _sheetButton = new();
    private readonly Button _printButton = new();

    // Historial
    private readonly TextBox _searchTextBox = new();
    private readonly CheckBox _showSoldCheck = new();
    private readonly Label _historyCountLabel = new();
    private readonly DataGridView _historyGrid = new();
    private readonly ContextMenuStrip _historyMenu = new();
    private readonly ToolStripMenuItem _menuOpen = new("Abrir") { ShortcutKeyDisplayString = "Enter" };
    private readonly ToolStripMenuItem _menuSold = new("Marcar como vendido");
    private readonly ToolStripMenuItem _menuDelete = new("Eliminar") { ShortcutKeyDisplayString = "Supr" };

    // Encabezado y barra de estado
    private readonly Label _statsLabel = new();
    private readonly ToolStripStatusLabel _statusLabel = new();

    /// <summary>Registro guardado que se está editando; null cuando se captura uno nuevo.</summary>
    private VehicleRecord? _editing;
    private bool _dirty;
    private bool _loading;

    public Form1() : this(new VehicleRepository())
    {
    }

    public Form1(VehicleRepository repository)
    {
        _repository = repository;
        _errors = new ErrorProvider { BlinkStyle = ErrorBlinkStyle.NeverBlink, ContainerControl = this };
        _placeholder = CreatePlaceholder();

        InitializeForm();
        WireEvents();
        LoadAutocomplete();
        RefreshHistory();
        RefreshStats();
        UpdateModeUi();
        UpdatePreview();
        ShowStatus("Listo. Atajos: Ctrl+S guardar · Ctrl+N nuevo · Ctrl+P imprimir ficha · Ctrl+F buscar", StatusKind.Info, autoHide: false);

        Shown += (_, _) => _marcaTextBox.Focus();
    }

    #region Construcción de la interfaz

    private void InitializeForm()
    {
        // Las medidas están pensadas a 96 DPI; WinForms las escala solo en pantallas con zoom (125 %, 150 %…).
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = $"Generador de QR · {Brand.Nombre}";
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Brand.Surface;
        Font = new Font("Segoe UI", 10F);
        if (_logo is not null)
        {
            using var small = new Bitmap(_logo, new Size(64, 64));
            Icon = Icon.FromHandle(small.GetHicon());
        }

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(12),
            BackColor = Brand.Surface
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 468F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var historyCard = BuildHistoryCard();
        root.Controls.Add(BuildCaptureCard(), 0, 0);
        root.Controls.Add(BuildPreviewCard(), 1, 0);
        root.Controls.Add(historyCard, 0, 1);
        root.SetColumnSpan(historyCard, 2);

        var accent = new Panel { Dock = DockStyle.Top, Height = 4, BackColor = Brand.Red };

        Controls.Add(root);
        Controls.Add(accent);
        Controls.Add(BuildHeader());
        Controls.Add(BuildStatusBar());
        ResumeLayout(false);
        PerformLayout();

        // Tamaño inicial ajustado a la pantalla, ya con el escalado aplicado.
        var scale = DeviceDpi / 96F;
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        MinimumSize = new Size(Math.Min((int)(1040 * scale), area.Width), Math.Min((int)(720 * scale), area.Height));
        Size = new Size(Math.Min((int)(1380 * scale), area.Width), Math.Min((int)(920 * scale), area.Height));
    }

    private Control BuildHeader()
    {
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 80,
            BackColor = Brand.Ink,
            Padding = new Padding(16, 10, 24, 10)
        };

        var logoBox = new PictureBox
        {
            Dock = DockStyle.Left,
            Width = 96,
            BackColor = Color.White,
            SizeMode = PictureBoxSizeMode.Zoom,
            Padding = new Padding(2),
            Image = _logo
        };

        var textPanel = new Panel { Dock = DockStyle.Fill, BackColor = Brand.Ink };
        textPanel.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "Generador de QR para vehículos",
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 18F),
            Location = new Point(14, 2)
        });
        textPanel.Controls.Add(new Label
        {
            AutoSize = true,
            Text = $"{Brand.Nombre} · {Brand.Ubicacion} · funciona sin internet",
            ForeColor = Color.FromArgb(178, 182, 190),
            Location = new Point(17, 38)
        });

        _statsLabel.Dock = DockStyle.Right;
        _statsLabel.AutoSize = false;
        _statsLabel.Width = 420;
        _statsLabel.TextAlign = ContentAlignment.MiddleRight;
        _statsLabel.ForeColor = Color.White;
        _statsLabel.Font = new Font("Segoe UI Semibold", 10.5F);

        header.Controls.Add(textPanel);
        header.Controls.Add(_statsLabel);
        header.Controls.Add(logoBox);
        return header;
    }

    private Control BuildStatusBar()
    {
        var status = new StatusStrip { BackColor = Color.White, SizingGrip = true, Padding = new Padding(10, 2, 4, 2) };
        _statusLabel.Spring = true;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;

        var dbLabel = new ToolStripStatusLabel("📁 Carpeta de datos")
        {
            ForeColor = Brand.Muted,
            IsLink = true,
            LinkColor = Brand.Muted,
            ActiveLinkColor = Brand.Red,
            ToolTipText = $"Base local: {_repository.DatabasePath} · clic para abrir la carpeta"
        };
        dbLabel.Click += (_, _) => OpenInExplorer(_repository.DatabasePath);

        status.Items.Add(_statusLabel);
        status.Items.Add(dbLabel);
        return status;
    }

    private Control BuildCaptureCard()
    {
        var card = CreateCard("Datos del vehículo", out var body, _formTitle);
        body.AutoScroll = true;

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

        _procedenciaCombo.Items.AddRange(["Nacional", "Americano regularizado", "Americano no regularizado"]);
        _procedenciaCombo.SelectedIndex = 0;
        _configuracionCombo.Items.AddRange(Configuraciones);

        _precioTextBox.PlaceholderText = "Ej. 495000";
        _motorLitrosTextBox.PlaceholderText = "Ej. 5.0 L";
        _configuracionCombo.Text = string.Empty;
        _observacionesTextBox.PlaceholderText = "Opcional: equipamiento, detalles, servicios…";

        AddFieldRow(grid, ("Marca *", _marcaTextBox), ("Modelo *", _modeloTextBox));
        AddFieldRow(grid, ("Año *", _anioNumeric), ("Kilometraje *", _kilometrajeNumeric));
        AddFieldRow(grid, ("Precio (MXN) *", _precioTextBox), ("Procedencia *", _procedenciaCombo));
        AddFieldRow(grid, ("Motor / litraje *", _motorLitrosTextBox), ("Configuración / cilindros *", _configuracionCombo));
        AddWideField(grid, "Observaciones", _observacionesTextBox, 54);

        StyleButton(_saveButton, "Generar QR y guardar", ButtonKind.Primary, (_, _) => Save());
        StyleButton(_newButton, "Nuevo", ButtonKind.Secondary, (_, _) => NewVehicle());
        StyleButton(_soldButton, "Marcar como vendido", ButtonKind.Secondary, (_, _) =>
        {
            if (_editing is not null)
            {
                ToggleSold(_editing);
            }
        });
        _toolTip.SetToolTip(_saveButton, "Guardar (Ctrl+S)");
        _toolTip.SetToolTip(_newButton, "Capturar un vehículo nuevo (Ctrl+N)");

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 54,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 12, 0, 0)
        };
        buttons.Controls.AddRange([_saveButton, _newButton, _soldButton]);

        body.Controls.Add(buttons);
        body.Controls.Add(grid);
        return card;
    }

    private Control BuildPreviewCard()
    {
        var card = CreateCard("Vista previa", out var body);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 47F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 53F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        _qrPictureBox.Dock = DockStyle.Fill;
        _qrPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
        _qrPictureBox.BackColor = Color.White;
        _qrPictureBox.Margin = new Padding(0, 0, 18, 0);
        _qrPictureBox.Cursor = Cursors.Hand;
        _toolTip.SetToolTip(_qrPictureBox, "Doble clic para copiar el QR");
        _qrPictureBox.DoubleClick += (_, _) => CopyQr();

        var info = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Margin = Padding.Empty };
        info.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
        info.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
        info.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
        info.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
        info.RowStyles.Add(new RowStyle(SizeType.Absolute, 94F));
        info.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        _badgeLabel.AutoSize = true;
        _badgeLabel.Padding = new Padding(8, 3, 8, 3);
        _badgeLabel.Margin = new Padding(0, 2, 0, 0);
        _badgeLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        _badgeLabel.Anchor = AnchorStyles.Left | AnchorStyles.Top;

        _previewTitle.Dock = DockStyle.Fill;
        _previewTitle.AutoEllipsis = true;
        _previewTitle.Font = new Font("Segoe UI Semibold", 17F);
        _previewTitle.ForeColor = Brand.Ink;
        _previewTitle.TextAlign = ContentAlignment.MiddleLeft;

        _previewSubtitle.Dock = DockStyle.Fill;
        _previewSubtitle.AutoEllipsis = true;
        _previewSubtitle.ForeColor = Brand.Muted;
        _previewSubtitle.TextAlign = ContentAlignment.TopLeft;

        _previewPrice.Dock = DockStyle.Fill;
        _previewPrice.AutoEllipsis = true;
        _previewPrice.Font = new Font("Segoe UI", 21F, FontStyle.Bold);
        _previewPrice.ForeColor = Brand.Red;
        _previewPrice.TextAlign = ContentAlignment.MiddleLeft;

        StyleButton(_sheetButton, "Ficha PNG", ButtonKind.Dark, (_, _) => SaveSheetPng());
        StyleButton(_printButton, "Imprimir ficha", ButtonKind.Dark, (_, _) => PrintSheet());
        StyleButton(_savePngButton, "Guardar QR (PNG)", ButtonKind.Secondary, (_, _) => SaveQrPng());
        StyleButton(_copyButton, "Copiar QR", ButtonKind.Secondary, (_, _) => CopyQr());
        _toolTip.SetToolTip(_sheetButton, "Ficha tamaño carta con logo, precio, datos y QR (Ctrl+E)");
        _toolTip.SetToolTip(_printButton, "Imprimir la ficha o guardarla como PDF con «Microsoft Print to PDF» (Ctrl+P)");
        _toolTip.SetToolTip(_savePngButton, "Guardar solo el código QR en alta resolución");
        _toolTip.SetToolTip(_copyButton, "Copiar el QR para pegarlo en WhatsApp, Word, etc. (Ctrl+Shift+C)");

        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = new Padding(0, 4, 0, 6) };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        actions.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        actions.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        foreach (var button in new[] { _sheetButton, _printButton, _savePngButton, _copyButton })
        {
            button.AutoSize = false;
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(0, 0, 6, 6);
            button.Padding = new Padding(4, 0, 4, 0);
            actions.Controls.Add(button);
        }

        _payloadTextBox.Multiline = true;
        _payloadTextBox.ReadOnly = true;
        _payloadTextBox.ScrollBars = ScrollBars.Vertical;
        _payloadTextBox.BorderStyle = BorderStyle.None;
        _payloadTextBox.BackColor = Brand.Surface;
        _payloadTextBox.ForeColor = Brand.InkSoft;
        _payloadTextBox.Font = new Font("Consolas", 9F);
        _payloadTextBox.Dock = DockStyle.Fill;
        var payloadPanel = new Panel { Dock = DockStyle.Fill, BackColor = Brand.Surface, Padding = new Padding(10, 8, 6, 8), Margin = Padding.Empty };
        payloadPanel.Controls.Add(_payloadTextBox);
        _toolTip.SetToolTip(_payloadTextBox, "Texto exacto que leerá el teléfono al escanear");

        info.Controls.Add(_badgeLabel, 0, 0);
        info.Controls.Add(_previewTitle, 0, 1);
        info.Controls.Add(_previewSubtitle, 0, 2);
        info.Controls.Add(_previewPrice, 0, 3);
        info.Controls.Add(actions, 0, 4);
        info.Controls.Add(payloadPanel, 0, 5);

        layout.Controls.Add(_qrPictureBox, 0, 0);
        layout.Controls.Add(info, 1, 0);
        body.Controls.Add(layout);
        return card;
    }

    private Control BuildHistoryCard()
    {
        // Búsqueda y filtro van en la misma línea del título para dejarle más espacio a la tabla.
        var toolbar = new Panel { Width = 560, Padding = new Padding(0, 4, 0, 0) };
        _searchTextBox.PlaceholderText = "Buscar por marca, modelo, año, motor…  (Ctrl+F)";
        _searchTextBox.BorderStyle = BorderStyle.FixedSingle;
        _searchTextBox.Dock = DockStyle.Right;
        _searchTextBox.Width = 380;

        _showSoldCheck.Text = "Mostrar vendidos";
        _showSoldCheck.AutoSize = true;
        _showSoldCheck.Checked = true;
        _showSoldCheck.Dock = DockStyle.Right;
        _showSoldCheck.Padding = new Padding(16, 0, 0, 0);
        _showSoldCheck.ForeColor = Brand.InkSoft;
        toolbar.Controls.Add(_searchTextBox);
        toolbar.Controls.Add(_showSoldCheck);

        var card = CreateCard("Inventario", out var body, _historyCountLabel, toolbar);
        ConfigureHistoryGrid();
        _toolTip.SetToolTip(_historyGrid, "Doble clic o Enter para abrir · clic derecho para más opciones · Supr para eliminar");

        body.Controls.Add(_historyGrid);
        return card;
    }

    private void ConfigureHistoryGrid()
    {
        var grid = _historyGrid;
        grid.Dock = DockStyle.Fill;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.ReadOnly = true;
        grid.MultiSelect = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.AutoGenerateColumns = false;
        grid.BackgroundColor = Color.White;
        grid.BorderStyle = BorderStyle.None;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.GridColor = Brand.Border;
        grid.RowHeadersVisible = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.ColumnHeadersHeight = 34;
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Brand.Surface,
            ForeColor = Brand.Muted,
            SelectionBackColor = Brand.Surface,
            SelectionForeColor = Brand.Muted,
            Font = new Font("Segoe UI Semibold", 9F),
            Padding = new Padding(6, 0, 0, 0)
        };
        grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.White,
            ForeColor = Brand.Ink,
            SelectionBackColor = Brand.RedTint,
            SelectionForeColor = Brand.Ink,
            Padding = new Padding(6, 0, 0, 0),
            FormatProvider = Mx
        };
        grid.RowTemplate.Height = 32;

        grid.Columns.Add(Column("Marca", nameof(VehicleRecord.Marca), 110));
        grid.Columns.Add(Column("Modelo", nameof(VehicleRecord.Modelo), 130));
        grid.Columns.Add(Column("Año", nameof(VehicleRecord.Anio), 55));
        grid.Columns.Add(Column("Kilometraje", nameof(VehicleRecord.Kilometraje), 85, "#,0 'km'", right: true));
        grid.Columns.Add(Column("Precio", nameof(VehicleRecord.Precio), 90, "C0", right: true));
        grid.Columns.Add(Column("Procedencia", nameof(VehicleRecord.Procedencia), 130));
        grid.Columns.Add(Column("Motor", nameof(VehicleRecord.Motor), 100));
        grid.Columns.Add(Column("Estado", nameof(VehicleRecord.Estado), 75));
        grid.Columns.Add(Column("Registrado", nameof(VehicleRecord.CreadoEn), 80, "dd/MM/yyyy"));

        grid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0 || grid.Rows[e.RowIndex].DataBoundItem is not VehicleRecord v || e.CellStyle is null)
            {
                return;
            }

            if (grid.Columns[e.ColumnIndex].DataPropertyName == nameof(VehicleRecord.Estado))
            {
                e.CellStyle.ForeColor = v.Vendido ? Brand.Red : Brand.Success;
                e.CellStyle.SelectionForeColor = e.CellStyle.ForeColor;
                e.CellStyle.Font = new Font(grid.Font, FontStyle.Bold);
            }
            else if (v.Vendido)
            {
                e.CellStyle.ForeColor = Brand.Muted;
            }
        };

        grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0)
            {
                OpenSelected();
            }
        };

        grid.CellMouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Right && e.RowIndex >= 0)
            {
                grid.CurrentCell = grid.Rows[e.RowIndex].Cells[Math.Max(0, e.ColumnIndex)];
            }
        };

        grid.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                OpenSelected();
                e.Handled = e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Delete)
            {
                DeleteSelected();
                e.Handled = e.SuppressKeyPress = true;
            }
        };

        _menuOpen.Click += (_, _) => OpenSelected();
        _menuSold.Click += (_, _) =>
        {
            if (SelectedVehicle() is { } v)
            {
                ToggleSold(v);
            }
        };
        _menuDelete.Click += (_, _) => DeleteSelected();
        _menuDelete.ForeColor = Brand.Red;
        _historyMenu.Items.AddRange([_menuOpen, _menuSold, new ToolStripSeparator(), _menuDelete]);
        _historyMenu.Opening += (_, e) =>
        {
            var v = SelectedVehicle();
            e.Cancel = v is null;
            if (v is not null)
            {
                _menuSold.Text = v.Vendido ? "Marcar como disponible" : "Marcar como vendido";
            }
        };
        grid.ContextMenuStrip = _historyMenu;
    }

    private static DataGridViewTextBoxColumn Column(string header, string property, float weight, string? format = null, bool right = false)
    {
        var column = new DataGridViewTextBoxColumn
        {
            HeaderText = header,
            DataPropertyName = property,
            FillWeight = weight,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        if (format is not null)
        {
            column.DefaultCellStyle.Format = format;
        }

        if (right)
        {
            column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            column.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
            column.DefaultCellStyle.Padding = new Padding(0, 0, 10, 0);
        }

        return column;
    }

    private void WireEvents()
    {
        foreach (var control in new Control[]
                 {
                     _marcaTextBox, _modeloTextBox, _precioTextBox, _motorLitrosTextBox,
                     _configuracionCombo, _observacionesTextBox
                 })
        {
            control.TextChanged += OnFormChanged;
        }

        _anioNumeric.ValueChanged += OnFormChanged;
        _kilometrajeNumeric.ValueChanged += OnFormChanged;
        _procedenciaCombo.SelectedIndexChanged += OnFormChanged;

        _precioTextBox.Leave += (_, _) =>
        {
            if (TryParsePrecio(_precioTextBox.Text, out var precio))
            {
                _precioTextBox.Text = precio.ToString("#,0.##", CultureInfo.InvariantCulture);
            }
        };

        _motorLitrosTextBox.Leave += (_, _) =>
        {
            var match = MotorRegex.Match(_motorLitrosTextBox.Text);
            if (match.Success &&
                decimal.TryParse(match.Groups[1].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var litros))
            {
                _motorLitrosTextBox.Text = $"{litros.ToString("0.0", CultureInfo.InvariantCulture)} L";
            }
        };

        _previewTimer.Tick += (_, _) =>
        {
            _previewTimer.Stop();
            UpdatePreview();
        };

        _statusTimer.Tick += (_, _) =>
        {
            _statusTimer.Stop();
            ShowStatus("Listo", StatusKind.Info, autoHide: false);
        };

        _searchTextBox.TextChanged += (_, _) => RefreshHistory();
        _showSoldCheck.CheckedChanged += (_, _) => RefreshHistory();
        _searchTextBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Down && _historyGrid.Rows.Count > 0)
            {
                _historyGrid.Focus();
                e.Handled = true;
            }
        };
    }

    #endregion

    #region Captura y guardado

    private void OnFormChanged(object? sender, EventArgs e)
    {
        if (_loading)
        {
            return;
        }

        _dirty = true;
        if (sender is Control control)
        {
            _errors.SetError(control, string.Empty);
        }

        _previewTimer.Stop();
        _previewTimer.Start();
        UpdateModeUi();
    }

    private void Save()
    {
        if (!TryReadForm(out var vehicle, showErrors: true))
        {
            ShowStatus("Revisa los campos marcados en rojo.", StatusKind.Warning);
            return;
        }

        try
        {
            if (_editing is null)
            {
                vehicle.CreadoEn = DateTime.Now;
                vehicle.Id = _repository.Insert(vehicle);
                ShowStatus($"✔ {vehicle.Titulo} {vehicle.Anio} guardado. Ya puedes imprimir su ficha (Ctrl+P).", StatusKind.Success);
            }
            else
            {
                if (!_dirty)
                {
                    return;
                }

                _repository.Update(vehicle);
                vehicle.ActualizadoEn = DateTime.Now;
                ShowStatus($"✔ Cambios de {vehicle.Titulo} {vehicle.Anio} guardados.", StatusKind.Success);
            }

            _editing = vehicle;
            _dirty = false;
            RefreshHistory(vehicle.Id);
            RefreshStats();
            LoadAutocomplete();
            UpdateModeUi();
            UpdatePreview();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"No se pudo guardar el vehículo:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private bool TryReadForm(out VehicleRecord vehicle, bool showErrors)
    {
        if (showErrors)
        {
            _errors.Clear();
        }

        var problems = new List<(Control Control, string Message)>();
        var marca = _marcaTextBox.Text.Trim();
        var modelo = _modeloTextBox.Text.Trim();
        var motor = _motorLitrosTextBox.Text.Trim();
        var configuracion = _configuracionCombo.Text.Trim();

        if (marca.Length == 0) problems.Add((_marcaTextBox, "Escribe la marca."));
        if (modelo.Length == 0) problems.Add((_modeloTextBox, "Escribe el modelo."));
        if (!TryParsePrecio(_precioTextBox.Text, out var precio)) problems.Add((_precioTextBox, "Escribe un precio válido, por ejemplo 495000."));
        if (motor.Length == 0) problems.Add((_motorLitrosTextBox, "Escribe el litraje, por ejemplo 2.5 L."));
        if (configuracion.Length == 0) problems.Add((_configuracionCombo, "Elige o escribe la configuración, por ejemplo V6."));

        if (problems.Count > 0)
        {
            vehicle = new VehicleRecord();
            if (showErrors)
            {
                foreach (var (control, message) in problems)
                {
                    _errors.SetError(control, message);
                }

                problems[0].Control.Focus();
            }

            return false;
        }

        vehicle = new VehicleRecord
        {
            Id = _editing?.Id ?? 0,
            CreadoEn = _editing?.CreadoEn ?? DateTime.Now,
            ActualizadoEn = _editing?.ActualizadoEn,
            Vendido = _editing?.Vendido ?? false,
            Marca = marca,
            Modelo = modelo,
            Anio = (int)_anioNumeric.Value,
            Kilometraje = (long)_kilometrajeNumeric.Value,
            Precio = precio,
            Procedencia = _procedenciaCombo.Text,
            MotorLitros = motor,
            ConfiguracionMotor = configuracion,
            Observaciones = _observacionesTextBox.Text.Trim()
        };
        return true;
    }

    private static bool TryParsePrecio(string text, out decimal precio)
    {
        var clean = text.Trim()
            .Replace("$", string.Empty)
            .Replace(",", string.Empty)
            .Replace("MXN", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" ", string.Empty);
        return decimal.TryParse(clean, NumberStyles.Number, CultureInfo.InvariantCulture, out precio) && precio >= 0;
    }

    private void NewVehicle()
    {
        if (!ConfirmDiscard())
        {
            return;
        }

        ClearForm();
        ShowStatus("Captura un vehículo nuevo.", StatusKind.Info);
    }

    private void ClearForm()
    {
        _loading = true;
        _marcaTextBox.Clear();
        _modeloTextBox.Clear();
        _anioNumeric.Value = DateTime.Today.Year;
        _kilometrajeNumeric.Value = 0;
        _precioTextBox.Clear();
        _procedenciaCombo.SelectedIndex = 0;
        _motorLitrosTextBox.Clear();
        _configuracionCombo.Text = string.Empty;
        _observacionesTextBox.Clear();
        _loading = false;

        _editing = null;
        _dirty = false;
        _errors.Clear();
        _historyGrid.ClearSelection();
        UpdateModeUi();
        UpdatePreview();
        _marcaTextBox.Focus();
    }

    private void LoadVehicleIntoForm(VehicleRecord vehicle)
    {
        if (_editing?.Id == vehicle.Id && !_dirty)
        {
            return;
        }

        if (!ConfirmDiscard())
        {
            return;
        }

        _loading = true;
        _marcaTextBox.Text = vehicle.Marca;
        _modeloTextBox.Text = vehicle.Modelo;
        _anioNumeric.Value = Math.Clamp(vehicle.Anio, (int)_anioNumeric.Minimum, (int)_anioNumeric.Maximum);
        _kilometrajeNumeric.Value = Math.Clamp(vehicle.Kilometraje, (long)_kilometrajeNumeric.Minimum, (long)_kilometrajeNumeric.Maximum);
        _precioTextBox.Text = vehicle.Precio.ToString("#,0.##", CultureInfo.InvariantCulture);
        if (_procedenciaCombo.Items.Contains(vehicle.Procedencia))
        {
            _procedenciaCombo.SelectedItem = vehicle.Procedencia;
        }
        _motorLitrosTextBox.Text = vehicle.MotorLitros;
        _configuracionCombo.Text = vehicle.ConfiguracionMotor;
        _observacionesTextBox.Text = vehicle.Observaciones;
        _loading = false;

        _editing = vehicle.Clone();
        _dirty = false;
        _errors.Clear();
        UpdateModeUi();
        UpdatePreview();
        ShowStatus($"Abriste {vehicle.Titulo} {vehicle.Anio}. Modifica lo que necesites y pulsa «Guardar cambios».", StatusKind.Info);
    }

    private bool ConfirmDiscard()
    {
        if (!_dirty || !HasAnyInput())
        {
            return true;
        }

        return MessageBox.Show(this,
            "Hay cambios sin guardar. ¿Deseas descartarlos?",
            "Cambios sin guardar",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2) == DialogResult.Yes;
    }

    private bool HasAnyInput() =>
        _marcaTextBox.TextLength > 0 || _modeloTextBox.TextLength > 0 || _precioTextBox.TextLength > 0 ||
        _motorLitrosTextBox.TextLength > 0 || _configuracionCombo.Text.Length > 0 || _observacionesTextBox.TextLength > 0 ||
        _editing is not null;

    private void UpdateModeUi()
    {
        if (_editing is null)
        {
            _formTitle.Text = "Nuevo vehículo";
            _saveButton.Text = "Generar QR y guardar";
            _saveButton.Enabled = true;
            _soldButton.Visible = false;
            SetBadge(_dirty ? "BORRADOR · SIN GUARDAR" : "NUEVO", Brand.Muted, Brand.Surface);
            return;
        }

        _formTitle.Text = $"Editando: {_editing.Titulo} {_editing.Anio}";
        _saveButton.Text = "Guardar cambios";
        _saveButton.Enabled = _dirty;
        _soldButton.Visible = true;
        _soldButton.Text = _editing.Vendido ? "Marcar como disponible" : "Marcar como vendido";

        if (_dirty)
        {
            SetBadge("CAMBIOS SIN GUARDAR", Brand.Warning, Brand.WarningTint);
        }
        else if (_editing.Vendido)
        {
            SetBadge("VENDIDO", Color.White, Brand.Red);
        }
        else
        {
            SetBadge("✔ GUARDADO · DISPONIBLE", Brand.Success, Brand.SuccessTint);
        }
    }

    private void SetBadge(string text, Color fore, Color back)
    {
        _badgeLabel.Text = text;
        _badgeLabel.ForeColor = fore;
        _badgeLabel.BackColor = back;
    }

    #endregion

    #region Vista previa y exportación

    private void UpdatePreview()
    {
        if (!TryReadForm(out var vehicle, showErrors: false))
        {
            ReplaceImage(_placeholder);
            _previewTitle.Text = _editing is null ? "Nuevo vehículo" : _editing.Titulo;
            _previewTitle.ForeColor = Brand.Muted;
            _previewSubtitle.Text = "Completa los campos con * y el QR aparecerá aquí al instante.";
            _previewPrice.Text = string.Empty;
            _payloadTextBox.Clear();
            SetExportEnabled(false);
            return;
        }

        var payload = _qrService.BuildPayload(vehicle);
        ReplaceImage(_qrService.BuildImage(payload, 10, _logo));
        _previewTitle.Text = vehicle.Titulo;
        _previewTitle.ForeColor = Brand.Ink;
        _previewSubtitle.Text = $"{vehicle.Anio}  ·  {vehicle.Kilometraje.ToString("N0", Mx)} km  ·  {vehicle.Procedencia}  ·  {vehicle.Motor}";
        _previewPrice.Text = $"{vehicle.Precio.ToString("C0", Mx)} MXN";
        _payloadTextBox.Text = payload;
        SetExportEnabled(true);
    }

    private void SetExportEnabled(bool enabled)
    {
        _sheetButton.Enabled = enabled;
        _printButton.Enabled = enabled;
        _savePngButton.Enabled = enabled;
        _copyButton.Enabled = enabled;
    }

    private bool TryGetExportData(out VehicleRecord vehicle, out string payload)
    {
        payload = string.Empty;
        if (!TryReadForm(out vehicle, showErrors: true))
        {
            ShowStatus("Completa los datos obligatorios antes de exportar.", StatusKind.Warning);
            return false;
        }

        payload = _qrService.BuildPayload(vehicle);
        return true;
    }

    private void SaveQrPng()
    {
        if (!TryGetExportData(out var vehicle, out var payload))
        {
            return;
        }

        var path = AskSavePath($"QR-{vehicle.Titulo}-{vehicle.Anio}", "Guardar código QR");
        if (path is null)
        {
            return;
        }

        try
        {
            using var image = _qrService.BuildImage(payload, 20, _logo);
            image.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            ShowStatus($"QR guardado en {path}{UnsavedHint()}", StatusKind.Success);
        }
        catch (Exception ex)
        {
            ShowError("No se pudo guardar la imagen", ex);
        }
    }

    private void SaveSheetPng()
    {
        if (!TryGetExportData(out var vehicle, out var payload))
        {
            return;
        }

        var path = AskSavePath($"Ficha-{vehicle.Titulo}-{vehicle.Anio}", "Guardar ficha para imprimir");
        if (path is null)
        {
            return;
        }

        try
        {
            using var qr = _qrService.BuildImage(payload, 24, _logo);
            using var sheet = VehicleSheetRenderer.RenderBitmap(vehicle, qr, _logo);
            sheet.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            ShowStatus($"Ficha guardada en {path}{UnsavedHint()}", StatusKind.Success);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowError("No se pudo generar la ficha", ex);
        }
    }

    private void PrintSheet()
    {
        if (!TryGetExportData(out var vehicle, out var payload))
        {
            return;
        }

        try
        {
            using var qr = _qrService.BuildImage(payload, 24, _logo);
            using var document = new PrintDocument { DocumentName = $"Ficha {vehicle.Titulo} {vehicle.Anio}" };
            document.DefaultPageSettings.Margins = new Margins(35, 35, 35, 35);
            document.PrintPage += (_, e) =>
            {
                if (e.Graphics is not null)
                {
                    VehicleSheetRenderer.Draw(e.Graphics, e.MarginBounds, vehicle, qr, _logo);
                }
                e.HasMorePages = false;
            };

            using var dialog = new PrintDialog { Document = document, UseEXDialog = true, AllowSomePages = false };
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            document.Print();
            ShowStatus($"Ficha enviada a «{document.PrinterSettings.PrinterName}».{UnsavedHint()}", StatusKind.Success);
        }
        catch (Exception ex)
        {
            ShowError("No se pudo imprimir", ex);
        }
    }

    private void CopyQr()
    {
        if (!TryGetExportData(out _, out var payload))
        {
            return;
        }

        try
        {
            using var image = _qrService.BuildImage(payload, 12, _logo);
            Clipboard.SetImage(image);
            ShowStatus($"QR copiado. Pégalo con Ctrl+V en WhatsApp, Word o donde quieras.{UnsavedHint()}", StatusKind.Success);
        }
        catch (Exception ex)
        {
            ShowError("No se pudo copiar el QR", ex);
        }
    }

    private string UnsavedHint() => _dirty || _editing is null ? "  (Ojo: este vehículo aún no está guardado)" : string.Empty;

    private string? AskSavePath(string baseName, string title)
    {
        var safeName = string.Join("_", baseName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Replace(' ', '-');
        using var dialog = new SaveFileDialog
        {
            Filter = "Imagen PNG (*.png)|*.png",
            FileName = $"{safeName}.png",
            Title = title
        };
        return dialog.ShowDialog(this) == DialogResult.OK ? dialog.FileName : null;
    }

    private void ReplaceImage(Bitmap? image)
    {
        var oldImage = _qrPictureBox.Image;
        _qrPictureBox.Image = image;
        if (oldImage is not null && !ReferenceEquals(oldImage, _placeholder))
        {
            oldImage.Dispose();
        }
    }

    private static Bitmap CreatePlaceholder()
    {
        const int size = 420;
        var bitmap = new Bitmap(size, size);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        g.Clear(Color.White);

        var frame = new RectangleF(24, 24, size - 48, size - 48);
        using (var pen = new Pen(Brand.Border, 3) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
        using (var path = Brand.RoundedRect(frame, 18))
        {
            g.DrawPath(pen, path);
        }

        // Esquinas tipo QR.
        using var corner = new SolidBrush(Brand.Border);
        foreach (var (x, y) in new[] { (60f, 60f), (size - 130f, 60f), (60f, size - 130f) })
        {
            g.FillRectangle(corner, x, y, 70, 70);
            g.FillRectangle(Brushes.White, x + 10, y + 10, 50, 50);
            g.FillRectangle(corner, x + 20, y + 20, 30, 30);
        }

        using var font = new Font("Segoe UI Semibold", 15F, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(Brand.Muted);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString("Aquí aparecerá el QR", font, brush, new RectangleF(0, size / 2f + 40, size, 40), format);
        return bitmap;
    }

    #endregion

    #region Historial

    private void RefreshHistory(long? selectId = null)
    {
        selectId ??= _editing?.Id;
        try
        {
            var items = _repository.Search(_searchTextBox.Text, _showSoldCheck.Checked).ToList();
            _historyGrid.DataSource = items;

            var filtered = _searchTextBox.TextLength > 0 ? $" para «{_searchTextBox.Text.Trim()}»" : string.Empty;
            _historyCountLabel.Text = items.Count switch
            {
                0 when filtered.Length > 0 => $"Inventario · sin resultados{filtered}",
                0 => "Inventario · aún vacío, ¡captura el primero!",
                1 => $"Inventario · 1 vehículo{filtered}",
                _ => $"Inventario · {items.Count} vehículos{filtered}"
            };

            _historyGrid.ClearSelection();
            if (selectId is not null)
            {
                foreach (DataGridViewRow row in _historyGrid.Rows)
                {
                    if (row.DataBoundItem is VehicleRecord v && v.Id == selectId)
                    {
                        row.Selected = true;
                        _historyGrid.CurrentCell = row.Cells[0];
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ShowError("No se pudo leer el inventario", ex);
        }
    }

    private void RefreshStats()
    {
        try
        {
            var stats = _repository.GetStats();
            _statsLabel.Text =
                $"{stats.Disponibles} {(stats.Disponibles == 1 ? "auto disponible" : "autos disponibles")}\n" +
                $"Valor: {stats.ValorDisponible.ToString("C0", Mx)}  ·  {stats.Vendidos} {(stats.Vendidos == 1 ? "vendido" : "vendidos")}";
        }
        catch
        {
            _statsLabel.Text = string.Empty;
        }
    }

    private void LoadAutocomplete()
    {
        try
        {
            var marcas = new AutoCompleteStringCollection();
            marcas.AddRange(MarcasComunes.Union(_repository.GetDistinctMarcas(), StringComparer.OrdinalIgnoreCase).ToArray());
            _marcaTextBox.AutoCompleteCustomSource = marcas;
            _marcaTextBox.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            _marcaTextBox.AutoCompleteSource = AutoCompleteSource.CustomSource;

            var modelos = new AutoCompleteStringCollection();
            modelos.AddRange(_repository.GetDistinctModelos().ToArray());
            _modeloTextBox.AutoCompleteCustomSource = modelos;
            _modeloTextBox.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            _modeloTextBox.AutoCompleteSource = AutoCompleteSource.CustomSource;
        }
        catch
        {
            // El autocompletado es opcional; si falla, la captura sigue funcionando.
        }
    }

    private VehicleRecord? SelectedVehicle() =>
        _historyGrid.CurrentRow?.DataBoundItem as VehicleRecord;

    private void OpenSelected()
    {
        if (SelectedVehicle() is { } vehicle)
        {
            LoadVehicleIntoForm(vehicle);
        }
    }

    private void ToggleSold(VehicleRecord vehicle)
    {
        var vendido = !vehicle.Vendido;
        try
        {
            _repository.SetVendido(vehicle.Id, vendido);
            if (_editing?.Id == vehicle.Id)
            {
                _editing.Vendido = vendido;
            }

            RefreshHistory(vehicle.Id);
            RefreshStats();
            UpdateModeUi();
            UpdatePreview();
            ShowStatus(vendido
                ? $"🎉 {vehicle.Titulo} {vehicle.Anio} marcado como vendido."
                : $"{vehicle.Titulo} {vehicle.Anio} vuelve a estar disponible.", StatusKind.Success);
        }
        catch (Exception ex)
        {
            ShowError("No se pudo actualizar el estado", ex);
        }
    }

    private void DeleteSelected()
    {
        if (SelectedVehicle() is not { } vehicle)
        {
            return;
        }

        var answer = MessageBox.Show(this,
            $"¿Eliminar {vehicle.Titulo} {vehicle.Anio} ({vehicle.Precio.ToString("C0", Mx)})?\n\nEsta acción no se puede deshacer.",
            "Eliminar vehículo",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes)
        {
            return;
        }

        try
        {
            _repository.Delete(vehicle.Id);
            if (_editing?.Id == vehicle.Id)
            {
                _dirty = false;
                ClearForm();
            }

            RefreshHistory();
            RefreshStats();
            ShowStatus($"{vehicle.Titulo} {vehicle.Anio} eliminado.", StatusKind.Info);
        }
        catch (Exception ex)
        {
            ShowError("No se pudo eliminar", ex);
        }
    }

    #endregion

    #region Utilidades

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Control | Keys.S:
                Save();
                return true;
            case Keys.Control | Keys.N:
                NewVehicle();
                return true;
            case Keys.Control | Keys.P:
                PrintSheet();
                return true;
            case Keys.Control | Keys.E:
                SaveSheetPng();
                return true;
            case Keys.Control | Keys.Shift | Keys.C:
                CopyQr();
                return true;
            case Keys.Control | Keys.F:
                _searchTextBox.Focus();
                _searchTextBox.SelectAll();
                return true;
            case Keys.Escape when _searchTextBox.Focused && _searchTextBox.TextLength > 0:
                _searchTextBox.Clear();
                return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void ShowStatus(string message, StatusKind kind, bool autoHide = true)
    {
        _statusLabel.Text = message;
        _statusLabel.ForeColor = kind switch
        {
            StatusKind.Success => Brand.Success,
            StatusKind.Warning => Brand.Warning,
            StatusKind.Error => Brand.Red,
            _ => Brand.InkSoft
        };

        _statusTimer.Stop();
        if (autoHide)
        {
            _statusTimer.Start();
        }
    }

    private void ShowError(string title, Exception ex)
    {
        ShowStatus($"{title}.", StatusKind.Error);
        MessageBox.Show(this, $"{title}:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private static void OpenInExplorer(string filePath)
    {
        try
        {
            Process.Start("explorer.exe", $"/select,\"{filePath}\"");
        }
        catch
        {
            // Sin consecuencias si el explorador no abre.
        }
    }

    private static Panel CreateCard(string title, out Panel body, Label? titleLabel = null, Control? titleAccessory = null)
    {
        var card = new Panel { Dock = DockStyle.Fill, BackColor = Brand.Border, Padding = new Padding(1), Margin = new Padding(6) };
        var inner = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(18, 12, 18, 14) };
        var header = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Color.White };

        var label = titleLabel ?? new Label();
        label.Text = title;
        label.AutoSize = true;
        label.Font = new Font("Segoe UI Semibold", 13.5F);
        label.ForeColor = Brand.Ink;
        label.Location = new Point(0, 2);

        var underline = new Panel { BackColor = Brand.Red, Size = new Size(36, 3), Location = new Point(2, 33) };
        header.Controls.Add(label);
        header.Controls.Add(underline);
        if (titleAccessory is not null)
        {
            titleAccessory.Dock = DockStyle.Right;
            header.Controls.Add(titleAccessory);
        }

        body = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        inner.Controls.Add(body);
        inner.Controls.Add(header);
        card.Controls.Add(inner);
        return card;
    }

    private static void AddFieldRow(TableLayoutPanel grid, (string Label, Control Input) left, (string Label, Control Input) right)
    {
        var labelRow = grid.RowCount++;
        var inputRow = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));

        grid.Controls.Add(FieldLabel(left.Label), 0, labelRow);
        grid.Controls.Add(FieldLabel(right.Label), 1, labelRow);
        PrepareInput(left.Input);
        PrepareInput(right.Input);
        grid.Controls.Add(left.Input, 0, inputRow);
        grid.Controls.Add(right.Input, 1, inputRow);
    }

    private static void AddWideField(TableLayoutPanel grid, string labelText, Control input, float height)
    {
        var labelRow = grid.RowCount++;
        var inputRow = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, height));

        var label = FieldLabel(labelText);
        grid.Controls.Add(label, 0, labelRow);
        grid.SetColumnSpan(label, 2);
        PrepareInput(input);
        grid.Controls.Add(input, 0, inputRow);
        grid.SetColumnSpan(input, 2);
    }

    private static Label FieldLabel(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.BottomLeft,
        ForeColor = Brand.Muted,
        Font = new Font("Segoe UI Semibold", 9F),
        Margin = new Padding(0, 0, 0, 2)
    };

    private static void PrepareInput(Control input)
    {
        input.Dock = DockStyle.Fill;
        input.Margin = new Padding(0, 2, 24, 4);
    }

    private static void StyleButton(Button button, string text, ButtonKind kind, EventHandler onClick)
    {
        button.Text = text;
        button.AutoSize = true;
        button.MinimumSize = new Size(0, 40);
        button.Padding = new Padding(14, 0, 14, 0);
        button.Margin = new Padding(0, 0, 8, 0);
        button.FlatStyle = FlatStyle.Flat;
        button.Cursor = Cursors.Hand;
        button.UseVisualStyleBackColor = false;
        button.Font = new Font("Segoe UI Semibold", 10F);

        var (back, fore, hover, pressed, border) = kind switch
        {
            ButtonKind.Primary => (Brand.Red, Color.White, Brand.RedDark, Brand.RedPressed, Brand.Red),
            ButtonKind.Dark => (Brand.Ink, Color.White, Brand.InkSoft, Color.Black, Brand.Ink),
            _ => (Color.White, Brand.Ink, Brand.Surface, Brand.Border, Brand.Border)
        };

        button.BackColor = back;
        button.ForeColor = fore;
        button.FlatAppearance.BorderColor = border;
        button.FlatAppearance.BorderSize = kind == ButtonKind.Secondary ? 1 : 0;
        button.FlatAppearance.MouseOverBackColor = hover;
        button.FlatAppearance.MouseDownBackColor = pressed;
        button.EnabledChanged += (_, _) =>
        {
            button.BackColor = button.Enabled ? back : Color.FromArgb(232, 233, 236);
            button.Cursor = button.Enabled ? Cursors.Hand : Cursors.Default;
        };
        button.Click += onClick;
    }

    private static TextBox CreateTextBox(bool multiline = false) => new()
    {
        BorderStyle = BorderStyle.FixedSingle,
        Multiline = multiline,
        ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None
    };

    private static NumericUpDown CreateNumeric(int minimum, int maximum, int value, int increment = 1, bool thousands = true) => new()
    {
        Minimum = minimum,
        Maximum = maximum,
        Value = value,
        Increment = increment,
        ThousandsSeparator = thousands,
        BorderStyle = BorderStyle.FixedSingle
    };

    private static ComboBox CreateComboBox(ComboBoxStyle style) => new()
    {
        DropDownStyle = style,
        FlatStyle = FlatStyle.System
    };

    private static Bitmap? LoadLogo()
    {
        const string resourceName = "GeneradorQrAutos.Assets.rangel-logo.png";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return null;
        }

        using var source = Image.FromStream(stream);
        return new Bitmap(source);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!e.Cancel && !ConfirmDiscard())
        {
            e.Cancel = true;
        }

        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _previewTimer.Dispose();
        _statusTimer.Dispose();
        ReplaceImage(null);
        _placeholder.Dispose();
        _logo?.Dispose();
        base.OnFormClosed(e);
    }

    #endregion
}
