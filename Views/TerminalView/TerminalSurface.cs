using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using PiAgentGui.Services.Terminal;
using Windows.ApplicationModel.DataTransfer;

namespace PiAgentGui.Views;

/// <summary>Hosts the bundled terminal renderer and translates UI events to the injected shell session.</summary>
public sealed class TerminalSurface : UserControl, IDisposable
{
    private const string Origin = "https://terminal.piagent.local/";
    private readonly ITerminalSession session;
    private readonly WebView2 browser = new();
    private readonly TextBlock feedback = new() { Text = "Starting terminal…", Margin = new Thickness(12), TextWrapping = TextWrapping.Wrap };
    private readonly CancellationTokenSource lifetime = new();
    private TaskCompletionSource? acknowledgement;
    private bool loaded;
    private bool started;
    private bool rendered;
    private bool disposed;

    public TerminalSurface(ITerminalSession session)
    {
        this.session = session;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(browser, 1);
        grid.Children.Add(browser);
        grid.Children.Add(feedback);
        Content = grid;
        Loaded += OnLoaded;
        ActualThemeChanged += (_, _) => Send(new { type = "theme", dark = ActualTheme == ElementTheme.Dark });
        session.Output += OnOutput;
        session.Exited += OnExited;
    }

    private async void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (loaded || disposed) return;
        loaded = true;
        _ = WatchStartupAsync();
        var stage = "creating the browser environment";
        try
        {
            var profile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PiAgentGui", "WebView2", "Terminal");
            Directory.CreateDirectory(profile);
            var environment = await CoreWebView2Environment.CreateWithOptionsAsync("", profile, new CoreWebView2EnvironmentOptions());
            if (disposed) return;
            stage = "initializing the terminal display";
            feedback.Text = "Initializing terminal display…";
            await browser.EnsureCoreWebView2Async(environment);
            if (disposed) return;
            stage = "loading the terminal assets";
            feedback.Text = "Loading terminal page…";
            var core = browser.CoreWebView2;
            core.SetVirtualHostNameToFolderMapping("terminal.piagent.local", Path.Combine(AppContext.BaseDirectory, "Assets", "Terminal"), CoreWebView2HostResourceAccessKind.DenyCors);
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreBrowserAcceleratorKeysEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.NavigationStarting += (_, change) => change.Cancel = change.Uri != Origin + "index.html";
            core.NewWindowRequested += (_, change) => change.Handled = true;
            core.PermissionRequested += (_, change) => change.State = CoreWebView2PermissionState.Deny;
            core.WebMessageReceived += OnMessage;
            core.ProcessFailed += (_, _) => ShowError("Terminal display stopped. Close this tab and open another.");
            core.NavigationCompleted += (_, navigation) =>
            {
                if (!navigation.IsSuccess) ShowError($"Couldn't load the terminal page ({navigation.WebErrorStatus}).");
            };
            core.Navigate(Origin + "index.html");
        }
        catch (Exception exception) { ShowError($"Terminal failed while {stage} (0x{exception.HResult:X8}, {exception.GetType().Name})."); }
    }

    private async void OnMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        if (disposed || args.Source != Origin + "index.html") return;
        try
        {
            using var json = JsonDocument.Parse(args.WebMessageAsJson);
            var root = json.RootElement;
            switch (root.GetProperty("type").GetString())
            {
                case "ready" when !started:
                    started = true;
                    feedback.Text = "Starting shell…";
                    Send(new { type = "theme", dark = ActualTheme == ElementTheme.Dark });
                    await session.StartAsync(root.GetProperty("columns").GetInt32(), root.GetProperty("rows").GetInt32());
                    if (!rendered) feedback.Text = "Waiting for shell output…";
                    FocusTerminal();
                    break;
                case "input": await session.WriteAsync(root.GetProperty("data").GetString() ?? ""); break;
                case "resize" when started: await session.ResizeAsync(root.GetProperty("columns").GetInt32(), root.GetProperty("rows").GetInt32()); break;
                case "ack":
                    rendered = true;
                    feedback.Visibility = Visibility.Collapsed;
                    acknowledgement?.TrySetResult();
                    break;
                case "renderer-error":
                    ShowError("Terminal page failed to initialize or render. Close this tab and try again.");
                    break;
                case "copy":
                    var data = new DataPackage();
                    data.SetText(root.GetProperty("data").GetString() ?? "");
                    Clipboard.SetContent(data);
                    break;
                case "paste":
                    var clipboard = Clipboard.GetContent();
                    if (clipboard.Contains(StandardDataFormats.Text))
                    {
                        var text = await clipboard.GetTextAsync();
                        if (text.Length <= 65536) Send(new { type = "paste", data = text });
                        else { feedback.Text = "Paste is limited to 65,536 characters at a time."; feedback.Visibility = Visibility.Visible; }
                    }
                    break;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception) { ShowError("Couldn't communicate with this terminal. Close the tab and try again."); }
    }

    private void OnOutput(string text)
    {
        if (disposed || lifetime.IsCancellationRequested) return;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!DispatcherQueue.TryEnqueue(() =>
        {
            acknowledgement = completion;
            if (!rendered) feedback.Text = "Rendering shell output…";
            if (disposed || !Send(new { type = "output", data = text })) completion.TrySetResult();
        })) return;
        // This callback runs on the dedicated output reader; never block the UI thread.
        try { completion.Task.WaitAsync(lifetime.Token).GetAwaiter().GetResult(); }
        catch (OperationCanceledException) { }
    }

    private void OnExited() => DispatcherQueue.TryEnqueue(() =>
    {
        rendered = true;
        feedback.Visibility = Visibility.Collapsed;
        Send(new { type = "exit" });
    });
    private async Task WatchStartupAsync()
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), lifetime.Token);
            if (!disposed && !rendered)
                feedback.Text += " This is taking longer than expected.";
        }
        catch (OperationCanceledException) { }
    }
    public void FocusTerminal()
    {
        // Focusing before explicit initialization can start WebView2 with its default environment.
        if (!started || disposed) return;
        Send(new { type = "focus" });
        browser.Focus(FocusState.Programmatic);
    }
    private bool Send(object message)
    {
        if (disposed || browser.CoreWebView2 is null) return false;
        try { browser.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(message)); return true; }
        catch (Exception) { return false; }
    }
    private void ShowError(string text)
    {
        if (disposed) return;
        feedback.Text = text;
        feedback.Visibility = Visibility.Visible;
        lifetime.Cancel();
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        lifetime.Cancel();
        session.Output -= OnOutput;
        session.Exited -= OnExited;
        browser.Close();
    }
}
