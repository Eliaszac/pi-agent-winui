namespace PiAgentGui.Services.Pi;

public sealed class PiProcessExitException(int? exitCode, string hint)
    : IOException($"Pi exited{(exitCode is { } code ? $" (exit code {code})" : "")}. {hint}")
{
    public int? ExitCode { get; } = exitCode;
}
