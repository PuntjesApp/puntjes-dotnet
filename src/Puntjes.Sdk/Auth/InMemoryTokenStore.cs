using System.Collections.Concurrent;

namespace Puntjes.Sdk.Auth;

/// <summary>Keeps the tokens in this process. The default store.</summary>
public sealed class InMemoryTokenStore : ITokenStore
{
    private readonly ConcurrentDictionary<string, AccessToken> _tokens = new();

    public ValueTask<AccessToken?> GetAsync(string key, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_tokens.TryGetValue(key, out var token) ? token : null);

    public ValueTask SetAsync(string key, AccessToken token, CancellationToken cancellationToken)
    {
        _tokens[key] = token;
        return ValueTask.CompletedTask;
    }

    public ValueTask ClearAsync(string key, CancellationToken cancellationToken)
    {
        _tokens.TryRemove(key, out _);
        return ValueTask.CompletedTask;
    }
}
