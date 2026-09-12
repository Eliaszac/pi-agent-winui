namespace PiAgentGui.Utilities;

/// <summary>Reads an explicit model context setting; architecture limits are not allocations.</summary>
public static class OllamaContext
{
    public static int? ReadParameter(string? parameters)
    {
        foreach (var line in (parameters ?? "").Split('\n'))
        {
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && parts[0] == "num_ctx" && int.TryParse(parts[1], out var value) && value is >= 1024 and <= 2097152)
                return value;
        }
        return null;
    }
}
