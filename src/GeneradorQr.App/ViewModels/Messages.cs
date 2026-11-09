namespace GeneradorQr.App.ViewModels;

/// <summary>Textos que ve el usuario (spec, sección 5).</summary>
internal static class Messages
{
    public const string Offline = "Sin conexión — reintentando…";
    public const string OfflineLogin = "Sin conexión. Revisa el internet e intenta de nuevo.";
    public const string Conflict = "Otro equipo modificó este auto. Se recargaron los datos más recientes.";
    public const string InvalidCredentials = "Correo o contraseña incorrectos.";
    public const string Server = "No se pudo conectar con el servidor.";
    public const string ServerHint = Server + " Si no se ha usado en una semana, reactiva el proyecto en el panel de Supabase.";
    public const string EmptyLogin = "Escribe el correo y la contraseña.";
}
