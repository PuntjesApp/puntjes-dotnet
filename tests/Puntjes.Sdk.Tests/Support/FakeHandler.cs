using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Puntjes.Sdk.Generated;

namespace Puntjes.Sdk.Tests.Support;

public sealed record SentRequest(HttpMethod Method, Uri Uri, string? Body, string? Authorization, string? Accept, string? ContentType);

/// <summary>A transport that answers token grants by itself and replays queued answers for every other call.</summary>
public sealed class FakeHandler : HttpMessageHandler
{
    private readonly ConcurrentQueue<Func<HttpResponseMessage>> _answers = new();
    private int _grants;

    public ConcurrentQueue<SentRequest> Requests { get; } = new();

    public ConcurrentQueue<string> GrantBodies { get; } = new();

    public int Grants => _grants;

    public Func<int, HttpResponseMessage> GrantAnswer { get; set; } =
        n => Json(HttpStatusCode.OK, $$"""{"token_type":"Bearer","expires_in":3600,"access_token":"token-{{n}}"}""");

    public TimeSpan Latency { get; set; } = TimeSpan.Zero;

    public Func<HttpRequestMessage, HttpResponseMessage>? Respond { get; set; }

    public Func<int, TimeSpan>? ApiLatency { get; set; }

    private int _apiCalls;

    public static HttpResponseMessage Hang() => new(HttpStatusCode.Continue);

    public static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    public static HttpResponseMessage Error(HttpStatusCode status, string code, string requestId = "req-1") =>
        Json(status, JsonSerializer.Serialize(new
        {
            error = new { code, message = $"Something about {code}.", status = (int)status, request_id = requestId, details = new { field = new[] { "bad" } } },
        }));

    public FakeHandler Then(Func<HttpResponseMessage> answer)
    {
        _answers.Enqueue(answer);
        return this;
    }

    public FakeHandler Then(HttpStatusCode status, string body) => Then(() => Json(status, body));

    public FakeHandler ThenError(HttpStatusCode status, string code) => Then(() => Error(status, code));

    public FakeHandler ThenThrow(Exception exception) => Then(() => throw exception);

    public IReadOnlyList<SentRequest> ApiRequests => Requests.ToList();

    public PuntjesClient Client(PuntjesOptions? options = null, string clientId = "client-id")
    {
        options ??= new PuntjesOptions();
        options.BaseUrl ??= "https://shop.test";
        options.InnerHandler = this;
        options.Delay ??= (_, _) => Task.CompletedTask;

        return PuntjesClientFactory.Create(clientId, "client-secret", options);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        if (Latency > TimeSpan.Zero)
        {
            await Task.Delay(Latency, cancellationToken);
        }

        if (request.RequestUri!.AbsolutePath == "/oauth/token")
        {
            GrantBodies.Enqueue(body ?? "");
            return GrantAnswer(Interlocked.Increment(ref _grants));
        }

        var call = Interlocked.Increment(ref _apiCalls);
        if (ApiLatency is not null)
        {
            await Task.Delay(ApiLatency(call), cancellationToken);
        }

        Requests.Enqueue(new SentRequest(request.Method, request.RequestUri, body, request.Headers.Authorization?.ToString(), request.Headers.Accept.ToString(), request.Content?.Headers.ContentType?.MediaType));
        if (Respond is not null)
        {
            return Respond(request);
        }

        var response = _answers.TryDequeue(out var answer)
            ? answer()
            : throw new InvalidOperationException($"No answer queued for {request.Method} {request.RequestUri}.");
        if (response.StatusCode == HttpStatusCode.Continue)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        return response;
    }
}
