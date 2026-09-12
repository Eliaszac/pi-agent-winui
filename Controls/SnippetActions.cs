using System.ComponentModel;
using PiAgentGui.ViewModels.Conversations;
using Windows.ApplicationModel.DataTransfer;

namespace PiAgentGui.Controls;

/// <summary>Explicit snippet actions and bounded, selectable inline execution results.</summary>
internal sealed class SnippetActions : UserControl
{
    internal StackPanel HeaderActions { get; }

    internal SnippetActions(SnippetViewModel model)
    {
        var run = new SnippetIconButton("\uE768", $"Run · {model.Target}");
        run.Visibility = model.SupportsRun ? Visibility.Visible : Visibility.Collapsed;
        var save = new SnippetIconButton("\uE74E", $"Save to project · {model.Target}");
        var stop = new SnippetIconButton("\uE71A", "Stop snippet");
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        HeaderActions = actions;
        actions.Children.Add(run); actions.Children.Add(stop); actions.Children.Add(save);
        var panel = new StackPanel { Spacing = 8, Margin = new Thickness(12, 0, 12, 12) };
        var status = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true,
            VerticalAlignment = VerticalAlignment.Center };
        ToolTipService.SetToolTip(status, model.Target);
        var resultHeader = new Grid { ColumnSpacing = 8 };
        resultHeader.ColumnDefinitions.Add(new ColumnDefinition());
        resultHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        resultHeader.Children.Add(status);
        panel.Children.Add(resultHeader);
        var output = new TextBlock { FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"), FontSize = 12,
            TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
        var scroller = new ScrollViewer { Content = output, MaxHeight = 220, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        panel.Children.Add(scroller);
        var more = new SnippetIconButton("\uE740", "Expand output");
        var copy = new CopyFeedbackButton { Style = (Style)Application.Current.Resources["ShellIconButtonStyle"] };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(copy, "Copy snippet output");
        ToolTipService.SetToolTip(copy, "Copy snippet output");
        var add = new SnippetIconButton("\uE710", "Add output to prompt");
        var dismiss = new SnippetIconButton("\uE711", "Dismiss output");
        var results = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        results.Children.Add(more); results.Children.Add(copy); results.Children.Add(add); results.Children.Add(dismiss);
        Grid.SetColumn(results, 1);
        resultHeader.Children.Add(results);
        var expanded = false;
        more.Click += (_, _) => { expanded = !expanded; scroller.MaxHeight = expanded ? 600 : 220;
            more.SetAction(expanded ? "\uE73F" : "\uE740", expanded ? "Collapse output" : "Expand output"); };
        dismiss.Click += (_, _) => model.DismissOutput();
        run.Click += async (_, _) => await model.RunAsync();
        save.Click += async (_, _) => await model.SaveAsync();
        stop.Click += (_, _) => model.Stop();
        add.Click += (_, _) => model.AddOutputToDraft();
        copy.Click += (_, _) =>
        {
            try { var data = new DataPackage(); data.SetText(model.Output); Clipboard.SetContent(data); copy.ShowCopied(); }
            catch (System.Runtime.InteropServices.COMException) { ToolTipService.SetToolTip(copy, "Couldn't copy output."); }
        };
        void Refresh()
        {
            run.IsEnabled = model.CanRun; save.IsEnabled = model.CanSave;
            stop.Visibility = model.IsRunning ? Visibility.Visible : Visibility.Collapsed;
            status.Text = model.Status; status.Visibility = model.Status.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            output.Text = model.Output;
            scroller.Visibility = more.Visibility = copy.Visibility = add.Visibility = model.HasOutput ? Visibility.Visible : Visibility.Collapsed;
            panel.Visibility = model.HasOutput || model.Status.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            dismiss.IsEnabled = model.CanDismiss;
            if (!model.HasOutput)
            {
                expanded = false; scroller.MaxHeight = 220;
                more.SetAction("\uE740", "Expand output");
            }
            add.IsEnabled = !model.IsRunning;
        }
        void Changed(object? sender, PropertyChangedEventArgs args) => Refresh();
        Loaded += (_, _) => { model.PropertyChanged += Changed; Refresh(); };
        Unloaded += (_, _) => model.PropertyChanged -= Changed;
        Content = panel;
        Refresh();
    }
}
