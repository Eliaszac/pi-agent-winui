using PiAgentGui.Models.Projects;
using PiAgentGui.ViewModels.Projects;

namespace PiAgentGui.Views;

public sealed partial class MainPage
{
    private async void OnTargetDetailsClicked(object sender, RoutedEventArgs args)
    {
        if (dialogOpen || sender is not MenuFlyoutItem { Tag: ConversationItemViewModel conversation }) return;
        dialogOpen = true;
        try
        {
            await new Controls.ActionContentDialog { XamlRoot = XamlRoot, Title = "Execution target", CloseButtonText = "Close",
                Content = new TextBlock { Text = conversation.HoverDetails, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true, MaxWidth = 440 } }.ShowAsync();
        }
        catch (Exception exception) { ViewModel.ReportError(exception); }
        finally { dialogOpen = false; }
    }
    private async Task OpenTargetFileAsync(string path)
    {
        if (ViewModel.SelectedTarget is not { IsLocal: false } target) { await OpenIn.OpenFileAsync(path); return; }
        if (dialogOpen) return;
        dialogOpen = true;
        try
        {
            var text = await new Services.Files.TargetFileReader(target, new Services.Projects.TargetCommandRunner()).ReadAsync(path, githubCancellation);
            var content = new TextBox { Text = text, IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap, MinWidth = 400, MaxHeight = 600, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Mono") };
            await new Controls.ActionContentDialog { XamlRoot = XamlRoot, Title = target.Label + " · " + System.IO.Path.GetFileName(path), Content = content, CloseButtonText = "Close" }.ShowAsync();
        }
        finally { dialogOpen = false; }
    }
    private async Task<Guid?> ChooseConversationTargetAsync(ProjectItemViewModel project)
    {
        if (dialogOpen) return null;
        dialogOpen = true;
        try
        {
            var choice = new ComboBox { ItemsSource = project.Targets, DisplayMemberPath = "Label", SelectedItem = project.DefaultTarget, HorizontalAlignment = HorizontalAlignment.Stretch, MinWidth = 300 };
            var details = new TextBlock { Text = project.DefaultTarget.Description, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
            choice.SelectionChanged += (_, _) => details.Text = (choice.SelectedItem as ExecutionTarget)?.Description ?? "";
            var content = new StackPanel { Spacing = 12 }; content.Children.Add(choice); content.Children.Add(details);
            var dialog = new Controls.ActionContentDialog { XamlRoot = XamlRoot, Title = "New conversation", Content = content,
                PrimaryButtonText = "Create conversation", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Primary };
            return await dialog.ShowAsync() == ContentDialogResult.Primary ? (choice.SelectedItem as ExecutionTarget)?.Id : null;
        }
        finally { dialogOpen = false; }
    }

    private async void OnManageTargetsClicked(object sender, RoutedEventArgs args)
    {
        if (dialogOpen || sender is not MenuFlyoutItem { Tag: ProjectItemViewModel project }) return;
        dialogOpen = true;
        try
        {
            await new ExecutionTargetsDialog(project.Project, projectRepository, picker, wslDistributions) { XamlRoot = XamlRoot }.ShowAsync();
            await ViewModel.RefreshTargetsAsync(project);
        }
        catch (Exception exception) { ViewModel.ReportError(exception); }
        finally { dialogOpen = false; }
    }
}
