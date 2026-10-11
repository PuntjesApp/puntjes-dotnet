using System.Text.Json;

namespace Puntjes.Sdk.Exceptions;

/// <summary>HTTP 401: the credentials or the token were refused.</summary>
public sealed class AuthenticationException(string message, int? status = null, string? rawCode = null, string? requestId = null, JsonElement? details = null)
    : PuntjesException(message, status, rawCode, requestId, details);

/// <summary>HTTP 403.</summary>
public sealed class ForbiddenException(string message, int? status = null, string? rawCode = null, string? requestId = null, JsonElement? details = null)
    : PuntjesException(message, status, rawCode, requestId, details);

/// <summary>HTTP 404.</summary>
public sealed class NotFoundException(string message, int? status = null, string? rawCode = null, string? requestId = null, JsonElement? details = null)
    : PuntjesException(message, status, rawCode, requestId, details);

/// <summary>HTTP 409.</summary>
public sealed class ConflictException(string message, int? status = null, string? rawCode = null, string? requestId = null, JsonElement? details = null)
    : PuntjesException(message, status, rawCode, requestId, details);

/// <summary>HTTP 422 with VALIDATION_ERROR; <see cref="PuntjesException.Details"/> holds the fields. Other 422 codes are domain refusals.</summary>
public sealed class ValidationException(string message, int? status = null, string? rawCode = null, string? requestId = null, JsonElement? details = null)
    : PuntjesException(message, status, rawCode, requestId, details)
{
    /// <summary>The messages for one rejected field, or none.</summary>
    public IReadOnlyList<string> ErrorsFor(string field) =>
        Details is { } details && details.TryGetProperty(field, out var messages) && messages.ValueKind == JsonValueKind.Array
            ? messages.EnumerateArray().Where(m => m.ValueKind == JsonValueKind.String).Select(m => m.GetString()!).ToList()
            : [];
}

/// <summary>HTTP 429 with RATE_LIMITED: wait <see cref="RetryAfter"/> and try again.</summary>
public sealed class RateLimitException(string message, int? status = null, string? rawCode = null, string? requestId = null, JsonElement? details = null, TimeSpan? retryAfter = null)
    : PuntjesException(message, status, rawCode, requestId, details)
{
    public TimeSpan? RetryAfter { get; } = retryAfter;
}

/// <summary>HTTP 429 with PLAN_LIMIT_EXCEEDED: the shop's plan is used up. Waiting does not clear it.</summary>
public sealed class PlanLimitExceededException(string message, int? status = null, string? rawCode = null, string? requestId = null, JsonElement? details = null)
    : PuntjesException(message, status, rawCode, requestId, details);

/// <summary>HTTP 5xx.</summary>
public sealed class ServerException(string message, int? status = null, string? rawCode = null, string? requestId = null, JsonElement? details = null)
    : PuntjesException(message, status, rawCode, requestId, details);

/// <summary>No usable answer: the network failed, or something other than the Puntjes API answered.</summary>
public sealed class TransportException(string message, Exception? inner = null)
    : PuntjesException(message, inner: inner);
