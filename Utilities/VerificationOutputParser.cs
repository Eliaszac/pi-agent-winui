using System.Text.Json;
using System.Text.RegularExpressions;

namespace PiAgentGui.Utilities;

/// <summary>Recognizes direct verification commands and explicit runner totals; never infers counts from prose.</summary>
public static partial class VerificationOutputParser
{
    public static string Kind(string tool, string arguments)
    {
        if (tool != "bash") return "";
        string command;
        try
        {
            using var json = JsonDocument.Parse(arguments);
            command = json.RootElement.TryGetProperty("command", out var value) ? value.GetString()?.Trim() ?? "" : "";
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException) { return ""; }
        // Accept only the common test-then-build chain; arbitrary shell chains remain untrusted.
        var steps = command.Split("&&", StringSplitOptions.TrimEntries);
        if (steps.Length == 2 && steps[0].StartsWith("dotnet test ", StringComparison.OrdinalIgnoreCase)
            && steps[1].StartsWith("dotnet build ", StringComparison.OrdinalIgnoreCase))
            command = string.Join(" ", steps);
        // Pipelines, other compound commands and redirections can mask exit codes or modify files.
        if (command.IndexOfAny(['&', '|', ';', '>', '<', '\n', '\r', '`']) >= 0 || command.Contains("$(", StringComparison.Ordinal)
            || MutatingFlags().IsMatch(command)) return "";
        if (LintCommand().IsMatch(command)) return "lint";
        return TestCommand().IsMatch(command) ? "tests" : "";
    }

    public static string? PassedTests(string output)
    {
        var text = Ansi().Replace(output, "");
        var dotnet = DotNetSummary().Matches(text);
        if (dotnet.Count > 0)
        {
            long passed = 0, total = 0;
            foreach (Match match in dotnet)
            {
                if (match.Groups["failed"].Value != "0" || match.Groups["skipped"].Value != "0") return null;
                passed += long.Parse(match.Groups["passed"].Value);
                total += long.Parse(match.Groups["total"].Value);
            }
            return passed > 0 && passed == total ? $"Tests {passed:N0}/{total:N0} passed" : null;
        }
        var javascript = JavaScriptSummary().Matches(text);
        if (javascript.Count == 1 && javascript[0].Groups["passed"].Value == javascript[0].Groups["total"].Value)
        {
            var count = long.Parse(javascript[0].Groups["passed"].Value);
            return count > 0 ? $"Tests {count:N0}/{count:N0} passed" : null;
        }
        var pytest = PytestSummary().Matches(text);
        if (pytest.Count == 1)
        {
            var count = long.Parse(pytest[0].Groups["passed"].Value);
            return count > 0 ? $"Tests {count:N0}/{count:N0} passed" : null;
        }
        return null;
    }

    [GeneratedRegex(@"(?:^|\s)--(?:fix|write|update(?:Snapshot)?)(?:\s|=|$)|(?:^|\s)-u(?:\s|$)", RegexOptions.IgnoreCase)]
    private static partial Regex MutatingFlags();
    [GeneratedRegex(@"^(?:(?:npx|pnpm exec|yarn exec)\s+)?(?:eslint|biome check|ruff check)\b|^(?:npm run|pnpm(?: run)?|yarn(?: run)?)\s+lint(?:\s|$)", RegexOptions.IgnoreCase)]
    private static partial Regex LintCommand();
    [GeneratedRegex(@"^(?:dotnet test|npm (?:run )?test|pnpm(?: run)? test|yarn(?: run)? test|(?:npx |pnpm exec |yarn exec )?(?:vitest(?: run)?|jest|pytest)|python(?:3)? -m pytest)(?:\s|$)", RegexOptions.IgnoreCase)]
    private static partial Regex TestCommand();
    [GeneratedRegex(@"\x1B\[[0-?]*[ -/]*[@-~]")]
    private static partial Regex Ansi();
    [GeneratedRegex(@"^\s*Passed!\s*-\s*Failed:\s*(?<failed>\d+)\s*,\s*Passed:\s*(?<passed>\d+)\s*,\s*Skipped:\s*(?<skipped>\d+)\s*,\s*Total:\s*(?<total>\d+)\b", RegexOptions.Multiline)]
    private static partial Regex DotNetSummary();
    [GeneratedRegex(@"^\s*Tests\s*:?\s+(?<passed>\d+) passed(?:,\s*(?<total>\d+) total|\s+\((?<total>\d+)\))\s*$", RegexOptions.Multiline)]
    private static partial Regex JavaScriptSummary();
    [GeneratedRegex(@"^=+\s*(?<passed>\d+) passed in [\d.]+s(?:\s+\([\d:]+\))?\s*=+\s*$", RegexOptions.Multiline)]
    private static partial Regex PytestSummary();
}
