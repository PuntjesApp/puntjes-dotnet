using System.Net.Http.Headers;
using System.Text.Json;

namespace Puntjes.Sdk.Http;

internal sealed record ErrorBody(int Status, string Code, string Message, string? RequestId, JsonElement? Details, bool IsJson, string Excerpt)
{
    public const string PlanLimitExceeded = "PLAN_LIMIT_EXCEEDED";

    public const string RateLimited = "RATE_LIMITED";

    public static async Task<ErrorBody> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;
        var text = response.Content is null ? "" : await response.Content.ReadAsStringAsync(cancellationToken);
        var excerpt = text.Length > 200 ? text[..200] : text;
        var fallback = $"The Puntjes API returned HTTP {status}.";

        JsonElement root;
        try
        {
            root = JsonDocument.Parse(text).RootElement;
        }
        catch (JsonException)
        {
            return new ErrorBody(status, "NON_JSON_RESPONSE", fallback, null, null, false, excerpt);
        }

        var error = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.Object ? e : default;

        return new ErrorBody(
            status,
            Text(error, "code") ?? "UNKNOWN_ERROR",
            Text(error, "message") ?? fallback,
            Text(error, "request_id"),
            error.ValueKind == JsonValueKind.Object && error.TryGetProperty("details", out var d) && d.ValueKind == JsonValueKind.Object ? d.Clone() : null,
            true,
            excerpt);
    }

    public static TimeSpan? RetryAfter(RetryConditionHeaderValue? header, DateTimeOffset now)
    {
        if (header?.Delta is { } delta)
        {
            return delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
        }

        if (header?.Date is { } date)
        {
            var wait = date - now;
            return wait < TimeSpan.Zero ? TimeSpan.Zero : wait;
        }

        return null;
    }

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
