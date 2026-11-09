using System.Text.Json;

namespace GeneradorQr.Cloud;

/// <summary>URL del proyecto y llave pública (anon). Nunca la contraseña de la base ni la service key.</summary>
public sealed record SupabaseOptions(string Url, string AnonKey)
{
    public static SupabaseOptions Load(string jsonPath)
    {
        var json = File.ReadAllText(jsonPath);
        var raw = JsonSerializer.Deserialize<Raw>(json) ?? new Raw(null, null);

        if (string.IsNullOrWhiteSpace(raw.Url) || string.IsNullOrWhiteSpace(raw.AnonKey))
        {
            throw new InvalidOperationException($"El archivo {jsonPath} debe tener \"Url\" y \"AnonKey\".");
        }

        return new SupabaseOptions(raw.Url.Trim().TrimEnd('/'), raw.AnonKey.Trim());
    }

    private sealed record Raw(string? Url, string? AnonKey);
}
