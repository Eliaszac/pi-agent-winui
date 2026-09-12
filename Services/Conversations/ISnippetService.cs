namespace PiAgentGui.Services.Conversations;

/// <summary>Target-boundary contract for explicit snippet execution and saving.</summary>
public interface ISnippetService
{
    string TargetLabel { get; }
    Task<string> SaveAsync(string label, string code, CancellationToken token);
    Task<int> RunAsync(string label, string code, Action<string, bool> output, CancellationToken token);
}
