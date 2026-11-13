using System.Globalization;
using GeneradorQr.Core.Models;
using Microsoft.Data.Sqlite;

namespace GeneradorQr.Migrate;

/// <summary>
/// Lee la base SQLite de la versión WinForms (%LOCALAPPDATA%\GeneradorQrAutos\vehiculos.db),
/// incluidas las bases antiguas sin las columnas vendido / actualizado_en.
/// </summary>
public static class SqliteReader
{
    public static IReadOnlyList<VehicleRecord> ReadAll(string dbPath)
    {
        if (!File.Exists(dbPath))
        {
            throw new FileNotFoundException($"No existe la base {dbPath}.", dbPath);
        }

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();

        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var info = connection.CreateCommand())
        {
            info.CommandText = "PRAGMA table_info(vehiculos);";
            using var reader = info.ExecuteReader();
            while (reader.Read())
            {
                columns.Add(reader.GetString(1));
            }
        }

        var vendido = columns.Contains("vendido") ? "vendido" : "0";
        var actualizado = columns.Contains("actualizado_en") ? "actualizado_en" : "NULL";

        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT id, marca, modelo, anio, kilometraje, precio, procedencia, motor_litros,
                   configuracion_motor, observaciones, creado_en, {vendido}, {actualizado}
            FROM vehiculos
            ORDER BY id;
            """;

        var result = new List<VehicleRecord>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                result.Add(new VehicleRecord
                {
                    Id = reader.GetInt64(0),
                    Marca = reader.GetString(1),
                    Modelo = reader.GetString(2),
                    Anio = reader.GetInt32(3),
                    Kilometraje = reader.GetInt64(4),
                    Precio = decimal.Parse(reader.GetString(5), NumberStyles.Number, CultureInfo.InvariantCulture),
                    Procedencia = ActualizarProcedencia(reader.GetString(6)),
                    MotorLitros = reader.GetString(7),
                    ConfiguracionMotor = reader.GetString(8),
                    Observaciones = reader.GetString(9),
                    CreadoEn = ParseDate(reader.GetString(10)) ?? DateTime.Now,
                    Vendido = reader.GetInt64(11) != 0,
                    ActualizadoEn = reader.IsDBNull(12) ? null : ParseDate(reader.GetString(12))
                });
            }
        }

        return result;
    }

    /// <summary>La versión WinForms decía "regularizado"; ahora el catálogo dice "legalizado".</summary>
    private static string ActualizarProcedencia(string procedencia) => procedencia switch
    {
        "Americano regularizado" => "Americano legalizado",
        "Americano no regularizado" => "Americano no legalizado",
        _ => procedencia
    };

    /// <summary>Las fechas se guardaron con ToString("O"); sin zona se toman como hora local.</summary>
    private static DateTime? ParseDate(string text) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var value)
            ? value.LocalDateTime
            : null;
}
