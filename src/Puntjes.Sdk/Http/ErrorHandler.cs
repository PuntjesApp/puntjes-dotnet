using Puntjes.Sdk.Exceptions;

namespace Puntjes.Sdk.Http;

internal sealed class ErrorHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await base.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException e)
        {
            throw new TransportException($"The Puntjes API could not be reached: {e.Message}", e);
        }

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        using (response)
        {
            throw await ErrorMapping.ToExceptionAsync(response, cancellationToken);
        }
    }
}
