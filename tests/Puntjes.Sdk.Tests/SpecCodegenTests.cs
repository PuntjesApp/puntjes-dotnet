using System.Text.Json;
using Puntjes.Sdk.Generated.Errors;
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
    public void ACodeKeepsItsNumberWhenANewCodeSortsBeforeIt()
    {
        var numbers = new Dictionary<string, int> { ["PLAN_LIMIT_EXCEEDED"] = 0, ["RATE_LIMITED"] = 1 };

        var numbered = SpecCodegen.Numbered(SpecCodegen.ErrorCodes(Fixture()), numbers);

        Assert.Equal(
            [("CUSTOMER_NOT_FOUND", 404, 2), ("PLAN_LIMIT_EXCEEDED", 429, 0), ("RATE_LIMITED", 429, 1)],
            numbered);
        Assert.Equal(2, numbers["CUSTOMER_NOT_FOUND"]);
    }

    [Fact]
    public void ARemovedCodeKeepsItsNumberSoNoOtherCodeTakesIt()
    {
        var numbers = new Dictionary<string, int> { ["CUSTOMER_NOT_FOUND"] = 0, ["GONE"] = 5, ["RATE_LIMITED"] = 2 };

        var numbered = SpecCodegen.Numbered(SpecCodegen.ErrorCodes(Fixture()), numbers);

        Assert.Equal(6, numbered.Single(c => c.Code == "PLAN_LIMIT_EXCEEDED").Number);
        Assert.Equal(5, numbers["GONE"]);
    }

    [Fact]
    public void TheNumbersFileRoundTripsInNumberOrder()
    {
        var numbers = new Dictionary<string, int> { ["RATE_LIMITED"] = 1, ["CUSTOMER_NOT_FOUND"] = 0 };

        var json = SpecCodegen.RenderNumbers(numbers);

        Assert.Equal("{\n  \"CUSTOMER_NOT_FOUND\": 0,\n  \"RATE_LIMITED\": 1\n}\n", json);
        Assert.Equal(numbers, SpecCodegen.ParseNumbers(json));
    }

    [Fact]
    public void ANumbersFileThatGivesOneNumberTwiceIsRefused()
    {
        var error = Assert.Throws<InvalidOperationException>(() => SpecCodegen.ParseNumbers("""{"A": 0, "B": 0}"""));

        Assert.Contains("A and B", error.Message);
    }

    [Fact]
    public void EachCodeOfTheReleasedSdkKeepsItsNumber()
    {
        var released = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Fixtures", "error-codes-2.2.txt"));

        for (var number = 0; number < released.Length; number++)
        {
            Assert.Equal(number, (int)Enum.Parse<ErrorCode>(released[number]));
        }
    }

    [Fact]
    public void TheEnumMapsEachMemberToItsWireCodeAndStatus()
    {
        var numbers = new Dictionary<string, int> { ["PLAN_LIMIT_EXCEEDED"] = 7 };
        var source = SpecCodegen.RenderErrorCode(SpecCodegen.Numbered(SpecCodegen.ErrorCodes(Fixture()), numbers));

        Assert.Contains("    PlanLimitExceeded = 7,", source);
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
