using System.Net;
using Puntjes.Sdk.Auth;
using Puntjes.Sdk.Exceptions;
using Puntjes.Sdk.Tests.Support;

namespace Puntjes.Sdk.Tests;

public class AuthenticationTests
{
    private const string Ok = """{"data":{}}""";

    [Fact]
    public async Task TwoCallsShareOneGrant()
    {
        var fake = new FakeHandler().Then(HttpStatusCode.OK, Ok).Then(HttpStatusCode.OK, Ok);
        var client = fake.Client();

        await client.Me.GetAsync();
        await client.Me.GetAsync();

        Assert.Equal(1, fake.Grants);
        Assert.All(fake.ApiRequests, r => Assert.Equal("Bearer token-1", r.Authorization));
        Assert.Equal("grant_type=client_credentials&client_id=client-id&client_secret=client-secret", fake.GrantBodies.Single());
    }

    [Fact]
    public async Task ARejectedTokenIsGrantedAgainAndTheCallSentOnceMore()
    {
        var fake = new FakeHandler().ThenError(HttpStatusCode.Unauthorized, "UNAUTHENTICATED").Then(HttpStatusCode.OK, Ok);

        await fake.Client().Me.GetAsync();

        Assert.Equal(2, fake.Grants);
        Assert.Equal(["Bearer token-1", "Bearer token-2"], fake.ApiRequests.Select(r => r.Authorization));
    }

    [Fact]
    public async Task ASecondRejectionReachesTheCaller()
    {
        var fake = new FakeHandler()
            .ThenError(HttpStatusCode.Unauthorized, "UNAUTHENTICATED")
            .ThenError(HttpStatusCode.Unauthorized, "UNAUTHENTICATED")
            .Then(HttpStatusCode.OK, Ok);

        await Assert.ThrowsAsync<AuthenticationException>(() => fake.Client().Me.GetAsync());

        Assert.Equal(2, fake.ApiRequests.Count);
    }

    [Fact]
    public async Task APublicOperationSendsNoToken()
    {
        var fake = new FakeHandler().Then(HttpStatusCode.OK, """{"status":"ok"}""");

        await fake.Client().Health.GetAsync();

        Assert.Equal(0, fake.Grants);
        Assert.Null(fake.ApiRequests.Single().Authorization);
    }

