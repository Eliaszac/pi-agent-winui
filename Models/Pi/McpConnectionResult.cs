namespace PiAgentGui.Models.Pi;

public sealed record McpConnectionResult(bool Connected, string State, int ToolCount, string Message);
