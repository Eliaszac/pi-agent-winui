namespace PiAgentGui.Models.Projects;

public sealed record WslDistributionSnapshot(IReadOnlyList<string> Names, string? DefaultName, string? Error = null)
{
    public string? PreferredName => Names.Count == 1 ? Names[0] : Names.Contains(DefaultName) ? DefaultName : Names.FirstOrDefault();
}
