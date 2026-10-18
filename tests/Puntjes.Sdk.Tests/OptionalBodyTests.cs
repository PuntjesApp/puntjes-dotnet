using System.Text.Json;
using Puntjes.Sdk.Generated.Models;
using Puntjes.Sdk.Tests.Support;

namespace Puntjes.Sdk.Tests;

public class OptionalBodyTests
{
    private const string Ok = """{"data":{}}""";

    [Fact]
    public async Task VerifyWithNoArgumentSendsNoBody()
    {
        var fake = new FakeHandler().Then(System.Net.HttpStatusCode.OK, Ok);

        await fake.Client().Redemptions["ABC"].Verify.PostAsync();

        var sent = Assert.Single(fake.ApiRequests);
        Assert.Equal(HttpMethod.Post, sent.Method);
        Assert.Equal("/api/v1/redemptions/ABC/verify", sent.Uri.AbsolutePath);
        Assert.Null(sent.Body);
        Assert.Null(sent.ContentType);
    }

    [Fact]
    public async Task VerifyWithABodySendsItAsJson()
    {
        var fake = new FakeHandler().Then(System.Net.HttpStatusCode.OK, Ok);

        await fake.Client().Redemptions["ABC"].Verify.PostAsync(new VerifyRedemptionData { Branch = "north" });

        var sent = Assert.Single(fake.ApiRequests);
        Assert.Equal("application/json", sent.ContentType);
        Assert.Equal("north", JsonDocument.Parse(sent.Body!).RootElement.GetProperty("branch").GetString());
    }

    [Fact]
    public async Task AVoucherVerifyAcceptsNoBodyToo()
    {
        var fake = new FakeHandler().Then(System.Net.HttpStatusCode.OK, Ok);

        await fake.Client().Vouchers["ABC"].Verify.PostAsync();

        Assert.Null(Assert.Single(fake.ApiRequests).Body);
    }

    [Fact]
    public async Task ARequiredBodyStillRefusesNull()
    {
        var fake = new FakeHandler();

        await Assert.ThrowsAsync<ArgumentNullException>(() => fake.Client().Transactions.PostAsync(null!));
    }
}
