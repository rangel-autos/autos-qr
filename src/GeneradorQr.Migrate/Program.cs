using GeneradorQr.Cloud;
using GeneradorQr.Core.Data;
using GeneradorQr.Migrate;

// Uso: GeneradorQr.Migrate <ruta vehiculos.db> <supabase.json> [--force]
if (args.Length < 2)
{
    Console.WriteLine("Uso: GeneradorQr.Migrate <ruta vehiculos.db> <supabase.json> [--force]");
    Console.WriteLine(@"La base de la versión de Windows está en %LOCALAPPDATA%\GeneradorQrAutos\vehiculos.db");
    return 2;
}

var dbPath = args[0];
var configPath = args[1];
var force = args.Contains("--force", StringComparer.OrdinalIgnoreCase);

try
{
    var local = SqliteReader.ReadAll(dbPath);
    Console.WriteLine($"Autos en la base local: {local.Count}");

    var options = SupabaseOptions.Load(configPath);
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    var auth = new SupabaseAuth(http, options, new MemorySessionStore(), TimeProvider.System);

    Console.Write("Correo de la cuenta: ");
    var email = Console.ReadLine() ?? string.Empty;
    Console.Write("Contraseña: ");
    var password = ReadPassword();

    await auth.SignInAsync(email.Trim(), password, CancellationToken.None);

    var repository = new SupabaseVehicleRepository(http, options, auth);
    var migrator = new Migrator(repository, repository.InsertMigratedAsync);
    var count = await migrator.RunAsync(local, force, CancellationToken.None);

    Console.WriteLine($"Listo: se subieron {count} autos.");
    await auth.SignOutAsync(CancellationToken.None);
    return 0;
}
catch (Exception ex) when (ex is InvalidOperationException or InvalidCredentialsException or OfflineException
                               or ServerUnavailableException or FileNotFoundException)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}

static string ReadPassword()
{
    if (Console.IsInputRedirected)
    {
        return Console.ReadLine() ?? string.Empty;
    }

    var chars = new List<char>();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter)
        {
            Console.WriteLine();
            return new string(chars.ToArray());
        }

        if (key.Key == ConsoleKey.Backspace)
        {
            if (chars.Count > 0) chars.RemoveAt(chars.Count - 1);
        }
        else if (!char.IsControl(key.KeyChar))
        {
            chars.Add(key.KeyChar);
        }
    }
}

/// <summary>La herramienta no guarda la sesión en disco.</summary>
internal sealed class MemorySessionStore : ISessionStore
{
    private string? _token;
    public string? Load() => _token;
    public void Save(string refreshToken) => _token = refreshToken;
    public void Clear() => _token = null;
}
