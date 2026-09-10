namespace PiAgentGui.Configuration;

/// <summary>Runtime configuration, separate from project metadata and credentials.</summary>
public sealed record PiRuntimeOptions
{
    public string? ExecutablePath { get; init; }
    public string? NodePath { get; init; }
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>Reads optional installation overrides once at application startup.</summary>
    public static PiRuntimeOptions FromEnvironment() => new()
    {
        ExecutablePath = Environment.GetEnvironmentVariable("PI_GUI_PI_EXECUTABLE"),
        NodePath = Environment.GetEnvironmentVariable("PI_GUI_NODE_EXECUTABLE")
    };
}
