namespace GeneradorQr.App;

/// <summary>Registro de errores en un archivo local para revisar fallas después.</summary>
internal static class Log
{
    private static readonly Lock Gate = new();

    public static void Error(Exception ex, string context)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(AppPaths.DataDir);
                File.AppendAllText(AppPaths.LogFile,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
            }
        }
        catch (IOException)
        {
            // Si no se puede escribir el log no hay nada más que hacer; la app sigue.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
