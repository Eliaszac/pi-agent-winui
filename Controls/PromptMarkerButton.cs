using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using PiAgentGui.ViewModels.Conversations;
using Windows.UI.ViewManagement;

namespace PiAgentGui.Controls;

public sealed class PromptMarkerButton : ActionButton
{
    private readonly ScaleTransform scale = new();
    private Storyboard? animation;
    private bool hovered;
    private bool focused;

    public PromptMarkerButton(ChatEntryViewModel entry, int number)
    {
        Width = 28;
        Height = 12;
        MinWidth = 0;
        MinHeight = 0;
        Padding = new Thickness(0);
        BorderThickness = new Thickness(0);
        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
        Content = new Border { Width = 7, Height = 2, CornerRadius = new CornerRadius(1),
            Background = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            RenderTransform = scale, RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5) };
        ActualThemeChanged += (_, _) =>
        {
            if (Content is Border line) line.Background = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
        };
        var excerpt = entry.Text.Length > 600 ? entry.Text[..600] + "…" : entry.Text;
        AutomationProperties.SetName(this, $"Jump to prompt {number}: {excerpt}");
        ToolTipService.SetToolTip(this, new ToolTip
        {
            Placement = Microsoft.UI.Xaml.Controls.Primitives.PlacementMode.Right,
            Content = new TextBlock { Text = $"Prompt {number}\n\n{excerpt}", Width = 300, MaxHeight = 220,
                TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, FontSize = 12 }
        });
        PointerEntered += (_, _) => { hovered = true; Animate(); };
        PointerExited += (_, _) => { hovered = false; Animate(); };
        GotFocus += (_, _) => { focused = true; Animate(); };
        LostFocus += (_, _) => { focused = false; Animate(); };
        Unloaded += (_, _) => { animation?.Stop(); scale.ScaleX = 1; };
    }

    private void Animate()
    {
        var from = scale.ScaleX;
        animation?.Stop();
        var target = hovered || focused ? 2 : 1;
        scale.ScaleX = target;
        if (!new UISettings().AnimationsEnabled) return;
        var tween = new DoubleAnimation { From = from, To = target, Duration = new Duration(TimeSpan.FromMilliseconds(160)),
            EnableDependentAnimation = true, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        Storyboard.SetTarget(tween, scale);
        Storyboard.SetTargetProperty(tween, nameof(ScaleTransform.ScaleX));
        animation = new Storyboard();
        animation.Children.Add(tween);
        animation.Begin();
    }
}

