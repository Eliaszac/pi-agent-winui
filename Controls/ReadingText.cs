namespace PiAgentGui.Controls;

public static class ReadingText
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(ReadingText),
        new PropertyMetadata(false, OnEnabledChanged));
    public static bool GetEnabled(DependencyObject target) => (bool)target.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject target, bool value) => target.SetValue(EnabledProperty, value);
    private static void OnEnabledChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not TextBlock text) return;
        if ((bool)args.NewValue) { text.Loaded += OnLoaded; text.Unloaded += OnUnloaded; if (text.IsLoaded) OnLoaded(text, new RoutedEventArgs()); }
        else { text.Loaded -= OnLoaded; text.Unloaded -= OnUnloaded; OnUnloaded(text, new RoutedEventArgs()); }
    }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<TextBlock, EventHandler> Handlers = new();
    private static void OnLoaded(object sender, RoutedEventArgs args)
    {
        var text = (TextBlock)sender;
        text.FontSize = ReadingPreferences.Body;
        if (Handlers.TryGetValue(text, out _)) return;
        EventHandler handler = (_, _) => text.FontSize = ReadingPreferences.Body;
        Handlers.Add(text, handler);
        ReadingPreferences.TypographyChanged += handler;
    }
    private static void OnUnloaded(object sender, RoutedEventArgs args)
    {
        var text = (TextBlock)sender;
        if (Handlers.TryGetValue(text, out var handler)) ReadingPreferences.TypographyChanged -= handler;
        Handlers.Remove(text);
    }
}
