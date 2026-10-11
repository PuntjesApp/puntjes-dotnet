using System.Text.RegularExpressions;

namespace Puntjes.Tools;

/// <summary>
/// Decides what the regenerate workflow does with one spec-published dispatch: refuse it, skip it, or regenerate,
/// with the commit type, the auto-merge choice and the older regenerate PRs to close.
/// </summary>
public static partial class DispatchPlan
{
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        var (tag, level, url, source, open) = (Arg(args, 0), Arg(args, 1), Arg(args, 2), Arg(args, 3), Arg(args, 4));

        if (Version(tag) is not { } current)
        {
            error.WriteLine($"The tag {tag} is not vX.Y.Z.");
            return 1;
        }

        if (url != $"https://raw.githubusercontent.com/PuntjesApp/puntjes-openapi/{tag}/api.json")
        {
            error.WriteLine($"The url is not api.json of puntjes-openapi at {tag}.");
            return 1;
        }

        if (level is not ("major" or "minor" or "patch"))
        {
            error.WriteLine($"The level {level} is not major, minor or patch.");
            return 1;
        }

        var sourced = Version(source.Trim());
        var openTags = new List<(string Branch, Version Version, bool Major)>();
        foreach (var line in open.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
            var branch = parts[0];
            if (branch.StartsWith("regenerate/", StringComparison.Ordinal) && Version(branch["regenerate/".Length..]) is { } version)
            {
                openTags.Add((branch, version, parts.Length > 1 && parts[1].StartsWith("feat!:", StringComparison.Ordinal)));
            }
        }

        var skip = (sourced is not null && sourced >= current) || openTags.Any(o => o.Version > current);
        var close = skip ? [] : openTags.Where(o => o.Version < current).ToList();
        var major = level == "major" || close.Any(o => o.Major) || (sourced is not null && sourced.Major < current.Major);

        output.WriteLine($"skip={(skip ? "true" : "false")}");
        output.WriteLine($"commit_type={(major ? "feat!" : level == "minor" ? "feat" : "fix")}");
        output.WriteLine($"auto_merge={(major ? "false" : "true")}");
        output.WriteLine($"branch=regenerate/{tag}");
        output.WriteLine($"close={string.Join(' ', close.Select(o => o.Branch))}");

        return 0;
    }

    private static string Arg(string[] args, int index) => index < args.Length ? args[index] : "";

    private static Version? Version(string reference)
    {
        var match = TagPattern().Match(reference);

        return match.Success ? new Version(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value), int.Parse(match.Groups[3].Value)) : null;
    }

    [GeneratedRegex(@"^v(\d+)\.(\d+)\.(\d+)$")]
    private static partial Regex TagPattern();
}
