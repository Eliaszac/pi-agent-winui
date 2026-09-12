using PiAgentGui.Models.Pi;

namespace PiAgentGui.Services.Pi;

/// <summary>Discovers and registers new tool-capable models without changing existing entries.</summary>
public sealed class OllamaModelSync(OllamaClient client, OllamaModelImporter importer)
{
    public async Task<OllamaSyncResult> SyncAsync(Uri endpoint, bool remember, CancellationToken token = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        var names = await client.ListAsync(endpoint, timeout.Token);
        // Explicitly connected empty servers still need to be remembered for future model installs.
        if (remember) await importer.RememberAsync(endpoint, timeout.Token);
        var known = (await importer.ReadAsync(timeout.Token)).FirstOrDefault(item => item.Endpoint == endpoint)?.ModelIds ?? new HashSet<string>();
        var missing = names.Where(name => !known.Contains(name)).ToArray();
        if (missing.Length == 0) return new(endpoint, names.Count, 0, [], null, names);
        IReadOnlyDictionary<string, int> contexts;
        try { contexts = await client.ReadAllocatedContextsAsync(endpoint, timeout.Token); }
        catch (Exception) when (!timeout.IsCancellationRequested) { contexts = new Dictionary<string, int>(); }
        using var gate = new SemaphoreSlim(4);
        var models = await Task.WhenAll(missing.Select(async name =>
        {
            await gate.WaitAsync(timeout.Token);
            try
            {
                var model = await client.InspectAsync(endpoint, name, timeout.Token);
                return contexts.TryGetValue(name, out var context)
                    ? model with { AllocatedContext = context, ContextSource = "loaded model" } : model;
            }
            catch (Exception) when (!timeout.IsCancellationRequested)
            { return new OllamaModel(name, false, false, false, null, "Couldn't inspect this model. Refresh to retry."); }
            finally { gate.Release(); }
        }));
        var eligible = models.Where(model => model.Tools && model.Error is null).ToArray();
        var imported = eligible.Length == 0 ? new OllamaImportResult(0, null) : await importer.ImportNewAsync(endpoint, eligible, timeout.Token);
        var warning = imported.Warning;
        if (models.Any(model => model.Error is not null)) warning = string.Join(" ", new[] { warning, "Some model details couldn't be read. Refresh to retry." }.Where(text => text is not null));
        return new(endpoint, names.Count, imported.Added, models, warning, names);
    }
}
