using Microsoft.UI.Input;

namespace PiAgentGui.Controls;

/// <summary>Provides a hand cursor for explorer rows and preserves text editing cursors.</summary>
public sealed class ExplorerRowGrid : Grid
{
    public ExplorerRowGrid()
    {
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
    }
}
