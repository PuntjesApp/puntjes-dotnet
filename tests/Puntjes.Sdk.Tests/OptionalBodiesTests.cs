using System.Text.Json;
using Puntjes.Tools;

namespace Puntjes.Sdk.Tests;

public class OptionalBodiesTests
{
    private const string Builder = """
        public class ThingRequestBuilder
        {
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
        #nullable enable
                public async Task<Reply?> PostAsync(global::Models.ThingData body, Action<RequestConfiguration<DefaultQueryParameters>>? requestConfiguration = default, CancellationToken cancellationToken = default)
                {
        #nullable restore
        #else
                public async Task<Reply> PostAsync(global::Models.ThingData body, Action<RequestConfiguration<DefaultQueryParameters>> requestConfiguration = default, CancellationToken cancellationToken = default)
                {
        #endif
                    if(ReferenceEquals(body, null)) throw new ArgumentNullException(nameof(body));
                    var requestInfo = ToPostRequestInformation(body, requestConfiguration);
                    return await RequestAdapter.SendAsync<Reply>(requestInfo);
                }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
        #nullable enable
                public RequestInformation ToPostRequestInformation(global::Models.ThingData body, Action<RequestConfiguration<DefaultQueryParameters>>? requestConfiguration = default)
                {
        #nullable restore
        #else
                public RequestInformation ToPostRequestInformation(global::Models.ThingData body, Action<RequestConfiguration<DefaultQueryParameters>> requestConfiguration = default)
                {
        #endif
                    if(ReferenceEquals(body, null)) throw new ArgumentNullException(nameof(body));
                    var requestInfo = new RequestInformation(Method.POST, UrlTemplate, PathParameters);
                    requestInfo.SetContentFromParsable(RequestAdapter, "application/json", body);
                    return requestInfo;
                }
        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
        #nullable enable
                public async Task<Reply?> PutAsync(global::Models.ThingData body, Action<RequestConfiguration<DefaultQueryParameters>>? requestConfiguration = default, CancellationToken cancellationToken = default)
                {
        #nullable restore
        #else
                public async Task<Reply> PutAsync(global::Models.ThingData body, Action<RequestConfiguration<DefaultQueryParameters>> requestConfiguration = default, CancellationToken cancellationToken = default)
                {
        #endif
                    if(ReferenceEquals(body, null)) throw new ArgumentNullException(nameof(body));
                    return null;
                }
        }
        """;

    private static JsonDocument Spec(string body) =>
        JsonDocument.Parse("""{"paths":{"/things":{"post":{"requestBody":""" + body + """},"get":{}}}}""");

    [Fact]
    public void ABodyThatIsNotRequiredIsOptional()
    {
        using var spec = Spec("""{"content":{}}""");

        Assert.Equal([("/things", "Post")], OptionalBodies.Operations(spec));
    }

    [Fact]
    public void ABodyWithRequiredFalseIsOptionalAndRequiredTrueIsNot()
    {
        using var optional = Spec("""{"required":false}""");
        using var required = Spec("""{"required":true}""");

        Assert.Single(OptionalBodies.Operations(optional));
        Assert.Empty(OptionalBodies.Operations(required));
    }

    [Fact]
    public void PatchMakesTheBodyOptionalInBothVariantsAndSendsNoContentWithoutOne()
    {
        var source = OptionalBodies.Patch(Builder, "Post");

        Assert.Contains("PostAsync(global::Models.ThingData? body = default, Action<RequestConfiguration<DefaultQueryParameters>>? requestConfiguration", source);
        Assert.Contains("PostAsync(global::Models.ThingData body = default, Action<RequestConfiguration<DefaultQueryParameters>> requestConfiguration", source);
        Assert.Contains("ToPostRequestInformation(global::Models.ThingData? body = default, ", source);
        Assert.Contains("ToPostRequestInformation(global::Models.ThingData body = default, ", source);
        Assert.Contains("if(body != null) requestInfo.SetContentFromParsable(RequestAdapter, \"application/json\", body);", source);
        Assert.Equal(1, Count(source, "ArgumentNullException"));
    }

    [Fact]
    public void PatchLeavesAnotherMethodAlone()
    {
        var source = OptionalBodies.Patch(Builder, "Post");

        Assert.Contains("PutAsync(global::Models.ThingData body, ", source);
        Assert.Contains("nameof(body)", source);
    }

    [Fact]
    public void PatchRefusesASourceThatIsNotTheShapeItExpects()
    {
        var error = Assert.Throws<InvalidOperationException>(() => OptionalBodies.Patch(Builder, "Patch"));

        Assert.Contains("PatchAsync", error.Message);
    }

    private static int Count(string text, string part) => text.Split(part).Length - 1;
}
