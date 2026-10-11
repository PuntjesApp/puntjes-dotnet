namespace Puntjes.Sdk.Auth;

/// <summary>
/// Keeps access tokens between calls, for example in a distributed cache shared by several servers.
/// The key names the credentials a token belongs to, so one store can serve the clients of many shops.
/// </summary>
public interface ITokenStore
{
    ValueTask<AccessToken?> GetAsync(string key, CancellationToken cancellationToken);

    ValueTask SetAsync(string key, AccessToken token, CancellationToken cancellationToken);

    ValueTask ClearAsync(string key, CancellationToken cancellationToken);
}

/// <summary>A bearer token and the moment it stops working.</summary>
public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);
