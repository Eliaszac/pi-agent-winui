namespace PiAgentGui.Utilities;

public static class ComposerEnterBehavior
{
    public static bool Sends(bool controlPressed, bool controlEnterToSend) => controlPressed == controlEnterToSend;
    public static string Hint(bool controlEnterToSend) => controlEnterToSend
        ? "Ctrl + Enter to send · Enter for a new line"
        : "Enter to send · Ctrl + Enter for a new line";
}
