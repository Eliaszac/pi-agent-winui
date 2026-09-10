using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace PiAgentGui.Controls;

/// <summary>Accessible syntax foregrounds for the native code surface.</summary>
internal static class CodeSyntaxColors
{
    public static Brush? Get(string kind, bool dark)
    {
        var rgb = (kind, dark) switch
        {
            ("keyword", true) => 0xC586C0, ("keyword", false) => 0x7F007F,
            ("string", true) => 0xCE9178, ("string", false) => 0xA31515,
            ("comment", true) => 0x8BAF78, ("comment", false) => 0x427A30,
            ("number", true) => 0xB5CEA8, ("number", false) => 0x096F50,
            ("type", true) => 0x4EC9B0, ("type", false) => 0x176F76,
            ("function", true) => 0xDCDCAA, ("function", false) => 0x795E26,
            ("variable", true) => 0x9CDCFE, ("variable", false) => 0x005A9E,
            _ => -1
        };
        return rgb < 0 ? null : new SolidColorBrush(Color.FromArgb(255, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));
    }
}
