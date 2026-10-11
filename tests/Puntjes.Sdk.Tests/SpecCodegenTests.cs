using System.Text.Json;
using Puntjes.Tools;

namespace Puntjes.Sdk.Tests;

public class SpecCodegenTests
{
    private static JsonDocument Fixture() => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "codegen-spec.json")));

    [Fact]
    public void ErrorCodesAreDistinctSortedAndCarryTheirStatus()
    {
        var codes = SpecCodegen.ErrorCodes(Fixture());

        Assert.Equal(
            [("CUSTOMER_NOT_FOUND", 404), ("PLAN_LIMIT_EXCEEDED", 429), ("RATE_LIMITED", 429)],
            codes);
    }

    [Fact]
    public void ACodeWithTwoStatusesIsRefused()
    {
        using var spec = JsonDocument.Parse("""{"a":{"x-error-codes":[{"code":"X","status":404}]},"b":{"x-error-codes":[{"code":"X","status":409}]}}""");

        var error = Assert.Throws<InvalidOperationException>(() => SpecCodegen.ErrorCodes(spec));
        Assert.Contains("X has two statuses", error.Message);
    }

    [Fact]
    public void PublicOperationsAreThoseWithAnEmptySecurity()
    {
        Assert.Equal(["POST /b/{id}"], SpecCodegen.PublicOperations(Fixture()));
    }

    [Fact]
    public void TheEnumMapsEachMemberToItsWireCodeAndStatus()
    {
        var source = SpecCodegen.RenderErrorCode(SpecCodegen.ErrorCodes(Fixture()));

        Assert.Contains("PlanLimitExceeded,", source);
        Assert.Contains("ErrorCode.PlanLimitExceeded => \"PLAN_LIMIT_EXCEEDED\",", source);
        Assert.Contains("ErrorCode.PlanLimitExceeded => 429,", source);
        Assert.Contains("\"PLAN_LIMIT_EXCEEDED\" => ErrorCode.PlanLimitExceeded,", source);
    }

    [Fact]
    public void TheListRendersEachPublicOperation()
    {
        var source = SpecCodegen.RenderPublicOperations(SpecCodegen.PublicOperations(Fixture()));

        Assert.Contains("\"POST /b/{id}\",", source);
    }
}
