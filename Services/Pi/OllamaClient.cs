using System.Net.Http.Json;
using System.Text.Json;
using PiAgentGui.Models.Pi;
using PiAgentGui.Utilities;

namespace PiAgentGui.Services.Pi;

/// <summary>Reads Ollama inventory and metadata without loading, downloading or running models.</summary>
public sealed class OllamaClient(HttpClient http)
{
    public async Task<IReadOnlyList<string>> ListAsync(Uri endpoint, CancellationToken token = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(endpoint, "api/tags"));
        using var document = await ReadAsync(request, token);
        if (!document.RootElement.TryGetProperty("models", out var models) || models.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("This server did not return an Ollama model list.");
        if (models.GetArrayLength() > 256) throw new InvalidDataException("This server lists more than 256 models. Use a smaller Ollama inventory.");
        var names = new List<string>();
        foreach (var model in models.EnumerateArray())
        {
            if (!model.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(name.GetString()) || name.GetString()!.Length > 256 || name.GetString()!.Any(char.IsControl))
                throw new InvalidDataException("The server returned an invalid model name.");
            names.Add(name.GetString()!);
        }
        return names.Distinct(StringComparer.Ordinal).Order(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<OllamaModel> InspectAsync(Uri endpoint, string model, CancellationToken token = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(endpoint, "api/show"))
        { Content = JsonContent.Create(new { model, verbose = false }) };
        using var document = await ReadAsync(request, token);
        var root = document.RootElement;
        if (!root.TryGetProperty("capabilities", out var capabilities) || capabilities.ValueKind != JsonValueKind.Array)
            return new(model, false, false, false, null, "Capabilities unavailable. Update Ollama and refresh.");
        var values = capabilities.EnumerateArray().Where(value => value.ValueKind == JsonValueKind.String).Select(value => value.GetString()).ToHashSet();
        int? context = null;
        if (root.TryGetProperty("model_info", out var info) && info.ValueKind == JsonValueKind.Object
            && info.TryGetProperty("general.architecture", out var architecture) && architecture.ValueKind == JsonValueKind.String
            && info.TryGetProperty(architecture.GetString() + ".context_length", out var size)
            && size.ValueKind == JsonValueKind.Number && size.TryGetInt32(out var maximum) && maximum >= 1024) context = maximum;
        var configured = root.TryGetProperty("parameters", out var parameters) && parameters.ValueKind == JsonValueKind.String
            ? OllamaContext.ReadParameter(parameters.GetString()) : null;
        return new(model, values.Contains("tools") && values.Contains("completion"), values.Contains("vision"), values.Contains("thinking"), context,
            AllocatedContext: configured, ContextSource: configured.HasValue ? "model setting" : "fallback");
    }

    public async Task<IReadOnlyDictionary<string, int>> ReadAllocatedContextsAsync(Uri endpoint, CancellationToken token = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(endpoint, "api/ps"));
        using var document = await ReadAsync(request, token);
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        if (!document.RootElement.TryGetProperty("models", out var models) || models.ValueKind != JsonValueKind.Array) return result;
        foreach (var model in models.EnumerateArray())
        {
            if (model.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String
                && model.TryGetProperty("context_length", out var size) && size.ValueKind == JsonValueKind.Number
                && size.TryGetInt32(out var context) && context is >= 1024 and <= 2097152 && name.GetString() is { } id)
                result[id] = context;
        }
        return result;
    }

    private async Task<JsonDocument> ReadAsync(HttpRequestMessage request, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Ollama returned HTTP {(int)response.StatusCode}. Check the server address and access settings.");
        const int limit = 2 * 1024 * 1024;
        if (response.Content.Headers.ContentLength > limit) throw new InvalidDataException("Ollama's response is too large.");
        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var memory = new MemoryStream();
        var buffer = new byte[8192];
        int length;
        while ((length = await stream.ReadAsync(buffer, timeout.Token)) > 0)
        {
            if (memory.Length + length > limit) throw new InvalidDataException("Ollama's response is too large.");
            memory.Write(buffer, 0, length);
        }
        return JsonDocument.Parse(memory.ToArray());
    }
}
