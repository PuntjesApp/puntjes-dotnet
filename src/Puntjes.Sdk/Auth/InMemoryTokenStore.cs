namespace Puntjes.Sdk.Auth;

/// <summary>Keeps the token in this process. The default store.</summary>
public sealed class InMemoryTokenStore : ITokenStore
{
    private volatile AccessToken? _token;

    public ValueTask<AccessToken?> GetAsync(CancellationToken cancellationToken) => ValueTask.FromResult(_token);

    public ValueTask SetAsync(AccessToken token, CancellationToken cancellationToken)
    {
        _token = token;
        return ValueTask.CompletedTask;
    }

    public ValueTask ClearAsync(CancellationToken cancellationToken)
    {
        _token = null;
        return ValueTask.CompletedTask;
    }
}
