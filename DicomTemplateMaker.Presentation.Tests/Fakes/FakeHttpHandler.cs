using System.Net;
using System.Text;

namespace DicomTemplateMaker.Presentation.Tests.Fakes;

public sealed record RecordedRequest(HttpMethod Method, Uri Uri, string? Authorization);

/// <summary>Scripted HttpMessageHandler: records every request and answers from a queue. No network.</summary>
public sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> responses = new();

    public List<RecordedRequest> Requests { get; } = new();

    public FakeHttpHandler Respond(HttpStatusCode status, string body)
    {
        responses.Enqueue(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(new RecordedRequest(request.Method, request.RequestUri!, request.Headers.Authorization?.ToString()));
        if (responses.Count == 0)
        {
            throw new InvalidOperationException($"Unexpected extra request: {request.Method} {request.RequestUri}");
        }

        return Task.FromResult(responses.Dequeue());
    }
}
