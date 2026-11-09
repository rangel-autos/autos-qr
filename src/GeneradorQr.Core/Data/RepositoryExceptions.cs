namespace GeneradorQr.Core.Data;

/// <summary>Otro equipo modificó o eliminó el auto desde que se cargó.</summary>
public sealed class ConcurrencyConflictException(string message = "Otro equipo modificó este auto.") : Exception(message);

/// <summary>No hay conexión a internet o el servidor no respondió a tiempo.</summary>
public sealed class OfflineException(string message = "Sin conexión.", Exception? inner = null) : Exception(message, inner);

/// <summary>El servidor respondió con error (caído o proyecto pausado).</summary>
public sealed class ServerUnavailableException(string message = "No se pudo conectar con el servidor.", Exception? inner = null)
    : Exception(message, inner);

/// <summary>La sesión venció y no se pudo renovar: hay que iniciar sesión otra vez.</summary>
public sealed class AuthRequiredException(string message = "Inicia sesión de nuevo.") : Exception(message);

/// <summary>Correo o contraseña incorrectos.</summary>
public sealed class InvalidCredentialsException(string message = "Correo o contraseña incorrectos.") : Exception(message);
