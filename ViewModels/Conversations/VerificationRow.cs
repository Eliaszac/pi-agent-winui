namespace PiAgentGui.ViewModels.Conversations;

public sealed record VerificationRow(string Label, bool Failed)
{
    public bool Passed => !Failed;
}
