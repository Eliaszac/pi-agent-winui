namespace PiAgentGui.Tests.Pi;

internal sealed class ManualTimeProvider : TimeProvider
{
    private long timestamp;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => timestamp;
    public void Advance(TimeSpan elapsed) => timestamp += elapsed.Ticks;
}
