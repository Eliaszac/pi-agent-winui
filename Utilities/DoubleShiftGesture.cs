namespace PiAgentGui.Utilities;

/// <summary>Recognizes two short, unmodified Shift taps without treating typing as a gesture.</summary>
public sealed class DoubleShiftGesture
{
    private long? pressed;
    private long? released;
    public void Reset() { pressed = released = null; }
    public void Down(bool shift, bool modified, long milliseconds)
    {
        if (!shift || modified) { Reset(); return; }
        pressed ??= milliseconds;
    }
    public bool Up(bool shift, long milliseconds)
    {
        if (!shift || pressed is not { } start) { Reset(); return false; }
        pressed = null;
        if (milliseconds - start > 250) { Reset(); return false; }
        var match = released is { } last && milliseconds - last <= 400;
        released = match ? null : milliseconds;
        return match;
    }
}
