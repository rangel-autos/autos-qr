using GeneradorQr.Cloud;

namespace GeneradorQr.Tests;

public sealed class FileSessionStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "gqr-session-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void FileSessionStore_RoundTrip_YClear()
    {
        var store = new FileSessionStore(Path.Combine(_dir, "sub", "session"));

        Assert.Null(store.Load());
        store.Save("refresh-123");
        Assert.Equal("refresh-123", new FileSessionStore(Path.Combine(_dir, "sub", "session")).Load());

        store.Clear();
        Assert.Null(store.Load());
    }

    [Fact]
    public void SupabaseOptions_Load_QuitaDiagonalFinal()
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, "supabase.json");
        File.WriteAllText(path, """{ "Url": "https://demo.supabase.co/", "AnonKey": "k" }""");

        var options = SupabaseOptions.Load(path);

        Assert.Equal("https://demo.supabase.co", options.Url);
        Assert.Equal("k", options.AnonKey);
    }

    [Fact]
    public void SupabaseOptions_Load_FaltaLlave_Lanza()
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, "supabase.json");
        File.WriteAllText(path, """{ "Url": "https://demo.supabase.co" }""");

        Assert.Throws<InvalidOperationException>(() => SupabaseOptions.Load(path));
    }
}
