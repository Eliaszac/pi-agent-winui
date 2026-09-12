using Microsoft.UI.Xaml.Automation;

namespace PiAgentGui.Controls;

/// <summary>A compact snippet action with native feedback and an accessible label.</summary>
internal sealed class SnippetIconButton : ActionButton
{
    internal SnippetIconButton(string glyph, string label)
    {
        Style = (Style)Application.Current.Resources["ShellIconButtonStyle"];
        SetAction(glyph, label);
    }

    internal void SetAction(string glyph, string label)
    {
        Content = new FontIcon { Glyph = glyph, FontSize = 14 };
        AutomationProperties.SetName(this, label);
        ToolTipService.SetToolTip(this, label);
    }
}
