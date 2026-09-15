using Microsoft.Web.WebView2.Core;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using System.Net;
using System.Net.Sockets;
using PiAgentGui.Utilities;

namespace PiAgentGui.Views;

/// <summary>One native browser tab with an isolated environment and debugging endpoint.</summary>
public sealed class BrowserSurface : UserControl, IDisposable
{
    private readonly WebView2 browser = new();
    private readonly TextBox address = new() { PlaceholderText = "Search Google or enter an address", MinWidth = 0 };
    private readonly TextBlock feedback = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8), Visibility = Visibility.Collapsed };
    private readonly Button back;
    private readonly Button forward;
    private readonly Func<Uri, Task<Uri>> resolve;
    private readonly string profile;
    private Task? initialization;
    private bool disposed;
    public int Port { get; private set; }
    public string Address => browser.CoreWebView2?.Source ?? "about:blank";
    public event Action<string>? TitleChanged;
    public event Action<Uri>? NewTabRequested;
    public event Action? CloseRequested;

    public BrowserSurface(Guid owner, Func<Uri, Task<Uri>> resolve)
    {
        this.resolve = resolve;
        profile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PiAgentGui", "WebView2", "Browser", owner.ToString("N"), Guid.NewGuid().ToString("N"));
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        var grid = new Grid();
        grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        var toolbar = new Grid { Padding = new Thickness(6), ColumnSpacing = 4 };
        for (var i = 0; i < 5; i++) toolbar.ColumnDefinitions.Add(new() { Width = i == 3 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });
        back = Action("\uE72B", "Back", () => { if (browser.CoreWebView2?.CanGoBack == true) browser.CoreWebView2.GoBack(); });
        forward = Action("\uE72A", "Forward", () => { if (browser.CoreWebView2?.CanGoForward == true) browser.CoreWebView2.GoForward(); });
        var reload = Action("\uE72C", "Reload / retry", () => _ = RetryAsync());
        var external = Action("\uE8A7", "Open in external browser", () => _ = OpenExternalAsync());
        FrameworkElement[] items = [back, forward, reload, address, external];
        for (var i = 0; i < items.Length; i++) { Grid.SetColumn(items[i], i); toolbar.Children.Add(items[i]); }
        AutomationProperties.SetName(address, "Browser address");
        address.KeyDown += OnAddressKeyDown;
        Grid.SetRow(feedback, 1); Grid.SetRow(browser, 2);
        grid.Children.Add(toolbar); grid.Children.Add(feedback); grid.Children.Add(browser);
        Content = grid;
    }

    private static Button Action(string glyph, string title, Action action)
    {
        var button = new Button { Content = new FontIcon { Glyph = glyph, FontSize = 13 }, Padding = new Thickness(8), MinWidth = 30 };
        AutomationProperties.SetName(button, title); ToolTipService.SetToolTip(button, title);
        button.Click += (_, _) => action();
        return button;
    }

    public Task EnsureAsync() => initialization ??= InitializeAsync();
    private async Task InitializeAsync()
    {
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start(); Port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
            var environment = await CoreWebView2Environment.CreateWithOptionsAsync("", profile,
                new CoreWebView2EnvironmentOptions { AdditionalBrowserArguments = $"--remote-debugging-port={Port} --remote-debugging-address=127.0.0.1" });
            ObjectDisposedException.ThrowIf(disposed, this);
            await browser.EnsureCoreWebView2Async(environment);
            ObjectDisposedException.ThrowIf(disposed, this);
            var core = browser.CoreWebView2;
            core.Settings.AreHostObjectsAllowed = false;
            core.Settings.IsWebMessageEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.NavigationStarting += (sender, args) =>
            {
                feedback.Visibility = Visibility.Collapsed;
                try { _ = BrowserAddress.Parse(args.Uri); }
                catch (ArgumentException) { args.Cancel = true; ShowError("Only HTTP and HTTPS pages can open here."); }
            };
            core.SourceChanged += (_, _) => { address.Text = core.Source; back.IsEnabled = core.CanGoBack; forward.IsEnabled = core.CanGoForward; };
            core.DocumentTitleChanged += (_, _) => TitleChanged?.Invoke(string.IsNullOrWhiteSpace(core.DocumentTitle) ? "Browser" : core.DocumentTitle);
            core.NavigationCompleted += (_, args) => { if (!args.IsSuccess) ShowError($"Page could not load ({args.WebErrorStatus}). Use Reload to retry."); };
            core.NewWindowRequested += (_, args) => { args.Handled = true; try { NewTabRequested?.Invoke(BrowserAddress.Parse(args.Uri)); } catch (ArgumentException error) { ShowError(error.Message); } };
            core.WindowCloseRequested += (_, _) => CloseRequested?.Invoke();
            core.ProcessFailed += (_, _) => ShowError("Browser process stopped. Close this tab and open a new one.");
            core.Navigate("about:blank");
            environment.BrowserProcessExited += (sender, args) => { _ = Task.Run(() => BrowserProfileCleanup.Delete(profile)); };
        }
        catch (Exception error) { ShowError("Browser startup failed: " + error.Message); throw; }
    }

    public async Task NavigateAsync(Uri uri)
    {
        await EnsureAsync();
        var destination = await resolve(uri);
        ObjectDisposedException.ThrowIf(disposed, this);
        feedback.Visibility = Visibility.Collapsed;
        browser.CoreWebView2.Navigate(destination.AbsoluteUri);
    }

    private async void OnAddressKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key != Windows.System.VirtualKey.Enter) return;
        args.Handled = true;
        try { await NavigateAsync(BrowserAddress.FromInput(address.Text)); }
        catch (Exception error) { ShowError(error.Message); }
    }
    private async Task RetryAsync()
    {
        try
        {
            if (initialization?.IsFaulted == true) initialization = null;
            await EnsureAsync(); feedback.Visibility = Visibility.Collapsed; browser.CoreWebView2.Reload();
        }
        catch (Exception error) { ShowError(error.Message); }
    }
    private async Task OpenExternalAsync()
    {
        try { if (Address != "about:blank") await Windows.System.Launcher.LaunchUriAsync(new Uri(Address)); }
        catch (Exception error) { ShowError(error.Message); }
    }
    public void ShowError(string message) { if (!disposed) { feedback.Text = message; feedback.Visibility = Visibility.Visible; } }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; browser.Close();
    }

}
