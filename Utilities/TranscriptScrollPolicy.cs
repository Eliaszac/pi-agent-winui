namespace PiAgentGui.Utilities;

public static class TranscriptScrollPolicy
{
    public static bool ShouldResumeFollowing(bool userScrolled, double previousOffset, double offset, double scrollableHeight)
        => userScrolled && offset > previousOffset + 0.5 && scrollableHeight - offset < 48;
}
