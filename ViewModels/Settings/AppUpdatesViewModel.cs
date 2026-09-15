using PiAgentGui.Configuration;
using PiAgentGui.Models.Updates;
using PiAgentGui.Services.Settings;
using PiAgentGui.Services.Updates;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Settings;

public sealed class AppUpdatesViewModel(AppUpdateClient client, AppSettingsStore preferences,
    Func<string?> blocked, Func<string, Task> install, CancellationToken lifetime) : ObservableObject
{
    private AppRelease? release;
    private string state = "idle";
    private string error = "";
    private double progress;
    private CancellationTokenSource? operation;
    private string? blockReason;
    public string Version => "Version " + ApplicationIdentity.Version;
    public bool AutomaticChecks => preferences.Current.AutomaticUpdateChecks;
    public bool AutomaticDownloads => preferences.Current.AutomaticUpdateDownloads;
    public bool HasUpdate => release is not null;
    public bool Busy => state is "checking" or "downloading" or "installing";
    public bool ShowProgress => state is "checking" or "downloading";
    public bool Indeterminate => state == "checking";
    public double Progress => progress;
    public bool ShowRestartNote => state == "ready";
    public bool CanAct => state is not ("checking" or "installing") && !(state == "ready" && blockReason is not null);
    public string Title => state switch
    {
        "checking" => "Checking for updates…", "current" => "You're up to date",
        "available" => "A new version is available", "downloading" => "Downloading update…",
        "ready" => blockReason is null ? "Your update is ready" : "Ready when you are",
        "installing" => "Preparing to restart…", "error" => "Couldn't complete the update",
        _ => "Check for a newer version"
    };
    public string Detail => state switch
    {
        "checking" => "Looking for the latest release on GitHub.", "current" => "Pi desktop is running the latest available version.",
        "available" => $"Version {release?.Version} · {release?.Size / 1048576d:0.#} MB",
        "downloading" => $"{progress:0}% · Version {release?.Version}",
        "ready" => blockReason ?? $"Version {release?.Version} is ready to install.",
        "installing" => "Closing Pi desktop safely before installation.", "error" => error,
        _ => "Get the latest improvements from GitHub releases."
    };
    public string ActionLabel => state switch { "available" => "Download update", "downloading" => "Cancel download", "ready" => "Update and restart", "checking" => "Checking…", "installing" => "Restarting…", "error" => "Try again", _ => "Check for updates" };
    public string Icon => state switch { "current" => "\uE73E", "available" or "downloading" => "\uE896", "ready" => "\uE777", "error" => "\uE7BA", _ => "\uE895" };
    private void Refresh()
    {
        foreach (var property in new[] { nameof(HasUpdate), nameof(Busy), nameof(ShowProgress), nameof(Indeterminate), nameof(Progress), nameof(ShowRestartNote), nameof(CanAct), nameof(Title), nameof(Detail), nameof(ActionLabel), nameof(Icon), nameof(AutomaticChecks), nameof(AutomaticDownloads) }) OnPropertyChanged(property);
    }
    public void RefreshAvailability() { if (state != "ready") return; var reason = blocked(); if (reason != blockReason) { blockReason = reason; Refresh(); } }
    public async Task InitializeAsync() { if (AutomaticChecks) await CheckAsync(); }
    public async Task SetPreferencesAsync(bool checks, bool downloads)
    {
        try { await preferences.SaveAsync(preferences.Current with { AutomaticUpdateChecks = checks, AutomaticUpdateDownloads = downloads }); }
        catch (Exception exception) { state = "error"; error = "Could not save update preferences. " + exception.Message; }
        Refresh();
    }
    public async Task ActAsync()
    {
        if (state == "downloading") { operation?.Cancel(); return; }
        if (!CanAct) return;
        if (state == "ready")
        {
            RefreshAvailability();
            if (!CanAct || release is null) return;
            state = "installing"; Refresh();
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
            deadline.CancelAfter(TimeSpan.FromMinutes(2));
            try
            {
                var path = await client.DownloadAsync(release, new Progress<double>(), deadline.Token);
                if (blocked() is { } reason) { state = "ready"; blockReason = reason; }
                else await install(path);
            }
            catch (Exception exception) { state = "error"; error = exception.Message; }
            Refresh();
        }
        else if (state == "available") await DownloadAsync();
        else await CheckAsync();
    }
    public async Task CheckAsync()
    {
        if (Busy) return;
        state = "checking"; Refresh();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
        deadline.CancelAfter(TimeSpan.FromSeconds(25));
        try
        {
            release = await client.CheckAsync(System.Version.Parse(ApplicationIdentity.Version), deadline.Token);
            state = release is null ? "current" : "available";
        }
        catch (OperationCanceledException) { state = "error"; error = "The update check timed out. Check your connection and try again."; }
        catch (Exception exception) { state = "error"; error = exception.Message; }
        Refresh();
        if (state == "available" && AutomaticDownloads) await DownloadAsync();
    }
    private async Task DownloadAsync()
    {
        if (release is null || Busy) return;
        state = "downloading"; progress = 0; Refresh();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
        operation = cancellation;
        cancellation.CancelAfter(TimeSpan.FromMinutes(15));
        try
        {
            await client.DownloadAsync(release, new Progress<double>(value => { progress = value; if (state == "downloading") Refresh(); }), cancellation.Token);
            state = "ready"; blockReason = blocked();
        }
        catch (OperationCanceledException) { state = "available"; }
        catch (Exception exception) { state = "error"; error = exception.Message; }
        finally { operation = null; Refresh(); }
    }
}
