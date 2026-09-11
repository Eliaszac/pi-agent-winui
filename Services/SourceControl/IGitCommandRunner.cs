using PiAgentGui.Models.SourceControl;

namespace PiAgentGui.Services.SourceControl;

public interface IGitCommandRunner
{
    Task<GitResult> RunAsync(string directory, IReadOnlyList<string> arguments, CancellationToken cancellationToken);
}
