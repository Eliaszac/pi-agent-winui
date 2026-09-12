namespace PiAgentGui.Models.Pi;

public sealed record OllamaModel(string Id, bool Tools, bool Vision, bool Thinking, int? MaximumContext, string? Error = null,
    int? AllocatedContext = null, string ContextSource = "fallback")
{
    public int ImportContext => Math.Min(AllocatedContext ?? 4096, MaximumContext ?? 2097152);
}
