using System.Collections;
using PiAgentGui.Models.Projects;

namespace PiAgentGui.Utilities;

public static class SshTerminalEnvironment
{
    public static string Create(ExecutionTarget target)
    {
        var values = Environment.GetEnvironmentVariables().Cast<DictionaryEntry>().ToDictionary(item => (string)item.Key, item => (string?)item.Value, StringComparer.OrdinalIgnoreCase);
        SshLaunchOptions.ConfigureEnvironment(values, target);
        return string.Join('\0', values.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).Select(pair => pair.Key + "=" + pair.Value)) + "\0\0";
    }
}
