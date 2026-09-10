using System.Collections.Concurrent;
using PiAgentGui.Services.Conversations;

namespace PiAgentGui.Tests.Pi;

internal sealed class QueuedUiDispatcher : IUiDispatcher
{
    private readonly ConcurrentQueue<Action> queue = new();
    public bool Post(Action action) { queue.Enqueue(action); return true; }
    public void Drain() { while (queue.TryDequeue(out var action)) action(); }
}
