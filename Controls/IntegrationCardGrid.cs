using Windows.Foundation;

namespace PiAgentGui.Controls;

/// <summary>Uses equal card sizes based on the tallest card at the available width.</summary>
public sealed class IntegrationCardGrid : Panel
{
    private const double Gap = 16;
    private double cardHeight;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 900;
        var columns = width >= 720 ? 2 : 1;
        var cardWidth = Math.Max(0, (width - Gap * (columns - 1)) / columns);
        cardHeight = 0;
        foreach (var child in Children)
        {
            child.Measure(new Size(cardWidth, double.PositiveInfinity));
            cardHeight = Math.Max(cardHeight, child.DesiredSize.Height);
        }
        var rows = (Children.Count + columns - 1) / columns;
        return new Size(width, rows * cardHeight + Math.Max(0, rows - 1) * Gap);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = finalSize.Width >= 720 ? 2 : 1;
        var cardWidth = Math.Max(0, (finalSize.Width - Gap * (columns - 1)) / columns);
        for (var index = 0; index < Children.Count; index++)
            Children[index].Arrange(new Rect(index % columns * (cardWidth + Gap), index / columns * (cardHeight + Gap), cardWidth, cardHeight));
        return finalSize;
    }
}
