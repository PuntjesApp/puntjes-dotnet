using System.Net;
using Puntjes.Sdk.Exceptions;
using Puntjes.Sdk.Generated.Errors;
using Puntjes.Sdk.Tests.Support;

namespace Puntjes.Sdk.Tests;

public class ErrorMappingTests
{
    public static TheoryData<HttpStatusCode, string, Type, ErrorCode> Families => new()
    {
        { HttpStatusCode.Unauthorized, "UNAUTHENTICATED", typeof(AuthenticationException), ErrorCode.Unauthenticated },
        { HttpStatusCode.Forbidden, "FORBIDDEN", typeof(ForbiddenException), ErrorCode.Forbidden },
        { HttpStatusCode.NotFound, "CUSTOMER_NOT_FOUND", typeof(NotFoundException), ErrorCode.CustomerNotFound },
        { HttpStatusCode.Conflict, "IDENTIFIER_DUPLICATE", typeof(ConflictException), ErrorCode.IdentifierDuplicate },
        { HttpStatusCode.UnprocessableEntity, "VALIDATION_ERROR", typeof(ValidationException), ErrorCode.ValidationError },
        { HttpStatusCode.UnprocessableEntity, "INSUFFICIENT_BALANCE", typeof(PuntjesException), ErrorCode.InsufficientBalance },
        { HttpStatusCode.TooManyRequests, "RATE_LIMITED", typeof(RateLimitException), ErrorCode.RateLimited },
        { HttpStatusCode.TooManyRequests, "PLAN_LIMIT_EXCEEDED", typeof(PlanLimitExceededException), ErrorCode.PlanLimitExceeded },
        { HttpStatusCode.InternalServerError, "INTERNAL_ERROR", typeof(ServerException), ErrorCode.InternalError },
    };

    [Theory]
    [MemberData(nameof(Families))]
    public async Task EachStatusAndCodeThrowsItsFamily(HttpStatusCode status, string code, Type family, ErrorCode expected)
    {
        var fake = new FakeHandler();
        for (var i = 0; i < 3; i++)
        {
            fake.ThenError(status, code);
        }

        var error = await Assert.ThrowsAnyAsync<PuntjesException>(() => fake.Client().Me.GetAsync());

        Assert.Equal(family, error.GetType());
        Assert.Equal(expected, error.ErrorCode);
        Assert.Equal(code, error.RawCode);
        Assert.Equal((int)status, error.Status);
        Assert.Equal("req-1", error.RequestId);
        Assert.Equal($"Something about {code}.", error.Message);
    }

    [Fact]
    public async Task ValidationDetailsReachTheCaller()
    {
        var fake = new FakeHandler().ThenError(HttpStatusCode.UnprocessableEntity, "VALIDATION_ERROR");

        var error = await Assert.ThrowsAsync<ValidationException>(() => fake.Client().Me.GetAsync());

        Assert.Equal("bad", error.Details!.Value.GetProperty("field")[0].GetString());
        Assert.Equal(["bad"], error.ErrorsFor("field"));
        Assert.Empty(error.ErrorsFor("other"));
    }

    [Fact]
    public async Task ACodeTheEnumDoesNotKnowKeepsTheRawCode()
    {
        var fake = new FakeHandler().ThenError(HttpStatusCode.NotFound, "SOMETHING_NEW");

        var error = await Assert.ThrowsAsync<NotFoundException>(() => fake.Client().Me.GetAsync());

        Assert.Null(error.ErrorCode);
        Assert.Equal("SOMETHING_NEW", error.RawCode);
    }

    [Fact]
    public async Task AnUnmappedStatusThrowsTheBaseException()
    {
        var fake = new FakeHandler().ThenError(HttpStatusCode.Gone, "GONE_FOREVER");

        var error = await Assert.ThrowsAsync<PuntjesException>(() => fake.Client().Me.GetAsync());

        Assert.Equal(410, error.Status);
    }

    [Fact]
    public async Task ANonJsonServerErrorIsAServerException()
    {
        var fake = new FakeHandler();
        for (var i = 0; i < 3; i++)
        {
            fake.Then(() => new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent("<html>bad gateway</html>") });
        }

        var error = await Assert.ThrowsAsync<ServerException>(() => fake.Client().Me.GetAsync());

        Assert.Equal(502, error.Status);
        Assert.Null(error.ErrorCode);
        Assert.Equal("NON_JSON_RESPONSE", error.RawCode);
    }

    [Fact]
    public async Task ANonJsonClientErrorIsATransportException()
    {
        var fake = new FakeHandler().Then(() => new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("<html>login</html>") });

        await Assert.ThrowsAsync<TransportException>(() => fake.Client().Me.GetAsync());
    }

    [Fact]
    public async Task JsonWithoutAnErrorObjectFallsBackToTheStatus()
    {
        var fake = new FakeHandler().Then(HttpStatusCode.Conflict, """{"message":"no envelope"}""");

        var error = await Assert.ThrowsAsync<ConflictException>(() => fake.Client().Me.GetAsync());

        Assert.Null(error.ErrorCode);
        Assert.Equal("UNKNOWN_ERROR", error.RawCode);
        Assert.Equal("The Puntjes API returned HTTP 409.", error.Message);
    }

    [Fact]
    public async Task ANetworkFailureIsATransportException()
    {
        var fake = new FakeHandler();
        for (var i = 0; i < 3; i++)
        {
            fake.ThenThrow(new HttpRequestException("connection refused"));
        }

        var error = await Assert.ThrowsAsync<TransportException>(() => fake.Client().Me.GetAsync());

        Assert.IsType<HttpRequestException>(error.InnerException);
    }

    [Fact]
    public async Task ABaseUrlWithOrWithoutTheApiPrefixReachesTheSameUrl()
    {
        foreach (var baseUrl in new[] { "https://shop.test", "https://shop.test/", "https://shop.test/api/v1", "https://shop.test/api/v1/" })
        {
            var fake = new FakeHandler().Then(HttpStatusCode.OK, """{"data":{}}""");

            await fake.Client(new PuntjesOptions { BaseUrl = baseUrl }).Me.GetAsync();

            Assert.Equal("https://shop.test/api/v1/me", fake.ApiRequests.Single().Uri.ToString());
        }
    }

    [Fact]
    public async Task TheRequestIdFallsBackToTheResponseHeader()
    {
        var fake = new FakeHandler().Then(() =>
        {
            var response = FakeHandler.Json(HttpStatusCode.NotFound, """{"error":{"code":"CUSTOMER_NOT_FOUND","message":"No such customer.","status":404}}""");
            response.Headers.Add("X-Request-ID", "req-header");
            return response;
        });

        var error = await Assert.ThrowsAsync<NotFoundException>(() => fake.Client().Me.GetAsync());

        Assert.Equal("req-header", error.RequestId);
    }
}
