namespace PiAgentGui.Controls;

/// <summary>Applies the response weight to labels without changing their size.</summary>
public static class ReadingWeight
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(ReadingWeight),
        new PropertyMetadata(false, OnEnabledChanged));
    public static bool GetEnabled(DependencyObject target) => (bool)target.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject target, bool value) => target.SetValue(EnabledProperty, value);
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<TextBlock, EventHandler> Handlers = new();

    private static void OnEnabledChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not TextBlock text) return;
        if ((bool)args.NewValue)
        {
            text.Loaded += OnLoaded;
            text.Unloaded += OnUnloaded;
            if (text.IsLoaded) OnLoaded(text, new RoutedEventArgs());
        }
        else
        {
            text.Loaded -= OnLoaded;
            text.Unloaded -= OnUnloaded;
            OnUnloaded(text, new RoutedEventArgs());
            text.ClearValue(TextBlock.FontWeightProperty);
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs args)
    {
        var text = (TextBlock)sender;
        text.FontWeight = ReadingPreferences.BodyWeight;
        if (Handlers.TryGetValue(text, out _)) return;
        EventHandler handler = (_, _) => text.FontWeight = ReadingPreferences.BodyWeight;
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
