using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Puntjes.Tools;

/// <summary>Kiota ignores requestBody.required: false. This makes the body optional in the builders of those operations.</summary>
public static partial class OptionalBodies
{
    private const string NullCheck = "if(ReferenceEquals(body, null)) throw new ArgumentNullException(nameof(body));";

    public static IReadOnlyList<(string Path, string Method)> Operations(JsonDocument spec)
    {
        var operations = new List<(string Path, string Method)>();
        if (!spec.RootElement.TryGetProperty("paths", out var paths))
        {
            return operations;
        }

        foreach (var path in paths.EnumerateObject())
        {
            foreach (var operation in path.Value.EnumerateObject())
            {
                if (operation.Value.ValueKind == JsonValueKind.Object
                    && operation.Value.TryGetProperty("requestBody", out var body)
                    && !(body.TryGetProperty("required", out var required) && required.ValueKind == JsonValueKind.True))
                {
                    operations.Add((path.Name, CultureInfo.InvariantCulture.TextInfo.ToTitleCase(operation.Name)));
                }
            }
        }

        return operations;
    }

    public static int Apply(JsonDocument spec, string generatedDirectory)
    {
        var builders = Directory.EnumerateFiles(generatedDirectory, "*RequestBuilder.cs", SearchOption.AllDirectories).ToList();
        var operations = Operations(spec);
        foreach (var (path, method) in operations)
        {
            var template = new Regex("\"\\{\\+baseurl\\}" + Regex.Escape(path) + "(\"|\\{\\?)");
            var files = builders.Where(file => template.IsMatch(File.ReadAllText(file))).ToList();
            if (files.Count != 1)
            {
                throw new InvalidOperationException($"{method.ToUpperInvariant()} {path} has {files.Count} request builders, not 1.");
            }

            File.WriteAllText(files[0], Patch(File.ReadAllText(files[0]), method));
        }

        return operations.Count;
    }

    public static string Patch(string source, string method)
    {
        var signature = new Regex($@"^(\s*public .*\b(?:{method}Async|To{method}RequestInformation)\()(global::[\w.]+) body, ");
        var counts = new Dictionary<string, int> { [$"{method}Async"] = 0, [$"To{method}RequestInformation"] = 0 };
        var checks = new Dictionary<string, int> { [$"{method}Async"] = 0, [$"To{method}RequestInformation"] = 0 };
        var contents = 0;
        string? current = null;
        var lines = source.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (signature.Match(line) is { Success: true } match)
            {
                current = line.Contains($"To{method}RequestInformation(", StringComparison.Ordinal) ? $"To{method}RequestInformation" : $"{method}Async";
                counts[current]++;
                var nullable = line.Contains(">? requestConfiguration", StringComparison.Ordinal) ? "?" : "";
                lines[i] = signature.Replace(line, $"$1$2{nullable} body = default, ");
            }
            else if (current is not null && line.Trim() == NullCheck)
            {
                checks[current]++;
                lines[i] = null!;
            }
            else if (current is not null && line.Contains("SetContentFromParsable(", StringComparison.Ordinal))
            {
                contents++;
                lines[i] = Regex.Replace(line, @"^(\s*)", "$1if(body != null) ");
            }
            else if (line == "        }")
            {
                current = null;
            }
        }

        foreach (var name in counts.Keys)
        {
            if (counts[name] != 2 || checks[name] != 1)
            {
                throw new InvalidOperationException($"{name} is not the shape the patch expects: {counts[name]} signatures and {checks[name]} null checks, not 2 and 1.");
            }
        }

        if (contents != 1)
        {
            throw new InvalidOperationException($"To{method}RequestInformation sets the content {contents} times, not 1.");
        }

        return string.Join('\n', lines.Where(line => line is not null));
    }
}
