namespace PiAgentGui.Models.Pi;

/// <summary>A command explicitly rejected by Pi, before acceptance.</summary>
public sealed class PiCommandException(string command, string message) : Exception(message)
{
    public string Command { get; } = command;
}
