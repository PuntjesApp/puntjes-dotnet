using Puntjes.Sdk.Exceptions;

namespace Puntjes.Sdk.Http;

internal static class ErrorMapping
{
    public static async Task<PuntjesException> ToExceptionAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await ErrorBody.ReadAsync(response, cancellationToken);
        if (!body.IsJson)
        {
            return body.Status >= 500
                ? new ServerException($"The Puntjes API returned HTTP {body.Status} with a non-JSON body, likely a proxy answering for a failing backend: {body.Excerpt}", body.Status, body.Code)
                : new TransportException($"The Puntjes API returned a non-JSON error response (HTTP {body.Status}): {body.Excerpt}");
        }

        var (status, code, message, requestId, details) = (body.Status, body.Code, body.Message, body.RequestId, body.Details);

        return status switch
        {
            429 when code == ErrorBody.PlanLimitExceeded => new PlanLimitExceededException(message, status, code, requestId, details),
            429 => new RateLimitException(message, status, code, requestId, details, ErrorBody.RetryAfter(response.Headers.RetryAfter, DateTimeOffset.UtcNow)),
            422 when code == "VALIDATION_ERROR" => new ValidationException(message, status, code, requestId, details),
            401 => new AuthenticationException(message, status, code, requestId, details),
            403 => new ForbiddenException(message, status, code, requestId, details),
            404 => new NotFoundException(message, status, code, requestId, details),
            409 => new ConflictException(message, status, code, requestId, details),
            >= 500 => new ServerException(message, status, code, requestId, details),
            _ => new PuntjesException(message, status, code, requestId, details),
        };
    }
}
