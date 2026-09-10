using Microsoft.UI.Input;

namespace PiAgentGui.Controls;

/// <summary>A composer button with a hand cursor for its available action.</summary>
public sealed class ComposerActionButton : ActionButton
{
    public ComposerActionButton()
    {
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
    }
}

