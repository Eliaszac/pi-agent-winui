using Microsoft.UI.Xaml.Input;
using PiAgentGui.Utilities;
using Windows.System;

namespace PiAgentGui.Views;

public sealed partial class MainPage
{
    private readonly DoubleShiftGesture paletteGesture = new();
    private CommandPaletteDialog? commandPalette;
    private readonly Services.Conversations.PaletteUsageStore paletteUsage = new(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PiAgentGui", "command-usage.json"));
    private bool loadingPalette;
    private void InitializeCommandPalette()
    {
        AddHandler(PreviewKeyDownEvent, new KeyEventHandler(OnPaletteKeyDown), true);
        AddHandler(PreviewKeyUpEvent, new KeyEventHandler(OnPaletteKeyUp), true);
        Unloaded += (_, _) => { paletteGesture.Reset(); commandPalette?.Hide(); };
        TerminalPane.PaletteKey += async (down, shift, modified) =>
        {
            if (down) paletteGesture.Down(shift, modified, Environment.TickCount64);
            else if (paletteGesture.Up(shift, Environment.TickCount64)) await ToggleCommandPaletteAsync();
        };
    }
    private void OnPaletteKeyDown(object sender, KeyRoutedEventArgs args)
    {
        var modified = (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0
            || (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
        paletteGesture.Down(args.Key is VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift, modified, Environment.TickCount64);
    }
    private async void OnPaletteKeyUp(object sender, KeyRoutedEventArgs args)
    {
        if (!paletteGesture.Up(args.Key is VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift, Environment.TickCount64)) return;
        args.Handled = true;
        await ToggleCommandPaletteAsync();
    }
    private async Task ToggleCommandPaletteAsync()
    {
        if (commandPalette is { } current) { current.Hide(); return; }
        if (loadingPalette) return;
        loadingPalette = true;
        try { await paletteUsage.LoadAsync(); }
        finally { loadingPalette = false; }
        var palette = new CommandPaletteDialog(ViewModel.Providers?.HasConfiguredProvider == true, BuildPaletteCommands(), paletteUsage) { XamlRoot = XamlRoot };
        commandPalette = palette;
        palette.AddHandler(PreviewKeyDownEvent, new KeyEventHandler(OnPaletteKeyDown), true);
        palette.AddHandler(PreviewKeyUpEvent, new KeyEventHandler(OnPaletteKeyUp), true);
        try
        {
            await palette.ShowAsync();
            if (palette.OpenProviders) ViewModel.OpenProviders();
            if (palette.SelectedCommand is { CanUse: true, Execute: { } execute } command && ViewModel.Providers?.HasConfiguredProvider == true)
            {
                await execute();
                try { foreach (var id in palette.UsageIds) await paletteUsage.RecordAsync(id); }
                catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException) { ViewModel.ReportError(new InvalidOperationException("Command completed, but usage history could not be saved.", error)); }
            }
        }
        catch (System.Runtime.InteropServices.COMException) { /* Another native dialog owns modal input. */ }
        catch (Exception error) { ViewModel.ReportError(error); }
        finally { commandPalette = null; paletteGesture.Reset(); }
    }
}
