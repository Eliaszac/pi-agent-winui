namespace PiAgentGui.Utilities;

public static class ComposerEnterBehavior
{
    public static bool Sends(bool controlPressed, bool controlEnterToSend) => controlPressed == controlEnterToSend;
}
