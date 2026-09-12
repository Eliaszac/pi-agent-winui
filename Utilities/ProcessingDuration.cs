namespace PiAgentGui.Utilities;

public static class ProcessingDuration
{
    public static string Format(TimeSpan elapsed)
    {
        var seconds = Math.Max(0, (long)elapsed.TotalSeconds);
        return seconds < 60 ? $"{seconds}s" : $"{seconds / 60}m {seconds % 60:00}s";
    }
}
