using Microsoft.UI.Dispatching;
using PiAgentGui.Services.Conversations;

namespace PiAgentGui.Services.Windowing;

public sealed class DispatcherQueueUiDispatcher(DispatcherQueue queue) : IUiDispatcher
{
    public bool Post(Action action) => queue.TryEnqueue(() => action());
    public bool PostBackground(Action action) => queue.TryEnqueue(DispatcherQueuePriority.Low, () => action());
}
