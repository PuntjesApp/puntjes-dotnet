using System.Text.Json;

namespace Puntjes.Sdk.Http;

internal sealed class RetryHandler(PuntjesOptions options) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken);
        var headers = request.Content?.Headers.ToList();
        var retries = IsSafeToRepeat(request.Method, body) ? options.MaxRetries : 0;

        for (var attempt = 0; ; attempt++)
        {
            if (body is not null)
            {
                request.Content = new ByteArrayContent(body);
                foreach (var header in headers!)
                {
                    request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            HttpResponseMessage response;
            try
            {
                response = await base.SendAsync(request, cancellationToken);
            }
            catch (HttpRequestException) when (attempt < retries)
            {
                await Wait(attempt, null, cancellationToken);
                continue;
            }

            if (attempt >= retries || !await IsTransient(response, cancellationToken))
            {
                return response;
            }

            var retryAfter = ErrorBody.RetryAfter(response.Headers.RetryAfter, DateTimeOffset.UtcNow);
            response.Dispose();
            await Wait(attempt, retryAfter, cancellationToken);
        }
    }

    private static bool IsSafeToRepeat(HttpMethod method, byte[]? body)
    {
        if (method == HttpMethod.Get || method == HttpMethod.Head || method == HttpMethod.Put || method == HttpMethod.Patch || method == HttpMethod.Delete)
        {
            return true;
        }

        return method == HttpMethod.Post && CarriesIdempotencyKey(body);
    }

    private static bool CarriesIdempotencyKey(byte[]? body)
    {
        if (body is null || body.Length == 0)
        {
            return false;
        }

        try
        {
            using var json = JsonDocument.Parse(body);
            return json.RootElement.ValueKind == JsonValueKind.Object
                && json.RootElement.TryGetProperty("idempotency_key", out var key)
                && key.ValueKind == JsonValueKind.String
                && !string.IsNullOrEmpty(key.GetString());
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static async Task<bool> IsTransient(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;
        if (status >= 500)
        {
            return true;
        }

        if (status != 429)
        {
            return false;
        }

        await response.Content.LoadIntoBufferAsync();

        return (await ErrorBody.ReadAsync(response, cancellationToken)).Code == ErrorBody.RateLimited;
    }

    private Task Wait(int attempt, TimeSpan? retryAfter, CancellationToken cancellationToken)
    {
        var wait = retryAfter ?? options.RetryBaseDelay * Math.Pow(2, attempt);

        return (options.Delay ?? Task.Delay)(wait, cancellationToken);
    }
}
