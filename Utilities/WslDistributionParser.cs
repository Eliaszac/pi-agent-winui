using PiAgentGui.Models.Projects;

namespace PiAgentGui.Utilities;

public static class WslDistributionParser
{
    public static WslDistributionSnapshot Parse(string names, string verbose)
    {
        var installed = names.Replace("\0", "").Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(name => name.Trim().Trim('\uFEFF')).Where(name => name.Length > 0 && name is not ("docker-desktop" or "docker-desktop-data"))
            .Distinct(StringComparer.Ordinal).ToArray();
        var defaultRow = verbose.Replace("\0", "").Split('\n').Select(line => line.TrimStart().TrimStart('\uFEFF'))
            .FirstOrDefault(line => line.StartsWith('*'))?.TrimStart('*').TrimStart();
        var preferred = installed.OrderByDescending(name => name.Length).FirstOrDefault(name => defaultRow is not null &&
            defaultRow.StartsWith(name, StringComparison.Ordinal) && (defaultRow.Length == name.Length || char.IsWhiteSpace(defaultRow[name.Length])));
        return new(installed, preferred);
    }
}
