using PiAgentGui.Models.Home;

namespace PiAgentGui.Utilities;

public static class HomeUsageAggregator
{
    public static HomeUsageSummary Summarize(UsageInventory inventory, IReadOnlyDictionary<Guid, string> projectNames, DateOnly today, int days)
    {
        var first = today.AddDays(1 - days);
        var samples = inventory.Samples.Where(sample => DateOnly.FromDateTime(sample.At.LocalDateTime) is var date && date >= first && date <= today).ToArray();
        var total = samples.Sum(sample => (decimal)(sample.Tokens ?? 0));
        var groupedDays = samples.GroupBy(sample => DateOnly.FromDateTime(sample.At.LocalDateTime)).ToDictionary(group => group.Key, group => group.ToArray());
        var maximum = Math.Max(1, groupedDays.Values.Select(group => group.Sum(sample => (decimal)(sample.Tokens ?? 0))).DefaultIfEmpty().Max());
        var daily = Enumerable.Range(0, days).Select(index =>
        {
            var date = first.AddDays(index);
            var group = groupedDays.GetValueOrDefault(date) ?? [];
            var tokens = group.Sum(sample => (decimal)(sample.Tokens ?? 0));
            return new HomeUsageDay(date, tokens, group.Length, (double)(tokens / maximum) * 64);
        }).ToArray();
        var models = samples.GroupBy(sample => (sample.Provider, sample.Model)).Select(group =>
            Group(group.Key.Model, group.Key.Provider, group, total)).OrderByDescending(group => group.Tokens).ThenByDescending(group => group.Responses).Take(5).ToArray();
        var projects = samples.GroupBy(sample => sample.ProjectId).Select(group =>
            Group(projectNames.GetValueOrDefault(group.Key) ?? "Unavailable project", "Project", group, total))
            .OrderByDescending(group => group.Tokens).ThenByDescending(group => group.Responses).Take(5).ToArray();
        return new(total, samples.Length, samples.Select(sample => sample.ProjectId).Distinct().Count(), samples.Count(sample => sample.Tokens is null), daily, models, projects);
    }

    private static HomeUsageGroup Group(string name, string detail, IEnumerable<UsageSample> samples, decimal total)
    {
        var rows = samples.ToArray();
        var tokens = rows.Sum(sample => (decimal)(sample.Tokens ?? 0));
        return new(name, detail, tokens, rows.Length, total > 0 ? (double)(tokens / total) * 100 : 0);
    }
}
