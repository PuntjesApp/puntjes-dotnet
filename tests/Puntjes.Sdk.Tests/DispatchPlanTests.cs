using Puntjes.Tools;

namespace Puntjes.Sdk.Tests;

public class DispatchPlanTests
{
    private const string Url = "https://raw.githubusercontent.com/PuntjesApp/puntjes-openapi/v1.1.0/api.json";

    private static (int Exit, Dictionary<string, string> Plan) Plan(params string[] args)
    {
        var output = new StringWriter();
        var exit = DispatchPlan.Run(args, output, TextWriter.Null);
        var plan = output.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('=', 2))
            .ToDictionary(pair => pair[0], pair => pair.Length > 1 ? pair[1] : "");

        return (exit, plan);
    }

    [Fact]
    public void AMinorIsAFeatThatMergesItself()
    {
        var (exit, plan) = Plan("v1.1.0", "minor", Url, "main@abc1234");

        Assert.Equal(0, exit);
        Assert.Equal(
            new Dictionary<string, string> { ["skip"] = "false", ["commit_type"] = "feat", ["auto_merge"] = "true", ["branch"] = "regenerate/v1.1.0", ["close"] = "" },
            plan);
    }

    [Fact]
    public void AMajorIsBreakingAndWaitsForAPerson()
    {
        var plan = Plan("v1.1.0", "major", Url, "main@abc1234").Plan;

        Assert.Equal("feat!", plan["commit_type"]);
        Assert.Equal("false", plan["auto_merge"]);
    }

    [Fact]
    public void APatchIsAFix() => Assert.Equal("fix", Plan("v1.1.0", "patch", Url, "main@abc1234").Plan["commit_type"]);

    [Fact]
    public void TheTagAlreadyInSourceIsSkipped() => Assert.Equal("true", Plan("v1.1.0", "minor", Url, "v1.1.0").Plan["skip"]);

    [Fact]
    public void AUrlOutsidePuntjesOpenapiIsRefused()
    {
        Assert.Equal(1, Plan("v1.1.0", "minor", "https://example.com/api.json", "x").Exit);
        Assert.Equal(1, Plan("v1.1.0", "minor", "https://raw.githubusercontent.com/PuntjesApp/puntjes-openapi/v9.9.9/api.json", "x").Exit);
    }

    [Fact]
    public void AnUnknownLevelOrTagIsRefused()
    {
        Assert.Equal(1, Plan("v1.1.0", "huge", Url, "x").Exit);
        Assert.Equal(1, Plan("v1.1.0;rm", "minor", Url, "x").Exit);
    }

    [Fact]
    public void ASourceTagNewerThanTheTagIsSkipped() => Assert.Equal("true", Plan("v1.1.0", "minor", Url, "v1.2.0").Plan["skip"]);

    [Fact]
    public void VersionsCompareByNumberNotByText()
    {
        const string url = "https://raw.githubusercontent.com/PuntjesApp/puntjes-openapi/v1.10.0/api.json";

        Assert.Equal("false", Plan("v1.10.0", "minor", url, "v1.9.0").Plan["skip"]);
        Assert.Equal("true", Plan("v1.9.0", "minor", url.Replace("v1.10.0", "v1.9.0"), "v1.10.0").Plan["skip"]);
    }

    [Fact]
    public void AnOpenRegeneratePrForANewerTagSkipsTheOlderTag()
    {
        var plan = Plan("v1.1.0", "minor", Url, "main@abc1234", "regenerate/v1.0.0\nregenerate/v1.2.0").Plan;

        Assert.Equal("true", plan["skip"]);
        Assert.Equal("", plan["close"]);
    }

    [Fact]
    public void TheOpenRegeneratePrsForOlderTagsAreClosed()
    {
        var plan = Plan("v1.1.0", "minor", Url, "v1.0.0", "regenerate/v1.0.1\nregenerate/v1.1.0\nfeature/x\nregenerate/v0.9.0\nregenerate/v1.0.2;rm").Plan;

        Assert.Equal("false", plan["skip"]);
        Assert.Equal("regenerate/v1.0.1 regenerate/v0.9.0", plan["close"]);
    }

    [Fact]
    public void ClosingAnOpenMajorForAnOlderTagMakesTheNewPrAMajor()
    {
        var plan = Plan("v1.1.0", "patch", Url, "v1.0.0", "regenerate/v1.0.5 feat!: regenerate from puntjes-openapi v1.0.5").Plan;

        Assert.Equal("regenerate/v1.0.5", plan["close"]);
        Assert.Equal("feat!", plan["commit_type"]);
        Assert.Equal("false", plan["auto_merge"]);
    }

    [Fact]
    public void ClosingAnOpenMinorForAnOlderTagKeepsTheNewLevel()
    {
        var plan = Plan("v1.1.0", "patch", Url, "v1.0.0", "regenerate/v1.0.5 feat: regenerate from puntjes-openapi v1.0.5").Plan;

        Assert.Equal("regenerate/v1.0.5", plan["close"]);
        Assert.Equal("fix", plan["commit_type"]);
        Assert.Equal("true", plan["auto_merge"]);
    }

    [Fact]
    public void ASourceTagWithALowerMajorMakesTheNewPrAMajor()
    {
        var plan = Plan("v2.0.1", "patch", "https://raw.githubusercontent.com/PuntjesApp/puntjes-openapi/v2.0.1/api.json", "v1.9.0").Plan;

        Assert.Equal("feat!", plan["commit_type"]);
        Assert.Equal("false", plan["auto_merge"]);
    }

    [Fact]
    public void ASourceThatIsNotATagKeepsTheNewLevel()
    {
        var plan = Plan("v1.1.0", "patch", Url, "main@abc1234").Plan;

        Assert.Equal("fix", plan["commit_type"]);
        Assert.Equal("true", plan["auto_merge"]);
    }
}
