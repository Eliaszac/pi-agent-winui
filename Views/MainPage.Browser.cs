using System.Text.Json;
using System.Text.Json.Nodes;
using PiAgentGui.Services.Conversations;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Conversations;

namespace PiAgentGui.Views;

public sealed partial class MainPage
{
    private void OnBrowserClicked(object sender, RoutedEventArgs args) => OpenSidePanel("browser");
    private readonly Dictionary<SidePanelTab, BrowserSurface> browserSurfaces = [];
    private readonly Dictionary<(Guid Owner, int Port), BrowserPreviewTunnel> browserTunnels = [];
    private readonly Dictionary<Guid, SidePanelTab> selectedBrowsers = [];

    private async Task OpenBrowserAsync(ConversationViewModel chat, Uri uri)
    {
        try
        {
            if (PiBrowserSupport.FindPackage() is null) { ViewModel.OpenExtensions(); return; }
            var (_, surface) = CreateBrowserTab(chat);
            await surface.NavigateAsync(uri);
        }
        catch (Exception error) { chat.ReportAttachmentError("Browser: " + error.Message); }
    }

    private (SidePanelTab Tab, BrowserSurface Surface) CreateBrowserTab(ConversationViewModel chat)
    {
        if (closingSidePanels || !sidePanels.TryGetValue(chat.ResearchOwnerId, out var state)) throw new InvalidOperationException("The conversation has closed.");
        var tab = state.Open("browser", "Browser");
        var surface = new BrowserSurface(chat.ResearchOwnerId, uri => ResolveBrowserAddressAsync(chat, uri));
        browserSurfaces.Add(tab, surface); selectedBrowsers[chat.ResearchOwnerId] = tab;
        surface.TitleChanged += title => tab.Title = title;
        surface.NewTabRequested += uri => _ = OpenBrowserAsync(chat, uri);
        surface.CloseRequested += () => { state.Close(tab); CloseBrowserTab(tab); ApplySidePanel(); };
        BrowserHost.Children.Add(surface);
        ApplySidePanel();
        return (tab, surface);
    }

    private Task<Uri> ResolveBrowserAddressAsync(ConversationViewModel chat, Uri uri)
    {
        uri = BrowserAddress.Parse(uri.AbsoluteUri);
        if (uri.Scheme is "http" or "https" && uri.IsLoopback && chat.Target is { IsLocal: false } target)
        {
            if (browserTunnels.Any(pair => pair.Key.Owner == chat.ResearchOwnerId && pair.Value.Port == uri.Port)) return Task.FromResult(uri);
            var key = (chat.ResearchOwnerId, uri.Port);
            if (!browserTunnels.TryGetValue(key, out var tunnel)) browserTunnels[key] = tunnel = new(target, uri.Port);
            uri = new UriBuilder(uri) { Host = "127.0.0.1", Port = tunnel.Port }.Uri;
        }
        return Task.FromResult(uri);
    }

    private async Task<JsonObject> HandleBrowserRequestAsync(ConversationViewModel chat, JsonElement request)
    {
        if (PiBrowserSupport.FindPackage() is null) throw new InvalidOperationException("Install Pi Browser from Extensions, then reconnect the conversation.");
        if (!sidePanels.TryGetValue(chat.ResearchOwnerId, out var state)) throw new InvalidOperationException("The conversation has closed.");
        var tabs = state.Tabs.Where(tab => tab.Kind == "browser").ToArray();
        var selected = state.Selected?.Kind == "browser" ? state.Selected : selectedBrowsers.GetValueOrDefault(chat.ResearchOwnerId);
        if (selected is not null && !browserSurfaces.ContainsKey(selected)) selected = null;
        var action = PiJson.Text(request, "action");
        if (action == "tabs")
        {
            var operation = PiJson.Text(request, "operation");
            if (operation == "new") { (selected, _) = CreateBrowserTab(chat); await browserSurfaces[selected].NavigateAsync(BrowserAddress.DefaultPage); }
            else if (operation is "close" or "select")
            {
                if (request.TryGetProperty("index", out var index))
                {
                    if (!index.TryGetInt32(out var position) || position < 0 || position >= tabs.Length) throw new ArgumentException("Browser tab index is out of range.");
                    selected = tabs[position];
                }
                if (selected is null) throw new InvalidOperationException("No browser tab is selected.");
                if (operation == "close") { state.Close(selected); CloseBrowserTab(selected); selected = state.Tabs.FirstOrDefault(tab => tab.Kind == "browser"); }
                else { state.Selected = selected; state.IsOpen = true; }
            }
            else if (operation != "list") throw new ArgumentException("Unknown browser tab operation.");
            if (selected is not null) selectedBrowsers[chat.ResearchOwnerId] = selected;
            ApplySidePanel();
            var list = new JsonArray();
            foreach (var tab in state.Tabs.Where(tab => tab.Kind == "browser")) list.Add(new JsonObject { ["index"] = list.Count, ["title"] = tab.Title, ["url"] = browserSurfaces[tab].Address, ["selected"] = tab == selected });
            return new() { ["tabs"] = list };
        }
        if (action == "resolve") return new() { ["url"] = (await ResolveBrowserAddressAsync(chat, BrowserAddress.Parse(PiJson.Text(request, "url")))).AbsoluteUri };
        if (action == "error") { if (selected is not null) browserSurfaces[selected].ShowError(PiJson.Text(request, "message")); return new(); }
        if (action != "ready") throw new ArgumentException("Unknown browser request.");
        if (selected is null) (selected, _) = CreateBrowserTab(chat);
        var surface = browserSurfaces[selected];
        await surface.EnsureAsync();
        return new() { ["port"] = surface.Port };
    }

    private void CloseBrowserTab(SidePanelTab tab)
    {
        if (browserSurfaces.Remove(tab, out var surface)) { BrowserHost.Children.Remove(surface); surface.Dispose(); }
        foreach (var id in selectedBrowsers.Where(pair => pair.Value == tab).Select(pair => pair.Key).ToArray()) selectedBrowsers.Remove(id);
        foreach (var key in browserTunnels.Keys.Where(key => !sidePanels.TryGetValue(key.Owner, out var state) || !state.Tabs.Any(item => item.Kind == "browser")).ToArray())
        {
            browserTunnels[key].Dispose(); browserTunnels.Remove(key);
        }
    }
}
