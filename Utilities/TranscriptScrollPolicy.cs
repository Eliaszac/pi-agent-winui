namespace PiAgentGui.Utilities;

public static class TranscriptScrollPolicy
{
    public static double TailOffset(double currentOffset, double realizedBottom, double viewportHeight, double scrollableHeight)
        => Math.Clamp(currentOffset + realizedBottom - viewportHeight, 0, scrollableHeight);

    public static bool ShouldResumeFollowing(bool userScrolled, double previousOffset, double offset, double scrollableHeight)
        => userScrolled && offset > previousOffset + 0.5 && scrollableHeight - offset < 48;
}
