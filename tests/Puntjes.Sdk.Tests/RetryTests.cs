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
    public async Task PutPatchAndDeleteRetry()
    {
        var fake = new FakeHandler()
            .ThenError(HttpStatusCode.ServiceUnavailable, "INTERNAL_ERROR").Then(HttpStatusCode.OK, Ok)
            .ThenError(HttpStatusCode.ServiceUnavailable, "INTERNAL_ERROR").Then(HttpStatusCode.OK, Ok)
            .ThenError(HttpStatusCode.ServiceUnavailable, "INTERNAL_ERROR").Then(() => new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = Client(fake);

        await client.Products["p1"].PutAsync(new UpsertProductData { Name = "Bread" });
        await client.Products["p1"].PatchAsync(new UpdateProductData { Name = "Rye" });
        await client.Products["p1"].DeleteAsync();

        Assert.Equal(
            [HttpMethod.Put, HttpMethod.Put, HttpMethod.Patch, HttpMethod.Patch, HttpMethod.Delete, HttpMethod.Delete],
            fake.ApiRequests.Select(r => r.Method));
    }

    [Fact]
    public async Task ARetriedPostKeepsItsContentType()
    {
        var fake = new FakeHandler().ThenError(HttpStatusCode.ServiceUnavailable, "INTERNAL_ERROR").Then(HttpStatusCode.Created, Ok);

        await Record(Client(fake), "key-1");

        Assert.Equal(["application/json", "application/json"], fake.ApiRequests.Select(r => r.ContentType));
    }

    [Fact]
    public async Task ARateLimitExceptionSaysHowLongToWait()
    {
        var fake = new FakeHandler().Then(() =>
        {
            var response = FakeHandler.Error(HttpStatusCode.TooManyRequests, "RATE_LIMITED");
            response.Headers.Add("Retry-After", "7");
            return response;
        });

        var error = await Assert.ThrowsAsync<RateLimitException>(() => Client(fake, maxRetries: 0).Me.GetAsync());

        Assert.Equal(TimeSpan.FromSeconds(7), error.RetryAfter);
    }

    [Fact]
    public async Task AGetThatHangsTimesOutAndIsRetried()
    {
        var fake = new FakeHandler().Then(FakeHandler.Hang).Then(HttpStatusCode.OK, Ok);

        await fake.Client(new PuntjesOptions { Timeout = TimeSpan.FromMilliseconds(100), Delay = (_, _) => Task.CompletedTask }).Me.GetAsync();

        Assert.Equal(2, fake.ApiRequests.Count);
    }

    [Fact]
    public async Task APostWithoutAKeyThatHangsIsATransportException()
    {
        var fake = new FakeHandler().Then(FakeHandler.Hang).Then(HttpStatusCode.Created, Ok);
        var client = fake.Client(new PuntjesOptions { Timeout = TimeSpan.FromMilliseconds(100) });

        var error = await Assert.ThrowsAsync<TransportException>(() => Record(client, null));

        Assert.Contains("did not answer within", error.Message);
        Assert.Single(fake.ApiRequests);
    }

    [Fact]
    public async Task ABodyThatBreaksWhileReadingIsRetried()
    {
        var fake = new FakeHandler()
            .Then(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new BrokenContent() })
            .Then(HttpStatusCode.OK, Ok);

        await Client(fake).Me.GetAsync();

        Assert.Equal(2, fake.ApiRequests.Count);
    }

    [Fact]
    public async Task ACallerCancellationIsNotATimeout()
    {
        var fake = new FakeHandler().Then(FakeHandler.Hang);
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fake.Client().Me.GetAsync(cancellationToken: cancel.Token));

        Assert.Single(fake.ApiRequests);
    }

    private sealed class BrokenContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context) =>
            throw new HttpRequestException("connection reset while reading the body");

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
