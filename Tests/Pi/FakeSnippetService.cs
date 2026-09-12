using PiAgentGui.Services.Conversations;

namespace PiAgentGui.Tests.Pi;

internal sealed class FakeSnippetService : ISnippetService
{
    public string TargetLabel => "WSL · test";
    public Func<Action<string, bool>, CancellationToken, Task<int>> Run { get; set; } = (_, _) => Task.FromResult(0);
    public Task<int> RunAsync(string label, string code, Action<string, bool> output, CancellationToken token) => Run(output, token);
    public Task<string> SaveAsync(string label, string code, CancellationToken token) => Task.FromResult("/project/snippet.py");
}
