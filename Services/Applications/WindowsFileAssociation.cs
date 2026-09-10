using System.Runtime.InteropServices;
using System.Text;

namespace PiAgentGui.Services.Applications;

public static class WindowsFileAssociation
{
    public static string? GetExecutable(string extension)
    {
        uint size = 0;
        AssocQueryString(0, 2, extension, "open", null, ref size);
        if (size is 0 or > 32768) return null;
        var output = new StringBuilder((int)size);
        return AssocQueryString(0, 2, extension, "open", output, ref size) == 0 ? output.ToString() : null;
    }

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, EntryPoint = "AssocQueryStringW")]
    private static extern int AssocQueryString(uint flags, uint associationString, string association, string? extra, StringBuilder? output, ref uint length);
}
