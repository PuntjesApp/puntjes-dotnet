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
        await store.SetAsync(new AccessToken("stored", DateTimeOffset.UtcNow.AddMinutes(10)), default);
        var fake = new FakeHandler().Then(HttpStatusCode.OK, Ok);

        await fake.Client(new PuntjesOptions { TokenStore = store }).Me.GetAsync();

        Assert.Equal(0, fake.Grants);
        Assert.Equal("Bearer stored", fake.ApiRequests.Single().Authorization);
    }

    [Fact]
    public async Task ATokenAboutToExpireIsReplaced()
    {
        var store = new InMemoryTokenStore();
        await store.SetAsync(new AccessToken("stale", DateTimeOffset.UtcNow.AddSeconds(10)), default);
        var fake = new FakeHandler().Then(HttpStatusCode.OK, Ok);

        await fake.Client(new PuntjesOptions { TokenStore = store }).Me.GetAsync();

        Assert.Equal(1, fake.Grants);
        Assert.Equal("token-1", (await store.GetAsync(default))!.Value);
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
}
