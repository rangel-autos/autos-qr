using GeneradorQr.Core.Models;
using GeneradorQr.Migrate;
using GeneradorQr.Tests.Fakes;
using Microsoft.Data.Sqlite;

namespace GeneradorQr.Tests;

public sealed class MigratorTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "gqr-migrate-" + Guid.NewGuid().ToString("N"));

    public MigratorTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private string CreateDb(bool withNewColumns)
    {
        var path = Path.Combine(_dir, "vehiculos.db");
        using var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();
        using var command = connection.CreateCommand();
        // Mismo DDL que GeneradorQrAutos/Data/VehicleRepository.EnsureDatabase.
        command.CommandText = """
            CREATE TABLE vehiculos (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                marca TEXT NOT NULL, modelo TEXT NOT NULL, anio INTEGER NOT NULL, kilometraje INTEGER NOT NULL,
                precio TEXT NOT NULL, procedencia TEXT NOT NULL, motor_litros TEXT NOT NULL,
                configuracion_motor TEXT NOT NULL, observaciones TEXT NOT NULL DEFAULT '', creado_en TEXT NOT NULL);
            """;
        command.ExecuteNonQuery();

        if (withNewColumns)
        {
            command.CommandText = """
                ALTER TABLE vehiculos ADD COLUMN vendido INTEGER NOT NULL DEFAULT 0;
                ALTER TABLE vehiculos ADD COLUMN actualizado_en TEXT NULL;
                INSERT INTO vehiculos (marca, modelo, anio, kilometraje, precio, procedencia, motor_litros,
                    configuracion_motor, observaciones, creado_en, vendido, actualizado_en)
                VALUES ('Ford', 'Lobo', 2020, 45000, '495000.50', 'Nacional', '5.0 L', 'V8', 'Único dueño',
                    '2026-10-01T09:30:00.0000000-06:00', 1, '2026-10-02T10:00:00.0000000-06:00'),
                       ('Nissan', 'Versa', 2021, 10000, '250000.00', 'Nacional', '1.6 L', '4 cilindros', '',
                    '2026-10-03T11:00:00.0000000', 0, NULL);
                """;
        }
        else
        {
            command.CommandText = """
                INSERT INTO vehiculos (marca, modelo, anio, kilometraje, precio, procedencia, motor_litros,
                    configuracion_motor, observaciones, creado_en)
                VALUES ('Ford', 'Lobo', 2020, 45000, '495000.50', 'Nacional', '5.0 L', 'V8', '', '2026-10-01T09:30:00.0000000');
                """;
        }

        command.ExecuteNonQuery();
        return path;
    }

    [Fact]
    public void SqliteReader_LeeEsquemaActual()
    {
        var all = SqliteReader.ReadAll(CreateDb(withNewColumns: true));

        Assert.Equal(2, all.Count);
        var lobo = all[0];
        Assert.Equal("Ford", lobo.Marca);
        Assert.Equal(495000.50m, lobo.Precio);
        Assert.True(lobo.Vendido);
        Assert.Equal("Único dueño", lobo.Observaciones);
        Assert.NotNull(lobo.ActualizadoEn);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 9, 30, 0, TimeSpan.FromHours(-6)).LocalDateTime, lobo.CreadoEn);
    }

    [Fact]
    public void SqliteReader_BaseAntiguaSinColumnasNuevas()
    {
        var v = Assert.Single(SqliteReader.ReadAll(CreateDb(withNewColumns: false)));
        Assert.False(v.Vendido);
        Assert.Null(v.ActualizadoEn);
    }

    [Fact]
    public async Task Migrar_TablaRemotaConDatos_SeNiegaSinForce()
    {
        var repo = new FakeRepository();
        repo.Seed(new VehicleRecord { Marca = "Ya", Modelo = "Existe" });
        var migrator = new Migrator(repo, repo.InsertAsync);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            migrator.RunAsync(SqliteReader.ReadAll(CreateDb(true)), force: false, default));

        Assert.Equal("La base en la nube ya tiene 1 autos. Usa --force para migrar de todos modos.", ex.Message);
        Assert.Single(repo.Rows);
    }

    [Fact]
    public async Task Migrar_ConForce_InsertaAunqueHayaDatos()
    {
        var repo = new FakeRepository();
        repo.Seed(new VehicleRecord { Marca = "Ya", Modelo = "Existe" });
        var migrator = new Migrator(repo, repo.InsertAsync);

        var count = await migrator.RunAsync(SqliteReader.ReadAll(CreateDb(true)), force: true, default);

        Assert.Equal(2, count);
        Assert.Equal(3, repo.Rows.Count);
    }

    [Fact]
    public async Task Migrar_ConservaVendidoYCreadoEn_EnOrdenDeId()
    {
        var repo = new FakeRepository();
        var local = SqliteReader.ReadAll(CreateDb(true));
        var migrator = new Migrator(repo, repo.InsertAsync);

        var count = await migrator.RunAsync(local.Reverse().ToList(), force: false, default);

        Assert.Equal(2, count);
        Assert.Equal(["Lobo", "Versa"], repo.Rows.Select(r => r.Modelo));
        Assert.True(repo.Rows[0].Vendido);
        Assert.Equal(local[0].CreadoEn, repo.Rows[0].CreadoEn);
    }
}
