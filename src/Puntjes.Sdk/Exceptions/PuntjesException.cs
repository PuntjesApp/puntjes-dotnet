using System.Text.Json;
using Puntjes.Sdk.Generated.Errors;

namespace Puntjes.Sdk.Exceptions;

/// <summary>
/// A call the Puntjes API refused. <see cref="ErrorCode"/> is null for a code this version does not know yet;
/// <see cref="RawCode"/> always holds the code as the API sent it.
/// </summary>
public class PuntjesException : Exception
{
    public PuntjesException(string message, int? status = null, string? rawCode = null, string? requestId = null, JsonElement? details = null, Exception? inner = null)
        : base(message, inner)
    {
        Status = status;
        RawCode = rawCode;
        RequestId = requestId;
        Details = details;
        ErrorCode = ErrorCodes.TryParse(rawCode, out var code) ? code : null;
    }

    public int? Status { get; }

    public string? RawCode { get; }

    public ErrorCode? ErrorCode { get; }

    /// <summary>Identifies the request in the Puntjes logs. Quote it when you report a problem.</summary>
    public string? RequestId { get; }

    /// <summary>Extra context; a validation error holds each rejected field and its messages here.</summary>
    public JsonElement? Details { get; }
}
