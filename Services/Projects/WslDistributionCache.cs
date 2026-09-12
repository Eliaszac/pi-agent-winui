using PiAgentGui.Models.Projects;

namespace PiAgentGui.Services.Projects;

/// <summary>Shares one background inventory across dialogs for this app lifetime.</summary>
public sealed class WslDistributionCache(Func<Task<WslDistributionSnapshot>> discover)
{
    private readonly object gate = new();
    private Task<WslDistributionSnapshot>? cached;
    public Task<WslDistributionSnapshot> GetAsync(bool refresh = false)
    {
        lock (gate)
        {
            if (cached is null || refresh && cached.IsCompleted) cached = Task.Run(DiscoverAsync);
            return cached;
        }
    }

    private async Task<WslDistributionSnapshot> DiscoverAsync()
    {
        try { return await discover().ConfigureAwait(false); }
        catch (Exception) { return new([], null, "Couldn't detect WSL distributions. Check that WSL is installed, then refresh."); }
    }
}
