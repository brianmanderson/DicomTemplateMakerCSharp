using System.Net;
using System.Text;

namespace TemplateSync.Tests.Fakes;

/// <summary>Scripted HttpMessageHandler: records every request and answers from a queue. No network.</summary>
public sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

    public List<RecordedRequest> Requests { get; } = new();

    public FakeHttpHandler Respond(HttpStatusCode status, string body, Action<HttpResponseMessage>? configure = null)
    {
        _responses.Enqueue(_ =>
        {
            var response = new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            configure?.Invoke(response);
            return response;
        });
        return this;
    }

    public FakeHttpHandler Respond(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responses.Enqueue(responder);
        return this;
    }

    public FakeHttpHandler Throw(Exception exception)
    {
        _responses.Enqueue(_ => throw exception);
        return this;
    }

    public int Remaining => _responses.Count;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string? body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new RecordedRequest(request.Method, request.RequestUri!, body, request.Headers.Authorization?.ToString(), request.Headers.TryGetValues("If-None-Match", out var etags) ? etags.FirstOrDefault() : null));
        if (_responses.Count == 0)
        {
            throw new InvalidOperationException($"Unexpected extra request: {request.Method} {request.RequestUri}");
        }

        return _responses.Dequeue()(request);
    }
}

public sealed record RecordedRequest(HttpMethod Method, Uri Uri, string? Body, string? Authorization, string? IfNoneMatch)
{
    public string Query => Uri.GetDecodedQuery();
}

public static class UriExtensions
{
    public static string GetDecodedQuery(this Uri uri) => Uri.UnescapeDataString(uri.Query);

    public static List<string> QueryValues(this Uri uri, string key)
    {
        return uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .Where(parts => Uri.UnescapeDataString(parts[0]) == key)
            .Select(parts => parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty)
            .ToList();
    }
}
