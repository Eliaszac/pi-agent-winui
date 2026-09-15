using PiAgentGui.Models.Home;
using PiAgentGui.Models.Pi;

namespace PiAgentGui.Utilities;

public static class MostUsedSelectors
{
    public static PiModel? Model(IEnumerable<UsageSample> history, IReadOnlyList<PiModel> available)
    {
        var allowed = available.DistinctBy(model => (model.Provider, model.Id)).ToDictionary(model => (model.Provider, model.Id));
        var winner = history.DistinctBy(sample => sample.Key).Where(sample => allowed.ContainsKey((sample.Provider, sample.Model)))
            .GroupBy(sample => (sample.Provider, sample.Model)).OrderByDescending(group => group.Count())
            .ThenByDescending(group => group.Max(sample => sample.At))
            .ThenBy(group => group.Key.Provider, StringComparer.Ordinal).ThenBy(group => group.Key.Model, StringComparer.Ordinal).FirstOrDefault();
        return winner is null ? null : allowed[winner.Key];
    }

    public static string? Effort(IEnumerable<UsageSample> history, string? provider, string? model, IReadOnlyList<string> supported) =>
        history.DistinctBy(sample => sample.Key)
            .Where(sample => sample.Provider == provider && sample.Model == model && sample.Effort is not null && supported.Contains(sample.Effort))
            .GroupBy(sample => sample.Effort!).OrderByDescending(group => group.Count())
            .ThenByDescending(group => group.Max(sample => sample.At)).ThenBy(group => group.Key, StringComparer.Ordinal).FirstOrDefault()?.Key;
}
