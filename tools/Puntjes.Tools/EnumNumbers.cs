using System.Text.Json;
using System.Text.RegularExpressions;

namespace Puntjes.Tools;

/// <summary>Kiota numbers enum members by their place in the spec, so a new value shifts the ones after it.
/// This writes each number from spec/enum-numbers.json, and gives a new member the next free number of its enum.</summary>
public static partial class EnumNumbers
{
    public static int Apply(string generatedDirectory, string numbersPath)
    {
        var numbers = Parse(File.ReadAllText(numbersPath));
        var files = Directory.EnumerateFiles(generatedDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(file => !Path.GetRelativePath(generatedDirectory, file).StartsWith("Errors", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();
        foreach (var file in files)
        {
            var key = Path.ChangeExtension(Path.GetRelativePath(generatedDirectory, file), null).Replace('\\', '/');
            File.WriteAllText(file, Apply(File.ReadAllText(file), key, numbers));
        }

        File.WriteAllText(numbersPath, Render(numbers));

        return numbers.Count;
    }

    public static string Apply(string source, string key, IDictionary<string, Dictionary<string, int>> numbers)
    {
        if (!EnumDeclaration().IsMatch(source))
        {
            return source;
        }

        if (!numbers.TryGetValue(key, out var known))
        {
            numbers[key] = known = new Dictionary<string, int>(StringComparer.Ordinal);
        }

        var next = known.Count == 0 ? 0 : known.Values.Max() + 1;

        return Member().Replace(source, match =>
        {
            var name = match.Groups["name"].Value;
            if (!known.TryGetValue(name, out var number))
            {
                known[name] = number = next++;
            }

            return $"{match.Groups["indent"].Value}{name} = {number},";
        });
    }

    public static Dictionary<string, Dictionary<string, int>> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var numbers = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        foreach (var type in document.RootElement.EnumerateObject())
        {
            var members = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var member in type.Value.EnumerateObject())
            {
                var number = member.Value.GetInt32();
                if (members.ContainsValue(number))
                {
                    throw new InvalidOperationException($"{type.Name} gives the number {number} twice.");
                }

                members[member.Name] = number;
            }

            numbers[type.Name] = members;
        }

        return numbers;
    }

    public static string Render(IDictionary<string, Dictionary<string, int>> numbers) =>
        "{\n" + string.Join(",\n", numbers.OrderBy(t => t.Key, StringComparer.Ordinal).Select(t =>
            $"  \"{t.Key}\": {{\n" + string.Join(",\n", t.Value.OrderBy(m => m.Value).Select(m => $"    \"{m.Key}\": {m.Value}")) + "\n  }"))
        + "\n}\n";

    [GeneratedRegex(@"^\s*public enum \w+", RegexOptions.Multiline)]
    private static partial Regex EnumDeclaration();

    [GeneratedRegex(@"^(?<indent>[ ]{8})(?<name>[A-Za-z_]\w*),[ ]*$", RegexOptions.Multiline)]
    private static partial Regex Member();
}
