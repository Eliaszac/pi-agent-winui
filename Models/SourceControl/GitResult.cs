namespace PiAgentGui.Models.SourceControl;

public sealed record GitResult(int ExitCode, string Output, string Error);