    [Fact]
    public async Task ParallelCallsOnAColdClientShareOneGrant()
    {
        var fake = new FakeHandler { Latency = TimeSpan.FromMilliseconds(20) };
        for (var i = 0; i < 20; i++)
        {
            fake.Then(HttpStatusCode.OK, Ok);
        }

        var client = fake.Client();

        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => client.Me.GetAsync()));

        Assert.Equal(1, fake.Grants);
    }

    [Fact]
    public async Task AStoredTokenIsUsedUntilShortlyBeforeItExpires()
    {
        var store = new InMemoryTokenStore();
        await store.SetAsync(StoreKey(), new AccessToken("stored", DateTimeOffset.UtcNow.AddMinutes(10)), default);
        var fake = new FakeHandler().Then(HttpStatusCode.OK, Ok);

        await fake.Client(new PuntjesOptions { TokenStore = store }).Me.GetAsync();

        Assert.Equal(0, fake.Grants);
        Assert.Equal("Bearer stored", fake.ApiRequests.Single().Authorization);
    }

    [Fact]
    public async Task ATokenAboutToExpireIsReplaced()
    {
        var store = new InMemoryTokenStore();
        await store.SetAsync(StoreKey(), new AccessToken("stale", DateTimeOffset.UtcNow.AddSeconds(10)), default);
        var fake = new FakeHandler().Then(HttpStatusCode.OK, Ok);

        await fake.Client(new PuntjesOptions { TokenStore = store }).Me.GetAsync();

        Assert.Equal(1, fake.Grants);
        Assert.Equal("token-1", (await store.GetAsync(StoreKey(), default))!.Value);
    }

    [Fact]
    public async Task RejectedCredentialsThrowAnAuthenticationException()
    {
        var fake = new FakeHandler
        {
            GrantAnswer = _ => FakeHandler.Json(HttpStatusCode.Unauthorized, """{"error":"invalid_client","error_description":"Client authentication failed"}"""),
        };

        var error = await Assert.ThrowsAsync<AuthenticationException>(() => fake.Client().Me.GetAsync());

        Assert.Equal("Client authentication failed", error.Message);
        Assert.Equal("invalid_client", error.RawCode);
        Assert.Empty(fake.ApiRequests);
    }

    [Fact]
    public async Task ATokenEndpointOutageIsAServerError()
    {
        var fake = new FakeHandler { GrantAnswer = _ => FakeHandler.Error(HttpStatusCode.ServiceUnavailable, "INTERNAL_ERROR") };

        await Assert.ThrowsAsync<ServerException>(() => fake.Client().Me.GetAsync());
    }

    [Fact]
    public async Task ClientsThatShareAStoreKeepTheirOwnToken()
    {
        var store = new InMemoryTokenStore();
        var fake = new FakeHandler().Then(HttpStatusCode.OK, Ok).Then(HttpStatusCode.OK, Ok);

        await fake.Client(new PuntjesOptions { TokenStore = store }, "shop-a").Me.GetAsync();
        await fake.Client(new PuntjesOptions { TokenStore = store }, "shop-b").Me.GetAsync();

        Assert.Equal(2, fake.Grants);
        Assert.Equal(["Bearer token-1", "Bearer token-2"], fake.ApiRequests.Select(r => r.Authorization));
    }

    [Fact]
    public async Task AShortLivedTokenIsReplacedWhenItsOwnLifetimeEnds()
    {
        var fake = new FakeHandler
        {
            GrantAnswer = n => FakeHandler.Json(HttpStatusCode.OK, $$"""{"token_type":"Bearer","expires_in":10,"access_token":"token-{{n}}"}"""),
        }.Then(HttpStatusCode.OK, Ok).Then(HttpStatusCode.OK, Ok);
        var client = fake.Client();

        await client.Me.GetAsync();
        await client.Me.GetAsync();

        Assert.Equal(2, fake.Grants);
    }

    [Fact]
    public async Task ATokenEndpointHiccupIsRetried()
    {
        var fake = new FakeHandler
        {
            GrantAnswer = n => n == 1
                ? FakeHandler.Error(HttpStatusCode.ServiceUnavailable, "INTERNAL_ERROR")
                : FakeHandler.Json(HttpStatusCode.OK, """{"token_type":"Bearer","expires_in":3600,"access_token":"token-ok"}"""),
        }.Then(HttpStatusCode.OK, Ok);

        await fake.Client().Me.GetAsync();

        Assert.Equal(2, fake.Grants);
        Assert.Equal("Bearer token-ok", fake.ApiRequests.Single().Authorization);
    }

    [Fact]
    public async Task ATokenEndpointThatStaysDownIsAServerErrorAfterTheRetries()
    {
        var fake = new FakeHandler { GrantAnswer = _ => FakeHandler.Error(HttpStatusCode.ServiceUnavailable, "INTERNAL_ERROR") };

        await Assert.ThrowsAsync<ServerException>(() => fake.Client().Me.GetAsync());

        Assert.Equal(3, fake.Grants);
    }

    [Fact]
    public async Task ParallelRejectionsShareOneNewGrant()
    {
        var store = new InMemoryTokenStore();
        await store.SetAsync(AnyKey, new AccessToken("old", DateTimeOffset.UtcNow.AddHours(1)), default);
        var fake = new FakeHandler
        {
            ApiLatency = call => call switch { 1 => TimeSpan.FromMilliseconds(50), 2 => TimeSpan.FromMilliseconds(300), _ => TimeSpan.Zero },
            Respond = request => request.Headers.Authorization?.Parameter == "old"
                ? FakeHandler.Error(HttpStatusCode.Unauthorized, "UNAUTHENTICATED")
                : FakeHandler.Json(HttpStatusCode.OK, Ok),
        };
        var client = fake.Client(new PuntjesOptions { TokenStore = new SingleTokenStore(store) });

        await Task.WhenAll(client.Me.GetAsync(), client.Me.GetAsync());

        Assert.Equal(1, fake.Grants);
    }

    private const string AnyKey = "any";

    private static string StoreKey() => TokenKey.For("https://shop.test", "client-id", "client-secret");

    private sealed class SingleTokenStore(InMemoryTokenStore inner) : ITokenStore
    {
        public ValueTask<AccessToken?> GetAsync(string key, CancellationToken cancellationToken) => inner.GetAsync(AnyKey, cancellationToken);

        public ValueTask SetAsync(string key, AccessToken token, CancellationToken cancellationToken) => inner.SetAsync(AnyKey, token, cancellationToken);

        public ValueTask ClearAsync(string key, CancellationToken cancellationToken) => inner.ClearAsync(AnyKey, cancellationToken);
    }
}
