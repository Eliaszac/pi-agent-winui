using PiAgentGui.Models.SourceControl;
using PiAgentGui.Services.SourceControl;

namespace PiAgentGui.Tests.SourceControl;

public sealed class FakeGitCommandRunner : IGitCommandRunner
{
    public List<string[]> Commands { get; } = [];
    public string Head { get; set; } = "refs/heads/main";
    public Func<IReadOnlyList<string>, GitResult>? Respond { get; set; }
    public Task<GitResult> RunAsync(string directory, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Commands.Add(arguments.ToArray());
        return Task.FromResult(Respond?.Invoke(arguments) ?? new GitResult(0, arguments[0] == "symbolic-ref" ? Head : "", ""));
    }
}
