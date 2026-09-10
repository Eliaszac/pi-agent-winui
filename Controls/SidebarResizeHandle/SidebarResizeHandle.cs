using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;

namespace PiAgentGui.Controls;

/// <summary>A focusable pointer grip using root coordinates so layout changes cannot distort dragging.</summary>
public sealed class SidebarResizeHandle : Control
{
    private bool isDragging;
    private double previousX;

    /// <summary>Notifies the shell when a resize begins.</summary>
    public event EventHandler? ResizeStarted;
    /// <summary>Reports pointer movement in root-relative effective pixels.</summary>
    public event EventHandler<double>? ResizeDelta;

    /// <summary>Creates a keyboard-focusable horizontal resize grip.</summary>
    public SidebarResizeHandle()
    {
        IsTabStop = true;
        UseSystemFocusVisuals = true;
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
        ManipulationMode = ManipulationModes.None;
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerRoutedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (!CapturePointer(e.Pointer)) return;
        Focus(FocusState.Pointer);
        previousX = e.GetCurrentPoint(XamlRoot.Content).Position.X;
        isDragging = true;
        ResizeStarted?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerRoutedEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!isDragging) return;
        var x = e.GetCurrentPoint(XamlRoot.Content).Position.X;
        ResizeDelta?.Invoke(this, x - previousX);
        previousX = x;
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerRoutedEventArgs e)
    {
        base.OnPointerReleased(e);
        isDragging = false;
        ReleasePointerCapture(e.Pointer);
        VisualStateManager.GoToState(this, "Normal", false);
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerRoutedEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        isDragging = false;
    }

    /// <inheritdoc />
    protected override void OnPointerEntered(PointerRoutedEventArgs e)
    {
        base.OnPointerEntered(e);
        VisualStateManager.GoToState(this, "PointerOver", false);
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerRoutedEventArgs e)
    {
        base.OnPointerExited(e);
        if (!isDragging) VisualStateManager.GoToState(this, "Normal", false);
    }
}
