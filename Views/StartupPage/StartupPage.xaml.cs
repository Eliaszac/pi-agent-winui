using PiAgentGui.ViewModels.Startup;

namespace PiAgentGui.Views;

public sealed partial class StartupPage : Page
{
    public StartupViewModel ViewModel { get; }
    public StartupPage(StartupViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }
}
