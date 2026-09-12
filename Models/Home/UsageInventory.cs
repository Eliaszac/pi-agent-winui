namespace PiAgentGui.Models.Home;

public sealed record UsageInventory(IReadOnlyList<UsageSample> Samples, int UnavailableSessions, int SkippedRecords);
