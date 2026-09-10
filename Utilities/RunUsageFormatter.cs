using PiAgentGui.Models.Conversations;

namespace PiAgentGui.Utilities;

public static class RunUsageFormatter
{
    public static string Format(RunUsage usage)
    {
        var seconds = Math.Max(1L, (long)Math.Floor(usage.Elapsed.TotalSeconds));
        var duration = seconds < 60 ? $"{seconds} s" : seconds % 60 == 0 ? $"{seconds / 60} min" : $"{seconds / 60} min {seconds % 60} s";
        return $"{(usage.Tokens is long tokens ? $"{tokens:N0} tokens" : "Tokens unavailable")} · {duration}";
    }
}
