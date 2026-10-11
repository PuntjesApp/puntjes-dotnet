using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Puntjes.Sdk.Generated;
using Puntjes.Sdk.Http;

namespace Puntjes.Sdk;

/// <summary>Builds the generated <see cref="PuntjesClient"/> with authentication, retries and typed exceptions.</summary>
public static class PuntjesClientFactory
{
    public const string DefaultBaseUrl = "https://puntjes.app";

    private const string ApiPrefix = "/api/v1";

    public static PuntjesClient Create(string clientId, string clientSecret, PuntjesOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientSecret);
        options ??= new PuntjesOptions();

        var host = Host(options.BaseUrl ?? DefaultBaseUrl);
        var pipeline = KiotaClientFactory.ChainHandlersCollectionAndGetFirstLink(
            options.InnerHandler ?? new HttpClientHandler(),
            new ErrorHandler(),
            new RetryHandler(options),
            new AuthHandler())!;
        var adapter = new HttpClientRequestAdapter(new AnonymousAuthenticationProvider(), httpClient: new HttpClient(pipeline))
        {
            BaseUrl = host + ApiPrefix,
        };

        return new PuntjesClient(adapter);
    }

    private static string Host(string baseUrl)
    {
        var trimmed = baseUrl.TrimEnd('/');

        return trimmed.EndsWith(ApiPrefix, StringComparison.OrdinalIgnoreCase) ? trimmed[..^ApiPrefix.Length] : trimmed;
    }
}
