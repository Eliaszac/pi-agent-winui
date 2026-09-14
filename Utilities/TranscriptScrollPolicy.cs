namespace PiAgentGui.Utilities;

public static class TranscriptScrollPolicy
{
    public static double TailOffset(double currentOffset, double realizedBottom, double viewportHeight, double scrollableHeight)
        // Shrinking/removing rows is handled by native anchoring. A streaming
        // correction must never pull the reader back to an earlier response.
        => Math.Clamp(currentOffset + Math.Max(0, realizedBottom - viewportHeight), 0, scrollableHeight);

    public static bool ShouldResumeFollowing(bool userScrolled, double previousOffset, double offset, double scrollableHeight)
        => userScrolled && offset > previousOffset + 0.5 && scrollableHeight - offset < 48;

    public static bool ShouldAnimateTail(bool initialNavigation, bool animationsEnabled, double distance, double viewportHeight)
        => !initialNavigation && animationsEnabled && distance >= 1 && distance <= viewportHeight / 2;
}
