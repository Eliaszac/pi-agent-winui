using PiAgentGui.Models.Projects;
using PiAgentGui.Models.SourceControl;
using PiAgentGui.Services.Projects;
using PiAgentGui.Utilities;

namespace PiAgentGui.Services.SourceControl;

public sealed class TargetGitCommandRunner(ExecutionTarget target, TargetCommandRunner runner) : IGitCommandRunner
{
    public Task<GitResult> RunAsync(string directory, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (!directory.StartsWith('/')) throw new ArgumentException("Git requires a target-native directory.");
        var args = new[] { "git", "--no-pager", "--literal-pathspecs", "-c", "color.ui=false" }.Concat(arguments);
        return runner.RunAsync(target, "cd -- " + PosixShell.Quote(directory) +
            " && GIT_TERMINAL_PROMPT=0 GCM_INTERACTIVE=never GIT_OPTIONAL_LOCKS=0 GIT_MERGE_AUTOEDIT=no LC_ALL=C " + string.Join(" ", args.Select(PosixShell.Quote)), cancellationToken);
    }
}
