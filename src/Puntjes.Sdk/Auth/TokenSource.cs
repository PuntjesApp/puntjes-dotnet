using System.Text.Json;
using Puntjes.Sdk.Exceptions;
using Puntjes.Sdk.Http;

namespace Puntjes.Sdk.Auth;

internal sealed class TokenSource(string clientId, string clientSecret, Uri tokenUrl, string key, ITokenStore store, HttpMessageInvoker grants)
{
    private static readonly TimeSpan Leeway = TimeSpan.FromSeconds(30);

    private readonly SemaphoreSlim _grant = new(1, 1);

    public async Task<string> GetAsync(CancellationToken cancellationToken)
    {
        if (Usable(await store.GetAsync(key, cancellationToken)) is { } token)
        {
            return token;
        }

        await _grant.WaitAsync(cancellationToken);
        try
        {
            if (Usable(await store.GetAsync(key, cancellationToken)) is { } granted)
            {
                return granted;
            }

            var fresh = await GrantAsync(cancellationToken);
            await store.SetAsync(key, fresh, cancellationToken);

            return fresh.Value;
        }
        finally
        {
            _grant.Release();
        }
    }

    public async Task ForgetAsync(string rejected, CancellationToken cancellationToken)
    {
        await _grant.WaitAsync(cancellationToken);
        try
        {
            if ((await store.GetAsync(key, cancellationToken))?.Value == rejected)
            {
                await store.ClearAsync(key, cancellationToken);
            }
        }
        finally
        {
            _grant.Release();
        }
    }

    private static string? Usable(AccessToken? token) =>
        token is not null && token.ExpiresAt - Leeway > DateTimeOffset.UtcNow ? token.Value : null;

    private async Task<AccessToken> GrantAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
            }),
        };
        request.Headers.Accept.ParseAdd("application/json");
        var issuedAt = DateTimeOffset.UtcNow;

        HttpResponseMessage sent;
        try
        {
            sent = await grants.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException e)
        {
            throw new TransportException($"The Puntjes token endpoint could not be reached: {e.Message}", e);
        }

        using var response = sent;
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if ((int)response.StatusCode is 400 or 401)
        {
            throw Rejected(text, (int)response.StatusCode);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw await ErrorMapping.ToExceptionAsync(response, cancellationToken);
        }

        try
        {
            var root = JsonDocument.Parse(text).RootElement;
            var value = root.GetProperty("access_token").GetString();
            var expiresIn = root.TryGetProperty("expires_in", out var e) && e.TryGetInt32(out var seconds) ? seconds : 3600;

            return string.IsNullOrEmpty(value)
                ? throw new TransportException("The Puntjes token endpoint returned no access_token.")
                : new AccessToken(value, issuedAt.AddSeconds(expiresIn));
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new TransportException("The Puntjes token endpoint returned no usable token.", e);
        }
    }

    private static AuthenticationException Rejected(string text, int status)
    {
        string? code = null;
        string? description = null;
        try
        {
            var root = JsonDocument.Parse(text).RootElement;
            code = root.TryGetProperty("error", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
            description = root.TryGetProperty("error_description", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() : null;
        }
        catch (JsonException)
        {
        }

        return new AuthenticationException(description ?? "The Puntjes API refused the client credentials.", status, code);
    }
}
