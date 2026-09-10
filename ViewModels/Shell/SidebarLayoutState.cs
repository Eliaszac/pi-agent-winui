using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Shell;

/// <summary>Owns sidebar sizing independently of pointer input and XAML layout.</summary>
public sealed class SidebarLayoutState : ObservableObject
{
    /// <summary>The width of the collapsed navigation rail.</summary>
    public const double CollapsedWidth = 48;
    /// <summary>The minimum readable expanded sidebar width.</summary>
    public const double MinimumWidth = 220;
    private const double CollapseThreshold = 140;
    private double expandedWidth = 280;
    private bool isOpen = true;
    private bool isOverlay;
    private double maximumWidth = 420;

    /// <summary>Gets the width restored when the sidebar opens.</summary>
    public double ExpandedWidth
    {
        get => expandedWidth;
        private set { if (SetProperty(ref expandedWidth, value)) OnPropertyChanged(nameof(ContentWidth)); }
    }
    /// <summary>Gets or sets whether the sidebar is expanded.</summary>
    public bool IsOpen
    {
        get => isOpen;
        set
        {
            if (!SetProperty(ref isOpen, value)) return;
            OnPropertyChanged(nameof(IsCollapsed));
            OnPropertyChanged(nameof(ContentWidth));
        }
    }
    /// <summary>Gets the actual pane content width, keeping the resize grip reachable when collapsed.</summary>
    public double ContentWidth => IsOpen ? ExpandedWidth : CollapsedWidth;
    /// <summary>Gets the maximum width allowed by the current window.</summary>
    public double MaximumWidth => maximumWidth;
    /// <summary>Gets whether only the navigation rail is visible.</summary>
    public bool IsCollapsed => !IsOpen;
    /// <summary>Gets whether the sidebar overlays narrow content.</summary>
    public bool IsOverlay { get => isOverlay; private set => SetProperty(ref isOverlay, value); }

    /// <summary>Updates constraints when the window width changes.</summary>
    public void SetAvailableWidth(double width)
    {
        var overlay = width < 760;
        if (overlay && !IsOverlay) IsOpen = false;
        IsOverlay = overlay;
        maximumWidth = Math.Clamp(width - (overlay ? 48 : 360), MinimumWidth, 420);
        ExpandedWidth = Math.Min(ExpandedWidth, maximumWidth);
    }

    /// <summary>Applies a requested width from dragging or keyboard input.</summary>
    public void ResizeTo(double width)
    {
        if (width < CollapseThreshold) { IsOpen = false; return; }
        ExpandedWidth = Math.Clamp(width, MinimumWidth, maximumWidth);
        IsOpen = true;
    }

    /// <summary>Toggles expansion while preserving the last expanded width.</summary>
    public void Toggle() => IsOpen = !IsOpen;
}
