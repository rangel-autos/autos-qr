namespace GeneradorQr.App;

/// <summary>Rutas de la app en Windows y macOS.</summary>
internal static class AppPaths
{
    /// <summary>Windows: %LOCALAPPDATA%\GeneradorQR · macOS: ~/Library/Application Support/GeneradorQR</summary>
    public static string DataDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GeneradorQR");

    public static string SessionFile => Path.Combine(DataDir, "session");

    public static string LogFile => Path.Combine(DataDir, "errores.log");

    public static string BackupDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GeneradorQR", "respaldos");

    /// <summary>
    /// supabase.json junto al ejecutable; dentro de un .app de macOS va en Contents/Resources.
    /// </summary>
    public static string ConfigFile
    {
        get
        {
            var baseDir = AppContext.BaseDirectory;
            var macResources = Path.GetFullPath(Path.Combine(baseDir, "..", "Resources", "supabase.json"));
            return File.Exists(macResources) ? macResources : Path.Combine(baseDir, "supabase.json");
        }
    }
}
