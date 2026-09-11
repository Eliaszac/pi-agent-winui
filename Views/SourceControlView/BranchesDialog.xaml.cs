using System.Collections.Specialized;
using PiAgentGui.Models.SourceControl;
using PiAgentGui.ViewModels.SourceControl;

namespace PiAgentGui.Views;

public sealed partial class BranchesDialog : Controls.ActionContentDialog
{
    private readonly SourceControlViewModel model;
    private GitBranch? source;
    public BranchesDialog(SourceControlViewModel model)
    {
        this.model = model; InitializeComponent(); DataContext = model;
        model.Branches.CollectionChanged += OnBranchesChanged;
        Closed += (_, _) => model.Branches.CollectionChanged -= OnBranchesChanged;
        ApplyFilter();
    }
    private void OnBranchesChanged(object? sender, NotifyCollectionChangedEventArgs args) => ApplyFilter();
    private void OnFilterChanged(object sender, TextChangedEventArgs args) { if (BranchList is not null) ApplyFilter(); }
    private void ApplyFilter() => BranchList.ItemsSource = model.Branches.Where(branch => branch.Name.Contains(Filter.Text, StringComparison.OrdinalIgnoreCase)).ToArray();
    private async void OnCheckout(object sender, RoutedEventArgs args)
    { if (sender is FrameworkElement { Tag: GitBranch branch }) await model.CheckoutAsync(branch); }
    private async void OnFetchBranch(object sender, RoutedEventArgs args)
    { if (sender is FrameworkElement { Tag: GitBranch branch }) await model.FetchAsync(branch); }
    private async void OnMerge(object sender, RoutedEventArgs args)
    { if (sender is FrameworkElement { Tag: GitBranch branch }) await model.MergeAsync(branch); }
    private void OnCreateFrom(object sender, RoutedEventArgs args)
    {
        if (sender is not FrameworkElement { Tag: GitBranch branch }) return;
        source = branch; CreateLabel.Text = $"Create a new branch from {branch.Name}";
        CreateSection.Visibility = Visibility.Visible; NewBranchName.Focus(FocusState.Programmatic);
    }
    private async void OnCreate(object sender, RoutedEventArgs args)
    {
        if (source is null) return;
        var current = model.Branches.FirstOrDefault(branch => branch.Ref == source.Ref);
        if (current is null) { model.ReportError("The source branch changed. Choose it again."); return; }
        await model.CheckoutAsync(current, NewBranchName.Text.Trim());
        if (!model.HasError) { CreateSection.Visibility = Visibility.Collapsed; NewBranchName.Text = ""; }
    }
}
