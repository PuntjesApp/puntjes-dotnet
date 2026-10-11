namespace Puntjes.Sdk.Http;

internal sealed class RetryHandler(PuntjesOptions options) : DelegatingHandler
{
    private readonly PuntjesOptions _options = options;
}
