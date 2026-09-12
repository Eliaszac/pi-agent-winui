namespace PiAgentGui.Models.Home;

public sealed record HomeUsageSummary(decimal Tokens, int Responses, int ActiveProjects, int MissingUsage,
    IReadOnlyList<HomeUsageDay> Days, IReadOnlyList<HomeUsageGroup> Models, IReadOnlyList<HomeUsageGroup> Projects);
