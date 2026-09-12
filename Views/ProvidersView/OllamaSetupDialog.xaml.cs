using PiAgentGui.Models.Pi;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Providers;

namespace PiAgentGui.Views;

public sealed partial class OllamaSetupDialog : Controls.ActionContentDialog
{
    private readonly OllamaSetupViewModel viewModel;
    private readonly CancellationTokenSource lifetime = new();
    public OllamaSetupDialog(OllamaSetupViewModel viewModel)
    {
        this.viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
        Closed += (_, _) => { lifetime.Cancel(); lifetime.Dispose(); };
    }
    private void OnLocal(object sender, RoutedEventArgs args) { SavedServers.SelectedItem = null; viewModel.Address = OllamaEndpoint.Local; }
    private void OnSavedServer(object sender, SelectionChangedEventArgs args)
    {
        if (SavedServers.SelectedItem is Uri server) viewModel.Address = server.AbsoluteUri.TrimEnd('/');
    }
    private async void OnImport(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        args.Cancel = true;
        var deferral = args.GetDeferral();
        try { await viewModel.ConnectAsync(lifetime.Token); }
        finally { deferral.Complete(); }
    }
}
