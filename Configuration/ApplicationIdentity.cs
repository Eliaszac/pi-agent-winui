namespace PiAgentGui.Configuration;

public static class ApplicationIdentity
{
    public const string Name = "Pi desktop";
    public static string Version { get; } = System.Reflection.CustomAttributeExtensions
        .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(typeof(ApplicationIdentity).Assembly)
        ?.InformationalVersion.Split('+')[0] ?? typeof(ApplicationIdentity).Assembly.GetName().Version?.ToString(3) ?? "Unknown";
}
