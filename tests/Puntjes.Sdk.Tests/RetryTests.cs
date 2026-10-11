using System.Net;
using Puntjes.Sdk.Exceptions;
using Puntjes.Sdk.Generated;
using Puntjes.Sdk.Generated.Models;
using Puntjes.Sdk.Tests.Support;

namespace Puntjes.Sdk.Tests;

public class RetryTests
{
    private const string Ok = """{"data":{}}""";

    private readonly List<TimeSpan> _waits = [];

    private PuntjesClient Client(FakeHandler fake, int maxRetries = 2) => fake.Client(new PuntjesOptions
    {
        MaxRetries = maxRetries,
        Delay = (wait, _) =>
        {
            _waits.Add(wait);
            return Task.CompletedTask;
        },
    });

    private static Task Record(PuntjesClient client, string? idempotencyKey) =>
        client.Transactions.PostAsync(new CreateTransactionData { Identifier = "c1", TotalAmount = 100, IdempotencyKey = idempotencyKey });

    [Fact]
    public async Task AGetRetriesAServerErrorAfterTheFirstWait()
    {
        var fake = new FakeHandler().ThenError(HttpStatusCode.ServiceUnavailable, "INTERNAL_ERROR").Then(HttpStatusCode.OK, Ok);

        await Client(fake).Me.GetAsync();

        Assert.Equal(2, fake.ApiRequests.Count);
        Assert.Equal([TimeSpan.FromSeconds(0.5)], _waits);
    }

    [Fact]
    public async Task TheWaitDoublesAndTheLimitHolds()
    {
        var fake = new FakeHandler()
            .ThenError(HttpStatusCode.InternalServerError, "INTERNAL_ERROR")
            .ThenError(HttpStatusCode.InternalServerError, "INTERNAL_ERROR")
            .ThenError(HttpStatusCode.InternalServerError, "INTERNAL_ERROR");

        await Assert.ThrowsAsync<ServerException>(() => Client(fake).Me.GetAsync());

        Assert.Equal(3, fake.ApiRequests.Count);
        Assert.Equal([TimeSpan.FromSeconds(0.5), TimeSpan.FromSeconds(1)], _waits);
    }

    [Fact]
    public async Task APostWithAnIdempotencyKeyRetries()
    {
        var fake = new FakeHandler().ThenError(HttpStatusCode.ServiceUnavailable, "INTERNAL_ERROR").Then(HttpStatusCode.Created, Ok);

        await Record(Client(fake), "key-1");

        Assert.Equal(2, fake.ApiRequests.Count);
        Assert.All(fake.ApiRequests, r => Assert.Contains("\"idempotency_key\":\"key-1\"", r.Body));
    }

    [Fact]
    public async Task APostWithoutAnIdempotencyKeyIsSentOnce()
    {
        var fake = new FakeHandler().ThenError(HttpStatusCode.ServiceUnavailable, "INTERNAL_ERROR").Then(HttpStatusCode.Created, Ok);

        await Assert.ThrowsAsync<ServerException>(() => Record(Client(fake), null));

        Assert.Single(fake.ApiRequests);
    }

    [Fact]
    public async Task APostWithoutAnIdempotencyKeyIsSentOnceOnANetworkFailure()
    {
        var fake = new FakeHandler().ThenThrow(new HttpRequestException("reset")).Then(HttpStatusCode.Created, Ok);

        await Assert.ThrowsAsync<TransportException>(() => Record(Client(fake), null));

        Assert.Single(fake.ApiRequests);
    }

    [Fact]
    public async Task AGetRetriesANetworkFailure()
    {
        var fake = new FakeHandler().ThenThrow(new HttpRequestException("reset")).Then(HttpStatusCode.OK, Ok);

        await Client(fake).Me.GetAsync();

        Assert.Equal(2, fake.ApiRequests.Count);
    }

    [Fact]
    public async Task ARateLimitWaitsWhatRetryAfterSays()
    {
        var fake = new FakeHandler()
            .Then(() =>
            {
                var response = FakeHandler.Error(HttpStatusCode.TooManyRequests, "RATE_LIMITED");
                response.Headers.Add("Retry-After", "3");
                return response;
            })
            .Then(HttpStatusCode.OK, Ok);

        await Client(fake).Me.GetAsync();

        Assert.Equal([TimeSpan.FromSeconds(3)], _waits);
    }

    [Fact]
    public async Task ARetryAfterDateInThePastWaitsNothing()
    {
        var fake = new FakeHandler()
            .Then(() =>
            {
                var response = FakeHandler.Error(HttpStatusCode.TooManyRequests, "RATE_LIMITED");
                response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(DateTimeOffset.UtcNow.AddMinutes(-5));
                return response;
            })
            .Then(HttpStatusCode.OK, Ok);

        await Client(fake).Me.GetAsync();

        Assert.Equal([TimeSpan.Zero], _waits);
    }

    [Fact]
    public async Task APlanLimitIsNeverRetried()
    {
        var fake = new FakeHandler().ThenError(HttpStatusCode.TooManyRequests, "PLAN_LIMIT_EXCEEDED").Then(HttpStatusCode.OK, Ok);

        await Assert.ThrowsAsync<PlanLimitExceededException>(() => Client(fake).Me.GetAsync());

        Assert.Single(fake.ApiRequests);
    }

    [Fact]
    public async Task AClientErrorIsNeverRetried()
    {
        var fake = new FakeHandler().ThenError(HttpStatusCode.NotFound, "CUSTOMER_NOT_FOUND").Then(HttpStatusCode.OK, Ok);

        await Assert.ThrowsAsync<NotFoundException>(() => Client(fake).Me.GetAsync());

        Assert.Single(fake.ApiRequests);
    }

    [Fact]
    public async Task ZeroRetriesSendsOnce()
    {
        var fake = new FakeHandler().ThenError(HttpStatusCode.ServiceUnavailable, "INTERNAL_ERROR").Then(HttpStatusCode.OK, Ok);

        await Assert.ThrowsAsync<ServerException>(() => Client(fake, maxRetries: 0).Me.GetAsync());

        Assert.Single(fake.ApiRequests);
    }

    [Fact]
    public async Task PutAndDeleteRetry()
    {
        var fake = new FakeHandler()
            .ThenError(HttpStatusCode.ServiceUnavailable, "INTERNAL_ERROR").Then(HttpStatusCode.OK, Ok)
            .ThenError(HttpStatusCode.ServiceUnavailable, "INTERNAL_ERROR").Then(() => new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = Client(fake);

        await client.Products["p1"].PutAsync(new UpsertProductData { Name = "Bread" });
        await client.Products["p1"].DeleteAsync();

        Assert.Equal(
            [HttpMethod.Put, HttpMethod.Put, HttpMethod.Delete, HttpMethod.Delete],
            fake.ApiRequests.Select(r => r.Method));
    }
}
