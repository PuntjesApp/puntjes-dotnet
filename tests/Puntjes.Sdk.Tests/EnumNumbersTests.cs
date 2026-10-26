using Puntjes.Sdk.Generated.Errors;
using Puntjes.Tools;

namespace Puntjes.Sdk.Tests;

public class EnumNumbersTests
{
    private const string Locale = """
        namespace Puntjes.Sdk.Generated.Models
        {
            public enum CreateCustomerData_locale
            {
                [EnumMember(Value = "nl")]
                #pragma warning disable CS1591
                Nl,
                #pragma warning restore CS1591
                [EnumMember(Value = "en")]
                #pragma warning disable CS1591
                En,
                #pragma warning restore CS1591
            }
        }
        """;

    [Fact]
    public void ANewEnumNumbersItsMembersInOrder()
    {
        var numbers = new Dictionary<string, Dictionary<string, int>>();

        var source = EnumNumbers.Apply(Locale, "Models/CreateCustomerData_locale", numbers);

        Assert.Contains("        Nl = 0,\n", source);
        Assert.Contains("        En = 1,\n", source);
        Assert.Equal(new Dictionary<string, int> { ["Nl"] = 0, ["En"] = 1 }, numbers["Models/CreateCustomerData_locale"]);
    }

    [Fact]
    public void AMemberKeepsItsNumberWhenANewOneComesBeforeIt()
    {
        var numbers = new Dictionary<string, Dictionary<string, int>>
        {
            ["Models/CreateCustomerData_locale"] = new() { ["Nl"] = 0, ["En"] = 1, ["Gone"] = 4 },
        };
        var withDe = Locale.Replace("        Nl,\n", "        Nl,\n        De,\n");

        var source = EnumNumbers.Apply(withDe, "Models/CreateCustomerData_locale", numbers);

        Assert.Contains("        De = 5,\n", source);
        Assert.Contains("        En = 1,\n", source);
        Assert.Equal(4, numbers["Models/CreateCustomerData_locale"]["Gone"]);
    }

    [Fact]
    public void AFileWithoutAnEnumStaysAsItIs()
    {
        const string model = "namespace X\n{\n    public partial class Customer\n    {\n        Nl,\n    }\n}\n";

        Assert.Equal(model, EnumNumbers.Apply(model, "Models/Customer", new Dictionary<string, Dictionary<string, int>>()));
    }

    [Fact]
    public void TheNumbersFileRoundTripsInNameAndNumberOrder()
    {
        var numbers = new Dictionary<string, Dictionary<string, int>>
        {
            ["Statistics/GetPeriodQueryParameterType"] = new() { ["Today"] = 1, ["Week"] = 0 },
            ["Models/A"] = new() { ["X"] = 0 },
        };

        var json = EnumNumbers.Render(numbers);

        Assert.Equal(
            "{\n  \"Models/A\": {\n    \"X\": 0\n  },\n  \"Statistics/GetPeriodQueryParameterType\": {\n    \"Week\": 0,\n    \"Today\": 1\n  }\n}\n",
            json);
        Assert.Equal(numbers, EnumNumbers.Parse(json));
    }

    [Fact]
    public void ANumbersFileThatGivesOneNumberTwiceInAnEnumIsRefused()
    {
        var error = Assert.Throws<InvalidOperationException>(() => EnumNumbers.Parse("""{"Models/A": {"X": 0, "Y": 0}}"""));

        Assert.Contains("Models/A", error.Message);
    }

    [Fact]
    public void EachMemberOfTheReleasedSdkKeepsItsNumber()
    {
        var released = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Fixtures", "model-enums-2.2.txt"));

        foreach (var members in released.GroupBy(line => line[..line.LastIndexOf('.')]))
        {
            var type = typeof(ErrorCode).Assembly.GetType(members.Key, throwOnError: true)!;
            var number = 0;
            foreach (var member in members)
            {
                Assert.Equal(number++, (int)Enum.Parse(type, member[(member.LastIndexOf('.') + 1)..]));
            }
        }
    }
}
