namespace PiAgentGui.Views;

public sealed partial class ConversationView
{
    private void OnComposerToolbarSizeChanged(object sender, SizeChangedEventArgs args)
    {
        var compact = args.NewSize.Width < 594;
        ModelSelector.Width = Math.Clamp(args.NewSize.Width - (compact ? 200 : 370), 80, 220);
        Grid.SetRow(ComposerModelActions, compact ? 1 : 0);
        Grid.SetColumn(ComposerModelActions, compact ? 0 : 1);
        Grid.SetColumnSpan(ComposerModelActions, compact ? 2 : 1);
    }
}
