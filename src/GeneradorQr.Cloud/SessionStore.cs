namespace GeneradorQr.Cloud;

/// <summary>Dónde se guarda el refresh token (nunca la contraseña).</summary>
public interface ISessionStore
{
    string? Load();
    void Save(string refreshToken);
    void Clear();
}

public sealed class FileSessionStore(string path) : ISessionStore
{
    public string? Load()
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var token = File.ReadAllText(path).Trim();
        return token.Length == 0 ? null : token;
    }

    public void Save(string refreshToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, refreshToken);

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    public void Clear()
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
