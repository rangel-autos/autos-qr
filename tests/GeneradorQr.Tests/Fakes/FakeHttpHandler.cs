using System.Net;
using System.Text;

namespace GeneradorQr.Tests.Fakes;

/// <summary>
/// Servidor HTTP simulado: responde en orden con lo programado y guarda cada petición (con su cuerpo).
/// </summary>
internal sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

    public List<RecordedRequest> Requests { get; } = [];

    public FakeHttpHandler Respond(HttpStatusCode status, string json = "") =>
        Respond(_ => new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") });

    public FakeHttpHandler Throw(Exception ex) => Respond(_ => throw ex);

    public FakeHttpHandler Respond(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responses.Enqueue(responder);
        return this;
    }

    public HttpClient Client() => new(this);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new RecordedRequest(
            request.Method,
            request.RequestUri!,
            request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase),
            body));

        if (_responses.Count == 0)
        {
            throw new InvalidOperationException($"Petición no esperada: {request.Method} {request.RequestUri}");
        }

        return _responses.Dequeue()(request);
    }
}

internal sealed record RecordedRequest(HttpMethod Method, Uri Uri, IReadOnlyDictionary<string, string> Headers, string? Body)
{
    public string PathAndQuery => Uri.PathAndQuery;
}
