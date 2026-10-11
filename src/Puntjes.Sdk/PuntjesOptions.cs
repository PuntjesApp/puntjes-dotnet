using Puntjes.Sdk.Auth;

namespace Puntjes.Sdk;

/// <summary>Settings for <see cref="PuntjesClientFactory.Create"/>. Every one has a working default.</summary>
public sealed class PuntjesOptions
{
    /// <summary>The Puntjes host, with or without /api/v1. Defaults to https://puntjes.app.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Retries per request for a call that is safe to repeat. 0 turns retrying off.</summary>
    public int MaxRetries { get; set; } = 2;

    /// <summary>The first wait between retries; it doubles per attempt unless the API sends Retry-After.</summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(0.5);

    /// <summary>Where access tokens are kept, per set of credentials. Defaults to memory in this client.</summary>
    public ITokenStore? TokenStore { get; set; }

    /// <summary>How long one attempt may take before it counts as a network failure. Defaults to 30 seconds.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>The handler that sends the request, for a proxy or a test. Defaults to a new <see cref="HttpClientHandler"/>.</summary>
    public HttpMessageHandler? InnerHandler { get; set; }

    internal Func<TimeSpan, CancellationToken, Task>? Delay { get; set; }
}
