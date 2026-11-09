using System.Globalization;
using GeneradorQrAutos.Models;
using Microsoft.Data.Sqlite;

namespace GeneradorQrAutos.Data;

public sealed record InventoryStats(int Disponibles, int Vendidos, decimal ValorDisponible);

/// <summary>
/// Persistencia local. La base se guarda en AppData y no requiere ningún servicio externo.
/// </summary>
public sealed class VehicleRepository
{
    private const string SelectColumns = """
        id, marca, modelo, anio, kilometraje, precio, procedencia,
        motor_litros, configuracion_motor, observaciones, creado_en,
        vendido, actualizado_en
        """;

    private readonly string _connectionString;

    public string DatabasePath { get; }

    public VehicleRepository(string? directory = null)
    {
        var appDirectory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GeneradorQrAutos");

        Directory.CreateDirectory(appDirectory);
        DatabasePath = Path.Combine(appDirectory, "vehiculos.db");
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        EnsureDatabase();
    }

    private void EnsureDatabase()
    {
        using var connection = OpenConnection();
        Execute(connection, """
            CREATE TABLE IF NOT EXISTS vehiculos (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                marca TEXT NOT NULL,
                modelo TEXT NOT NULL,
                anio INTEGER NOT NULL,
                kilometraje INTEGER NOT NULL,
                precio TEXT NOT NULL,
                procedencia TEXT NOT NULL,
                motor_litros TEXT NOT NULL,
                configuracion_motor TEXT NOT NULL,
                observaciones TEXT NOT NULL DEFAULT '',
                creado_en TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_vehiculos_marca_modelo
                ON vehiculos (marca, modelo);
            """);

        // Migraciones para bases creadas con versiones anteriores.
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA table_info(vehiculos);";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                columns.Add(reader.GetString(1));
            }
        }

        if (!columns.Contains("vendido"))
        {
            Execute(connection, "ALTER TABLE vehiculos ADD COLUMN vendido INTEGER NOT NULL DEFAULT 0;");
        }

        if (!columns.Contains("actualizado_en"))
        {
            Execute(connection, "ALTER TABLE vehiculos ADD COLUMN actualizado_en TEXT NULL;");
        }
    }

    public long Insert(VehicleRecord vehicle)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO vehiculos
                (marca, modelo, anio, kilometraje, precio, procedencia,
                 motor_litros, configuracion_motor, observaciones, creado_en, vendido)
            VALUES
                ($marca, $modelo, $anio, $kilometraje, $precio, $procedencia,
                 $motor_litros, $configuracion_motor, $observaciones, $creado_en, $vendido);
            SELECT last_insert_rowid();
            """;
        AddParameters(command, vehicle);
        return (long)(command.ExecuteScalar() ?? 0L);
    }

    public void Update(VehicleRecord vehicle)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE vehiculos SET
                marca = $marca,
                modelo = $modelo,
                anio = $anio,
                kilometraje = $kilometraje,
                precio = $precio,
                procedencia = $procedencia,
                motor_litros = $motor_litros,
                configuracion_motor = $configuracion_motor,
                observaciones = $observaciones,
                vendido = $vendido,
                actualizado_en = $actualizado_en
            WHERE id = $id;
            """;
        AddParameters(command, vehicle);
        command.Parameters.AddWithValue("$id", vehicle.Id);
        command.Parameters.AddWithValue("$actualizado_en", DateTime.Now.ToString("O"));
        command.ExecuteNonQuery();
    }

    public void SetVendido(long id, bool vendido)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE vehiculos SET vendido = $vendido, actualizado_en = $ahora WHERE id = $id;";
        command.Parameters.AddWithValue("$vendido", vendido ? 1 : 0);
        command.Parameters.AddWithValue("$ahora", DateTime.Now.ToString("O"));
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public void Delete(long id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM vehiculos WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Busca vehículos. Cada palabra del texto debe aparecer en algún campo (marca, modelo, año, motor, etc.).
    /// </summary>
    public IReadOnlyList<VehicleRecord> Search(string? text, bool includeSold, int limit = 500)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();

        var where = new List<string>();
        if (!includeSold)
        {
            where.Add("vendido = 0");
        }

        var terms = (text ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = 0; i < terms.Length; i++)
        {
            where.Add($"""
                (marca || ' ' || modelo || ' ' || anio || ' ' || procedencia || ' ' ||
                 motor_litros || ' ' || configuracion_motor || ' ' || observaciones) LIKE $t{i} ESCAPE '\'
                """);
            var escaped = terms[i].Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
            command.Parameters.AddWithValue($"$t{i}", $"%{escaped}%");
        }

        command.CommandText = $"""
            SELECT {SelectColumns}
            FROM vehiculos
            {(where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : string.Empty)}
            ORDER BY vendido ASC, id DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);

        using var reader = command.ExecuteReader();
        var result = new List<VehicleRecord>();
        while (reader.Read())
        {
            result.Add(ReadVehicle(reader));
        }

        return result;
    }

    public InventoryStats GetStats()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                COALESCE(SUM(CASE WHEN vendido = 0 THEN 1 ELSE 0 END), 0),
                COALESCE(SUM(CASE WHEN vendido = 1 THEN 1 ELSE 0 END), 0),
                COALESCE(SUM(CASE WHEN vendido = 0 THEN CAST(precio AS REAL) ELSE 0 END), 0)
            FROM vehiculos;
            """;
        using var reader = command.ExecuteReader();
        reader.Read();
        return new InventoryStats(reader.GetInt32(0), reader.GetInt32(1), (decimal)reader.GetDouble(2));
    }

    public IReadOnlyList<string> GetDistinctMarcas() => GetDistinct("marca");

    public IReadOnlyList<string> GetDistinctModelos() => GetDistinct("modelo");

    private IReadOnlyList<string> GetDistinct(string column)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        // El nombre de columna proviene solo de los métodos públicos de arriba, nunca del usuario.
        command.CommandText = $"SELECT DISTINCT {column} FROM vehiculos WHERE {column} <> '' ORDER BY {column} COLLATE NOCASE;";
        using var reader = command.ExecuteReader();
        var result = new List<string>();
        while (reader.Read())
        {
            result.Add(reader.GetString(0));
        }

        return result;
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static void AddParameters(SqliteCommand command, VehicleRecord vehicle)
    {
        command.Parameters.AddWithValue("$marca", vehicle.Marca);
        command.Parameters.AddWithValue("$modelo", vehicle.Modelo);
        command.Parameters.AddWithValue("$anio", vehicle.Anio);
        command.Parameters.AddWithValue("$kilometraje", vehicle.Kilometraje);
        command.Parameters.AddWithValue("$precio", vehicle.Precio.ToString("0.00", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$procedencia", vehicle.Procedencia);
        command.Parameters.AddWithValue("$motor_litros", vehicle.MotorLitros);
        command.Parameters.AddWithValue("$configuracion_motor", vehicle.ConfiguracionMotor);
        command.Parameters.AddWithValue("$observaciones", vehicle.Observaciones);
        command.Parameters.AddWithValue("$creado_en", vehicle.CreadoEn.ToString("O"));
        command.Parameters.AddWithValue("$vendido", vehicle.Vendido ? 1 : 0);
    }

    private static VehicleRecord ReadVehicle(SqliteDataReader reader)
    {
        _ = decimal.TryParse(reader.GetString(5), NumberStyles.Number, CultureInfo.InvariantCulture, out var precio);
        _ = DateTime.TryParse(reader.GetString(10), null, DateTimeStyles.RoundtripKind, out var creadoEn);

        DateTime? actualizadoEn = null;
        if (!reader.IsDBNull(12) &&
            DateTime.TryParse(reader.GetString(12), null, DateTimeStyles.RoundtripKind, out var actualizado))
        {
            actualizadoEn = actualizado;
        }

        return new VehicleRecord
        {
            Id = reader.GetInt64(0),
            Marca = reader.GetString(1),
            Modelo = reader.GetString(2),
            Anio = reader.GetInt32(3),
            Kilometraje = reader.GetInt64(4),
            Precio = precio,
            Procedencia = reader.GetString(6),
            MotorLitros = reader.GetString(7),
            ConfiguracionMotor = reader.GetString(8),
            Observaciones = reader.GetString(9),
            CreadoEn = creadoEn,
            Vendido = reader.GetInt64(11) != 0,
            ActualizadoEn = actualizadoEn
        };
    }
}
