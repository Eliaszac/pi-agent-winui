namespace PiAgentGui.Models.SourceControl;

public sealed record GitBranch(string Ref, string Name, bool Remote, string? RemoteName, string? RemoteRef, bool Current)
{
    public string Detail => Current ? "Current branch" : Remote ? "Remote branch" : "Local branch";
    public bool CanCheckout => !Current;
    public bool CanFetch => !string.IsNullOrEmpty(RemoteName) && RemoteName != "." && RemoteRef?.StartsWith("refs/heads/", StringComparison.Ordinal) == true;
}
