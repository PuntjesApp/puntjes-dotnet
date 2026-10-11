namespace Puntjes.Sdk.Auth;

/// <summary>Keeps the access token between calls, for example in a distributed cache shared by several servers.</summary>
public interface ITokenStore
{
    ValueTask<AccessToken?> GetAsync(CancellationToken cancellationToken);

    ValueTask SetAsync(AccessToken token, CancellationToken cancellationToken);

    ValueTask ClearAsync(CancellationToken cancellationToken);
}

/// <summary>A bearer token and the moment it stops working.</summary>
public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);
