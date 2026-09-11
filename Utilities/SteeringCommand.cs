namespace PiAgentGui.Utilities;

public static class SteeringCommand
{
    public static bool Matches(string text)
    {
        var value = text.TrimStart();
        return value.StartsWith("/steer", StringComparison.OrdinalIgnoreCase)
            && (value.Length == 6 || char.IsWhiteSpace(value[6]));
    }
}
