namespace PiAgentGui.Utilities;

public static class DockerExecutableLocator
{
    public static string Find()
    {
        var bundled = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Docker", "Docker", "resources", "bin", "docker.exe");
        return File.Exists(bundled) ? bundled : "docker.exe";
    }
}
