using PiAgentGui.Models.Applications;
using PiAgentGui.Services.Applications;

namespace PiAgentGui.Tests.Settings;

internal sealed class PreferenceApplicationLocator : IApplicationLocator
{
    public IReadOnlyList<InstalledApplication> Discover() => [new("vscode", "VS Code", @"C:\Example\Code.exe", "vscode.png")];
    public string? GetAssociatedExecutable(string directory) => null;
}
