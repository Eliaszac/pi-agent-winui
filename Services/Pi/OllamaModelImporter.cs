using System.Text.Json.Nodes;
using PiAgentGui.Models.Pi;
using PiAgentGui.Utilities;

namespace PiAgentGui.Services.Pi;

/// <summary>Adds selected models to Pi without replacing existing providers or model definitions.</summary>
public sealed class OllamaModelImporter(string agentDirectory)
{
    private readonly GlobalConfigurationFile config = new(Path.Combine(agentDirectory, "models.json"));
    private readonly GlobalConfigurationFile settings = new(Path.Combine(agentDirectory, "settings.json"));
    private readonly GlobalConfigurationFile connections = new(Path.Combine(agentDirectory, "pi-desktop-ollama.json"));

    public async Task<IReadOnlyList<Uri>> KnownEndpointsAsync(CancellationToken token = default)
    {
        var root = await connections.ReadAsync(token);
        if (root["servers"] is not null && root["servers"] is not JsonArray) throw new IOException("Saved Ollama connections are invalid.");
        var saved = (root["servers"] as JsonArray ?? []).Select(node => OllamaEndpoint.Parse(node?.GetValue<string>()
            ?? throw new IOException("A saved Ollama address is invalid.")));
        return new[] { OllamaEndpoint.Parse(OllamaEndpoint.Local) }.Concat(saved)
            .Concat((await ReadAsync(token)).Select(item => item.Endpoint)).Distinct().ToArray();
    }

    public Task RememberAsync(Uri endpoint, CancellationToken token = default) => connections.UpdateAsync(root =>
    {
        if (root["servers"] is not null && root["servers"] is not JsonArray) throw new IOException("Saved Ollama connections are invalid.");
        var servers = root["servers"] as JsonArray ?? new JsonArray();
        if (!servers.Any(node => OllamaEndpoint.Parse(node!.GetValue<string>()) == endpoint)) servers.Add(endpoint.AbsoluteUri);
        if (root["servers"] is null) root["servers"] = servers;
    }, token);

    public async Task<IReadOnlyList<OllamaRegistration>> ReadAsync(CancellationToken token = default)
    {
        var root = await config.ReadAsync(token);
        return Registrations(root);
    }

    public async Task<string?> ImportAsync(Uri endpoint, IReadOnlyList<OllamaModel> models, int contextWindow, CancellationToken token = default) =>
        (await ImportCoreAsync(endpoint, models, contextWindow, false, token)).Warning;

    public Task<OllamaImportResult> ImportNewAsync(Uri endpoint, IReadOnlyList<OllamaModel> models, CancellationToken token = default) =>
        ImportCoreAsync(endpoint, models, null, true, token);

