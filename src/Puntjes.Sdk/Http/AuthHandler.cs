using System.Net;
using System.Net.Http.Headers;
using Puntjes.Sdk.Auth;
using Puntjes.Sdk.Generated.Security;

namespace Puntjes.Sdk.Http;

internal sealed class AuthHandler(TokenSource tokens, string apiPath) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (IsPublic(request))
        {
            return await base.SendAsync(request, cancellationToken);
        }

        var token = await tokens.GetAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        response.Dispose();
        await tokens.ForgetAsync(token, cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokens.GetAsync(cancellationToken));

        return await base.SendAsync(request, cancellationToken);
    }

    private bool IsPublic(HttpRequestMessage request)
    {
        var path = request.RequestUri!.AbsolutePath;

        return path.StartsWith(apiPath, StringComparison.Ordinal)
            && PublicOperations.Matches(request.Method.Method, path[apiPath.Length..]);
    }
}
