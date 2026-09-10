using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Conversations;
using Microsoft.UI.Xaml.Automation;

namespace PiAgentGui.Controls;

/// <summary>A bounded prompt index alongside the independently scrolling transcript.</summary>
public sealed class PromptNavigationRail : UserControl
{
    private PromptNavigationPages pages = new();
    private ChatEntryViewModel[] prompts = [];
    private readonly StackPanel markers = new();
    private readonly Grid panel = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly ActionButton previous = new() { Content = new FontIcon { Glyph = "\uE70E", FontSize = 10 } };
    private readonly ActionButton next = new() { Content = new FontIcon { Glyph = "\uE70D", FontSize = 10 } };
    public event Action<ChatEntryViewModel>? PromptSelected;

    public PromptNavigationRail()
    {
        Width = 32;
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new RowDefinition());
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        foreach (var button in new[] { previous, next })
        {
            button.Width = 28; button.Height = 24; button.MinWidth = 0; button.MinHeight = 0; button.Padding = new Thickness(0);
        }
        AutomationProperties.SetName(previous, "Previous 50 prompts");
        AutomationProperties.SetName(next, "Next 50 prompts");
        var scroll = new ScrollViewer { Content = markers, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            HorizontalScrollMode = ScrollMode.Disabled, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 1); Grid.SetRow(next, 2);
        panel.Children.Add(previous); panel.Children.Add(scroll); panel.Children.Add(next);
        Content = panel;
        SizeChanged += (_, _) => panel.MaxHeight = Math.Max(0, ActualHeight);
        previous.Click += (_, _) => { pages.Previous(); Render(); scroll.ChangeView(null, 0, null, true); };
        next.Click += (_, _) => { pages.Next(); Render(); scroll.ChangeView(null, 0, null, true); };
        Visibility = Visibility.Collapsed;
    }

    public void Reset() { prompts = []; pages = new(); Render(); }

    public void Update(IEnumerable<ChatEntryViewModel> entries)
    {
        var updated = entries.Where(entry => entry.IsUser).ToArray();
        if (prompts.SequenceEqual(updated)) return;
        prompts = updated;
        pages.Update(prompts.Length);
        Render();
    }

    private void Render()
    {
        Visibility = prompts.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        panel.Height = pages.VisibleCount * 12 + (pages.PageCount > 1 ? 48 : 0);
        previous.Visibility = next.Visibility = pages.PageCount > 1 ? Visibility.Visible : Visibility.Collapsed;
        previous.IsEnabled = pages.HasPrevious;
        next.IsEnabled = pages.HasNext;
        var range = $"Prompts {pages.Start + 1}–{pages.Start + pages.VisibleCount} of {pages.Count}";
        ToolTipService.SetToolTip(previous, "Older prompts · " + range);
        ToolTipService.SetToolTip(next, "Newer prompts · " + range);
        markers.Children.Clear();
        foreach (var index in Enumerable.Range(pages.Start, pages.VisibleCount))
        {
            var entry = prompts[index];
            var button = new PromptMarkerButton(entry, index + 1);
            button.Click += (_, _) => PromptSelected?.Invoke(entry);
            markers.Children.Add(button);
        }
    }
}

