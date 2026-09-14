namespace PiAgentGui.Models.Home;

public sealed record UsageArchive(IReadOnlyList<UsageSample> Samples, DateTimeOffset? ResetAt = null);
