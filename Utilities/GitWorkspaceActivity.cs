namespace PiAgentGui.Utilities;

/// <summary>Distinguishes the app's bounded Git queries from commands that may mutate files or run hooks.</summary>
public static class GitWorkspaceActivity
{
    public static bool MayWrite(IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0) return true;
        return arguments[0] switch
        {
            "status" or "rev-parse" or "ls-files" or "for-each-ref" or "cat-file" or "check-ref-format" => false,
            "remote" when arguments.Count == 1 => false,
            "symbolic-ref" when arguments.SequenceEqual(new[] { "symbolic-ref", "--quiet", "HEAD" }) => false,
            "diff" when arguments.Contains("--no-ext-diff") && arguments.Contains("--no-textconv")
                && !arguments.Any(a => a == "--output" || a.StartsWith("--output=", StringComparison.Ordinal)) => false,
            _ => true
        };
    }
}
