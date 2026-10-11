using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Puntjes.Sdk.Auth;
using Puntjes.Sdk.Generated;
using Puntjes.Sdk.Http;

namespace Puntjes.Sdk;

/// <summary>Builds the generated <see cref="PuntjesClient"/> with authentication, retries and typed exceptions.</summary>
public static class PuntjesClientFactory
{
    public const string DefaultBaseUrl = "https://puntjes.app";

    private const string ApiPrefix = "/api/v1";

    private const string TokenPath = "/oauth/token";

    public static PuntjesClient Create(string clientId, string clientSecret, PuntjesOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientSecret);
        options ??= new PuntjesOptions();

        var host = Host(options.BaseUrl ?? DefaultBaseUrl);
        var inner = options.InnerHandler ?? new HttpClientHandler();
        var grants = new HttpMessageInvoker(new RetryHandler(options, repeatEveryMethod: true) { InnerHandler = inner }, disposeHandler: false);
        var tokens = new TokenSource(clientId, clientSecret, new Uri(host + TokenPath), TokenKey.For(host, clientId, clientSecret), options.TokenStore ?? new InMemoryTokenStore(), grants);
        var pipeline = KiotaClientFactory.ChainHandlersCollectionAndGetFirstLink(
            inner,
            new ErrorHandler(),
            new RetryHandler(options),
            new AuthHandler(tokens, new Uri(host + ApiPrefix).AbsolutePath))!;
        var http = new HttpClient(pipeline) { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
        var adapter = new HttpClientRequestAdapter(new AnonymousAuthenticationProvider(), httpClient: http)
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
