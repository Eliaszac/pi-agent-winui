using PiAgentGui.Models.Applications;

namespace PiAgentGui.Services.Applications;

public interface IApplicationLocator
{
    IReadOnlyList<InstalledApplication> Discover();
    string? GetAssociatedExecutable(string directory);
}
