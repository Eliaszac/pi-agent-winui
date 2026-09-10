using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Automation;
using PiAgentGui.Utilities;

namespace PiAgentGui.Controls;

/// <summary>Shows a redacted location, revealing it only briefly after explicit activation.</summary>
public sealed class ProjectPathButton : ActionButton
{
    public static readonly DependencyProperty FullPathProperty = DependencyProperty.Register(nameof(FullPath), typeof(string),
        typeof(ProjectPathButton), new PropertyMetadata("", (sender, _) => ((ProjectPathButton)sender).HidePath()));
    private readonly TextBlock label = new() { TextTrimming = TextTrimming.CharacterEllipsis, FontSize = 12 };
    private readonly DispatcherQueueTimer timer;
    private bool revealed;
    public string FullPath { get => (string)GetValue(FullPathProperty); set => SetValue(FullPathProperty, value); }

    public ProjectPathButton()
    {
        Content = label;
        timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(5);
        timer.IsRepeating = false;
        timer.Tick += (_, _) => HidePath();
        Click += (_, _) =>
        {
            if (revealed) { HidePath(); return; }
            revealed = true;
            label.Text = FullPath;
            AutomationProperties.SetName(this, "Hide project path");
            ToolTipService.SetToolTip(this, "Click to hide; hides automatically after 5 seconds");
            timer.Start();
        };
        Unloaded += (_, _) => HidePath();
        LostFocus += (_, _) => HidePath();
        HidePath();
    }

    public void HidePath()
    {
        timer.Stop();
        revealed = false;
        IsEnabled = !string.IsNullOrWhiteSpace(FullPath);
        label.Text = ProjectPathDisplay.Redact(FullPath);
        AutomationProperties.SetName(this, $"Reveal project path: {label.Text}");
        ToolTipService.SetToolTip(this, "Reveal full path for 5 seconds");
    }
}

