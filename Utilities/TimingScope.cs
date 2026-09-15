using System.Diagnostics;

namespace PiAgentGui.Utilities;

internal sealed class TimingScope(ConversationLoadTiming timing, string stage) : IDisposable
{
    private readonly long started = Stopwatch.GetTimestamp();
    public void Dispose() => timing.Mark(stage, Math.Round(Stopwatch.GetElapsedTime(started).TotalMilliseconds, 2));
}
