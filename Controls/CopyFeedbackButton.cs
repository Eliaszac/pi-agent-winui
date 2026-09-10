using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI.ViewManagement;

namespace PiAgentGui.Controls;

/// <summary>A native button with temporary, motion-aware copy confirmation.</summary>
public sealed class CopyFeedbackButton : ActionButton
{
    private readonly FontIcon copyIcon = new() { Glyph = "\uE8C8", FontSize = 14 };
    private readonly FontIcon checkIcon = new() { Glyph = "\uE73E", FontSize = 14, Opacity = 0 };
    private readonly DispatcherQueueTimer timer;
    private Storyboard? transition;

    public CopyFeedbackButton()
    {
        var icons = new Grid();
        icons.Children.Add(copyIcon);
        icons.Children.Add(checkIcon);
        Content = icons;
        timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(1600);
        timer.IsRepeating = false;
        timer.Tick += (_, _) => ShowState(false, animate: true);
        Unloaded += (_, _) => Reset();
        DataContextChanged += (_, _) => Reset();
    }

    public void ShowCopied()
    {
        timer.Stop();
        ShowState(true, animate: true);
        timer.Start();
    }

    private void Reset()
    {
        timer.Stop();
        ShowState(false, animate: false);
    }

    private void ShowState(bool copied, bool animate)
    {
        var copyOpacity = copyIcon.Opacity;
        var checkOpacity = checkIcon.Opacity;
        transition?.Stop();
        copyIcon.Opacity = copied ? 0 : 1;
        checkIcon.Opacity = copied ? 1 : 0;
        ToolTipService.SetToolTip(this, copied ? "Copied" : AutomationProperties.GetName(this));
        if (!animate || !IsLoaded || !new UISettings().AnimationsEnabled) return;
        transition = new Storyboard();
        AddFade(copyIcon, copyOpacity, copyIcon.Opacity);
        AddFade(checkIcon, checkOpacity, checkIcon.Opacity);
        transition.Begin();
    }

    private void AddFade(FontIcon target, double from, double to)
    {
        var animation = new DoubleAnimation
        {
            From = from, To = to, Duration = new Duration(TimeSpan.FromMilliseconds(180)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, "Opacity");
        transition!.Children.Add(animation);
    }
}

