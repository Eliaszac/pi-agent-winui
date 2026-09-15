namespace PiAgentGui.Models.GitHub;

public sealed record GitHubWriteRequest(string Action, int Number, string? Title = null, string? Body = null, string? State = null);
