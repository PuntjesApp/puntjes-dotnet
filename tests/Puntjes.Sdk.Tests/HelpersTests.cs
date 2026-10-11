using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Puntjes.Sdk.Exceptions;
using Puntjes.Sdk.Generated.Models;
using Puntjes.Sdk.Tests.Support;

namespace Puntjes.Sdk.Tests;

public partial class HelpersTests
{
    [Fact]
    public void AnIdempotencyKeyIsANewVersion4Uuid()
    {
        var first = IdempotencyKey.New();

        Assert.Matches(V4(), first);
        Assert.NotEqual(first, IdempotencyKey.New());
    }

    [Fact]
    public async Task ThePaginatorYieldsEveryItemOfEveryPageInOrder()
    {
        var fake = new FakeHandler().Then(HttpStatusCode.OK, Page(1, 3, "a", "b")).Then(HttpStatusCode.OK, Page(2, 3, "c")).Then(HttpStatusCode.OK, Page(3, 3, "d"));
        var client = fake.Client();

        var names = new List<string?>();
        await foreach (var product in Paginator.AllAsync<ProductData>(async (page, ct) =>
        {
            var response = await client.Products.GetAsync(c => c.QueryParameters.Page = page, ct);
            return (response?.Data?.Data, response?.Data?.Meta);
        }))
        {
            names.Add(product.Name);
        }

        Assert.Equal(["a", "b", "c", "d"], names);
        Assert.Equal(["page=1", "page=2", "page=3"], fake.ApiRequests.Select(r => r.Uri.Query.TrimStart('?')));
    }

    [Fact]
    public async Task ThePaginatorStopsWhenTheServerIgnoresThePage()
    {
        var fake = new FakeHandler().Then(HttpStatusCode.OK, Page(1, 5, "a")).Then(HttpStatusCode.OK, Page(1, 5, "a"));
        var client = fake.Client();

        var items = await Collect(client, fake);

        Assert.Equal(["a"], items);
        Assert.Equal(2, fake.ApiRequests.Count);
    }

    [Fact]
    public async Task AnEmptyFirstPageYieldsNothing()
    {
        var fake = new FakeHandler().Then(HttpStatusCode.OK, Page(1, 1));

        Assert.Empty(await Collect(fake.Client(), fake));
    }

    [Fact]
    public async Task TheApplePassIsFetchedAsBytes()
    {
        var bytes = new byte[] { 0x50, 0x4B, 0x03, 0x04 };
        var fake = new FakeHandler().Then(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) { Headers = { ContentType = new("application/vnd.apple.pkpass") } } });

        var pass = await WalletPass.AppleAsync(fake.Client(), "cus_1");

        Assert.Equal(bytes, pass);
        var request = fake.ApiRequests.Single();
        Assert.Equal("https://shop.test/api/v1/customers/cus_1/wallet-pass?platform=apple", request.Uri.ToString());
        Assert.Equal("application/vnd.apple.pkpass", request.Accept);
    }

    [Fact]
    public async Task AMissingCustomerHasNoPass()
    {
        var fake = new FakeHandler().ThenError(HttpStatusCode.NotFound, "CUSTOMER_NOT_FOUND");

        await Assert.ThrowsAsync<NotFoundException>(() => WalletPass.AppleAsync(fake.Client(), "cus_404"));
    }

    private static async Task<List<string?>> Collect(Generated.PuntjesClient client, FakeHandler fake)
    {
        var names = new List<string?>();
        await foreach (var product in Paginator.AllAsync<ProductData>(async (page, ct) =>
        {
            var response = await client.Products.GetAsync(c => c.QueryParameters.Page = page, ct);
            return (response?.Data?.Data, response?.Data?.Meta);
        }))
        {
            names.Add(product.Name);
        }

        return names;
    }

    private static string Page(int current, int last, params string[] names) => JsonSerializer.Serialize(new
    {
        data = new
        {
            data = names.Select(name => new { name }),
            meta = new { current_page = current, last_page = last, per_page = 2, total = 4 },
            links = new { first = (string?)null, last = (string?)null, prev = (string?)null, next = (string?)null },
        },
    });

    [GeneratedRegex("^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$")]
    private static partial Regex V4();
}
