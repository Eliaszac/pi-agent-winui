namespace PiAgentGui.Models.GitHub;

public sealed record GitHubDeviceCode(string DeviceCode, string UserCode, int Interval, DateTimeOffset ExpiresAt);