    private async Task<OllamaImportResult> ImportCoreAsync(Uri endpoint, IReadOnlyList<OllamaModel> models, int? contextWindow, bool skipExisting, CancellationToken token)
    {
        endpoint = OllamaEndpoint.Parse(endpoint.AbsoluteUri);
        if (models.Count == 0 || models.Count > 256 || models.Any(model => !model.Tools || model.Error is not null))
            throw new ArgumentException("Select models that support tool calling.");
        if (contextWindow is < 1024 or > 2097152) throw new ArgumentException("Enter a context length between 1,024 and 2,097,152 tokens.");
        var budgets = new Dictionary<string, int>(StringComparer.Ordinal);
        var added = 0;
        await config.UpdateAsync(root =>
        {
            var registrations = Registrations(root);
            var existing = registrations.FirstOrDefault(item => item.Endpoint == endpoint);
            var id = existing?.ProviderId ?? OllamaEndpoint.ProviderId(endpoint);
            var providers = root["providers"] as JsonObject ?? new JsonObject();
            if (existing is null && providers.ContainsKey(id)) throw new IOException("This provider ID is already used by another configuration. Nothing was replaced.");
            var provider = existing is null ? new JsonObject
            {
                ["baseUrl"] = new Uri(endpoint, "v1").AbsoluteUri,
                ["api"] = "openai-completions",
                ["apiKey"] = "ollama",
                ["compat"] = new JsonObject { ["supportsDeveloperRole"] = false, ["supportsReasoningEffort"] = false },
                ["models"] = new JsonArray()
            } : (JsonObject)providers[id]!;
            if (provider["models"] is not JsonArray target) throw new IOException("The existing Ollama model list is invalid.");
            if (provider["apiKey"] is null) provider["apiKey"] = "ollama";
            var known = target.Select(model => model?["id"]?.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
            foreach (var model in models.DistinctBy(model => model.Id))
            {
                if (!known.Add(model.Id))
                {
                    if (skipExisting) continue;
                    throw new IOException($"{model.Id} is already registered. Discover again before importing.");
                }
                added++;
                var context = contextWindow is { } requested ? Math.Min(requested, model.MaximumContext ?? requested) : model.ImportContext;
                if (context < 65536) budgets.Add(id + "/" + model.Id, context);
                target.Add(new JsonObject
                {
                    ["id"] = model.Id, ["name"] = model.Id, ["reasoning"] = model.Thinking,
                    ["input"] = model.Vision ? new JsonArray("text", "image") : new JsonArray("text"),
                    ["contextWindow"] = context, ["maxTokens"] = Math.Min(4096, context / 4),
                    ["cost"] = new JsonObject { ["input"] = 0, ["output"] = 0, ["cacheRead"] = 0, ["cacheWrite"] = 0 },
                    ["compat"] = new JsonObject { ["supportsDeveloperRole"] = false, ["supportsReasoningEffort"] = false }
                });
            }
            if (existing is null) providers.Add(id, provider);
            if (root["providers"] is null) root["providers"] = providers;
        }, token);
        if (budgets.Count == 0) return new(added, null);
        try
        {
            await settings.UpdateAsync(root =>
            {
                if (root["compaction"] is not null && root["compaction"] is not JsonObject)
                    throw new IOException("Existing compaction settings are invalid.");
                var compaction = root["compaction"] as JsonObject ?? new JsonObject();
                if (compaction["modelOverrides"] is not null && compaction["modelOverrides"] is not JsonObject)
                    throw new IOException("Existing model compaction overrides are invalid.");
                var overrides = compaction["modelOverrides"] as JsonObject ?? new JsonObject();
                foreach (var (key, context) in budgets)
                {
                    if (overrides[key] is not null && overrides[key] is not JsonObject)
                        throw new IOException("An existing model compaction override is invalid.");
                    var model = overrides[key] as JsonObject ?? new JsonObject();
                    // Preserve explicit user overrides; avoid the large global defaults for tiny models.
                    if (!model.ContainsKey("reserveTokens")) model["reserveTokens"] = Math.Min(4096, context / 4);
                    if (!model.ContainsKey("keepRecentTokens")) model["keepRecentTokens"] = Math.Min(20000, context / 2);
                    if (overrides[key] is null) overrides[key] = model;
                }
                if (compaction["modelOverrides"] is null) compaction["modelOverrides"] = overrides;
                if (root["compaction"] is null) root["compaction"] = compaction;
            }, token);
            return new(added, null);
        }
        catch (Exception)
        {
            // models.json was already committed. Never misreport this as an uncommitted import or replay it.
            return new(added, "Models were imported, but their small-context compaction settings couldn't be saved. Check Pi settings before using them.");
        }
    }

    private static IReadOnlyList<OllamaRegistration> Registrations(JsonObject root)
    {
        if (root["providers"] is null) return [];
        if (root["providers"] is not JsonObject providers) throw new IOException("Existing providers must be a JSON object.");
        var result = new List<OllamaRegistration>();
        foreach (var (id, node) in providers)
        {
            if (id != "ollama" && !id.StartsWith("ollama-remote-", StringComparison.Ordinal)) continue;
            if (node is not JsonObject provider || provider["baseUrl"] is not JsonValue url || !url.TryGetValue<string>(out var address)
                || provider["api"]?.GetValue<string>() != "openai-completions" || provider["models"] is not JsonArray models)
                throw new IOException("An existing Ollama provider has an unsupported configuration. It was not changed.");
            result.Add(new(id, OllamaEndpoint.Parse(address), models.Select(model => model?["id"]?.GetValue<string>()
                ?? throw new IOException("An existing model is missing its ID.")).ToHashSet(StringComparer.Ordinal)));
        }
        return result;
    }
}
