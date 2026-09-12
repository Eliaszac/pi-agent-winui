using System.Text.Json.Nodes;

namespace PiAgentGui.Utilities;

/// <summary>Explicit authentication choices preserve unrelated transport settings.</summary>
public static class McpAuthenticationSettings
{
    public static string Detect(JsonObject definition)
    {
        if (definition["url"] is null) return "local";
        if (definition["auth"] is JsonValue value)
        {
            if (value.TryGetValue<bool>(out var enabled) && !enabled) return "none";
            if (value.TryGetValue<string>(out var text) && text is "oauth" or "bearer") return text;
        }
        if (definition.ContainsKey("bearerToken") || definition.ContainsKey("bearerTokenEnv") || definition.ContainsKey("bearerTokenStore")) return "bearer";
        return "auto";
    }

    public static JsonObject Apply(JsonObject original, string mode)
    {
        var copy = original.DeepClone().AsObject();
        if (mode is "local" or "auto") return copy;
        if (mode is not ("none" or "oauth" or "bearer")) throw new ArgumentException("Choose an authentication method.");
        if (copy["url"] is null) throw new ArgumentException("Authentication selection requires an HTTP server.");
        foreach (var key in new[] { "bearerToken", "bearerTokenEnv", "bearerTokenStore" }) copy.Remove(key);
        if (copy["headers"] is JsonObject headers)
            foreach (var key in headers.Select(pair => pair.Key).Where(key => key.Equals("Authorization", StringComparison.OrdinalIgnoreCase)).ToArray()) headers.Remove(key);
        copy["auth"] = mode == "none" ? JsonValue.Create(false) : JsonValue.Create(mode);
        if (mode == "bearer") copy["bearerTokenStore"] = true;
        return copy;
    }
}
