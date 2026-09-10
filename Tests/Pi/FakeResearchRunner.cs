using System.Collections.Concurrent;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Services.Conversations;

namespace PiAgentGui.Tests.Pi;

internal sealed class FakeResearchRunner : IResearchRunner
{
    public ConcurrentDictionary<Guid, TaskCompletionSource<string>> Started { get; } = new();
    public async Task<string> RunAsync(ResearchTask task, CancellationToken cancellationToken)
    {
        var result = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        Started[task.Id] = result;
        return await result.Task.WaitAsync(cancellationToken);
    }
}
