using System.Collections.Concurrent;
using PiAgentGui.Services.Conversations;

namespace PiAgentGui.Tests.Pi;

internal sealed class QueuedUiDispatcher : IUiDispatcher
{
    private readonly ConcurrentQueue<Action> queue = new();
    private readonly ConcurrentQueue<Action> background = new();
    public bool Post(Action action) { queue.Enqueue(action); return true; }
    public bool PostBackground(Action action) { background.Enqueue(action); return true; }
    public bool DrainOne()
    {
        if (!queue.TryDequeue(out var action) && !background.TryDequeue(out action)) return false;
        action();
        return true;
    }
    public void Drain() { while (DrainOne()) { } }
}
