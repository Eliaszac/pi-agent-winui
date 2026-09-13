using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Microsoft.UI.Xaml.Media;
using Windows.UI.Text;
using Microsoft.UI.Text;
using PiAgentGui.Utilities;

namespace PiAgentGui.Controls;

/// <summary>A selectable native table with a ten-data-row viewport and scrolling on both axes.</summary>
internal sealed class MarkdownTableView : UserControl
{
    internal MarkdownTableView(Table table)
    {
        var data = new MarkdownTableData(table);
        var headings = new List<(ActionButton Button, TextBlock Arrow, int Column)>();
        var originalRows = new Dictionary<FrameworkElement, int>();
        Action<int>? sort = null;
        var grid = new Grid { FlowDirection = FlowDirection.LeftToRight };
        var header = new Grid { FlowDirection = FlowDirection.LeftToRight };
        var columns = table.ColumnDefinitions.Count;
        foreach (TableRow row in table) columns = Math.Max(columns, row.Count);
        for (var column = 0; column < columns; column++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        }
        for (var rowIndex = 0; rowIndex < table.Count; rowIndex++)
        {
            var row = (TableRow)table[rowIndex];
            var target = row.IsHeader ? header : grid;
            var targetRow = target.RowDefinitions.Count;
            target.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (var column = 0; column < row.Count; column++)
            {
                var cell = (TableCell)row[column];
                var content = new StackPanel { Spacing = 8, MinWidth = 120, MaxWidth = 320 };
                foreach (var block in cell)
                {
                    var element = MarkdownRenderer.RenderBlock(block);
                    if (element is RichTextBlock text)
                    {
                        text.FontSize = 14 * ReadingPreferences.Scale; text.LineHeight = 22 * ReadingPreferences.Scale;
                        if (row.IsHeader) text.FontWeight = FontWeights.SemiBold;
                        if (column < table.ColumnDefinitions.Count)
                            text.TextAlignment = table.ColumnDefinitions[column].Alignment switch
                            {
                                TableColumnAlign.Center => TextAlignment.Center,
                                TableColumnAlign.Right => TextAlignment.Right,
                                _ => TextAlignment.Left
                            };
                    }
                    content.Children.Add(element);
                }
                var border = new Border
                {
                    Child = content, Padding = new Thickness(14, 10, 14, 10),
                    BorderThickness = new Thickness(0, 0, 0, rowIndex < table.Count - 1 ? 1 : 0),
                    BorderBrush = (Brush)Application.Current.Resources["ControlStrokeColorDefaultBrush"]
                };
                if (row.IsHeader)
                {
                    border.Background = (Brush)Application.Current.Resources["ControlFillColorSecondaryBrush"];
                    var caption = new TextBlock { Text = string.IsNullOrWhiteSpace(data.Headers[column]) ? $"Column {column + 1}" : data.Headers[column], FontSize = 14 * ReadingPreferences.Scale, FontWeight = FontWeights.SemiBold,
                        TextWrapping = TextWrapping.Wrap, MaxWidth = 300 };
                    var arrow = new TextBlock { Text = "", Width = 14, VerticalAlignment = VerticalAlignment.Center };
                    var label = new Grid { ColumnSpacing = 6, MinWidth = 120, MaxWidth = 320 };
                    label.ColumnDefinitions.Add(new ColumnDefinition());
                    label.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    label.Children.Add(caption); Grid.SetColumn(arrow, 1); label.Children.Add(arrow);
                    var button = new ActionButton { Content = label, HorizontalAlignment = HorizontalAlignment.Stretch,
                        HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(14, 10, 14, 10),
                        Margin = new Thickness(0, 0, column == columns - 1 ? 36 : 0, 0),
                        BorderThickness = new Thickness(0), Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
                    var sortColumn = column;
                    button.Click += (_, _) => sort?.Invoke(sortColumn);
                    Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, $"{caption.Text}. Sort ascending.");
                    ToolTipService.SetToolTip(button, "Sort ascending");
                    headings.Add((button, arrow, column));
                    border.Padding = new Thickness(0);
                    border.Child = button;
                }
                else originalRows[border] = targetRow;
                Grid.SetRow(border, targetRow); Grid.SetColumn(border, column);
                target.Children.Add(border);
            }
        }
        // Measure both sections together so header and body use identical column widths.
        var widths = new double[columns];
        foreach (var cell in header.Children.Concat(grid.Children).OfType<FrameworkElement>())
        {
            cell.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            var column = Grid.GetColumn(cell);
            widths[column] = Math.Max(widths[column], cell.DesiredSize.Width);
        }
        for (var column = 0; column < columns; column++)
            header.ColumnDefinitions[column].Width = grid.ColumnDefinitions[column].Width = new GridLength(widths[column]);
        var overflows = grid.RowDefinitions.Count > 10;
        var scroll = new ScrollViewer { Content = grid, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            FlowDirection = FlowDirection.LeftToRight,
            HorizontalScrollMode = ScrollMode.Enabled,
            VerticalScrollBarVisibility = overflows ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Disabled,
            VerticalScrollMode = overflows ? ScrollMode.Enabled : ScrollMode.Disabled };
        var headerScroll = new ScrollViewer { Content = header, FlowDirection = FlowDirection.LeftToRight,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, HorizontalScrollMode = ScrollMode.Enabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollMode = ScrollMode.Disabled };
        var horizontal = new Microsoft.UI.Xaml.Controls.Primitives.ScrollBar
        {
            Orientation = Orientation.Horizontal, FlowDirection = FlowDirection.LeftToRight,
            SmallChange = 32, Visibility = Visibility.Collapsed
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(horizontal, "Scroll table columns");
        // Keep scrolling coordinates left-to-right; place a native vertical bar explicitly on the left.
        var vertical = new Microsoft.UI.Xaml.Controls.Primitives.ScrollBar
        {
            Orientation = Orientation.Vertical, SmallChange = 24, Width = 12,
            HorizontalAlignment = HorizontalAlignment.Left,
            Visibility = overflows ? Visibility.Visible : Visibility.Collapsed
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(vertical, "Scroll table rows");
        var body = new Grid();
        body.Children.Add(scroll);
        // Overlay within the cell padding instead of reserving an empty column.
        body.Children.Add(vertical);
        var panel = new StackPanel();
        MarkdownTableToolbar? toolbar = null;
        void ApplyOrder()
        {
            var positions = new int[data.Order.Count];
            for (var index = 0; index < data.Order.Count; index++) positions[data.Order[index]] = index;
            foreach (var (element, originalRow) in originalRows)
            {
                var position = positions[originalRow];
                Grid.SetRow(element, position);
                ((Border)element).BorderThickness = new Thickness(0, 0, 0, position < data.Order.Count - 1 ? 1 : 0);
            }
            foreach (var (button, arrow, column) in headings)
            {
                var active = data.SortColumn == column;
                arrow.Text = active ? data.Descending ? "↓" : "↑" : "";
                var next = active && !data.Descending ? "descending" : "ascending";
                var state = active ? $"Sorted { (data.Descending ? "descending" : "ascending") } by {data.SortType}. " : "";
                var name = string.IsNullOrWhiteSpace(data.Headers[column]) ? $"Column {column + 1}" : data.Headers[column];
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, $"{name}. {state}Sort {next}.");
                ToolTipService.SetToolTip(button, state + "Sort " + next);
            }
            toolbar?.SetSorted(data.SortColumn is not null);
            scroll.ChangeView(null, 0, null, true);
            // Row heights can change order without changing the full table's size.
            DispatcherQueue.TryEnqueue(() => { grid.UpdateLayout(); UpdateViewport(); });
        }
        sort = column => { data.Sort(column); ApplyOrder(); };
        toolbar = new MarkdownTableToolbar(data, () => { data.Reset(); ApplyOrder(); });
        var headerRow = new Grid { Background = (Brush)Application.Current.Resources["ApplicationPageBackgroundThemeBrush"] };
        headerRow.Children.Add(headerScroll);
        headerRow.Children.Add(new Border { Child = toolbar, Width = 36, HorizontalAlignment = HorizontalAlignment.Right,
            Background = (Brush)Application.Current.Resources["ApplicationPageBackgroundThemeBrush"] });
        panel.Children.Add(headerRow);
        panel.Children.Add(horizontal);
        panel.Children.Add(body);
        var outline = new Border { Child = panel, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
            HorizontalAlignment = HorizontalAlignment.Left, MaxWidth = widths.Sum() + 2,
            BorderBrush = (Brush)Application.Current.Resources["ControlStrokeColorDefaultBrush"] };
        var synchronizing = false;
        void Synchronize(double offset)
        {
            if (synchronizing) return;
            synchronizing = true;
            headerScroll.ChangeView(offset, null, null, true);
            scroll.ChangeView(offset, null, null, true);
            horizontal.Value = offset;
            synchronizing = false;
        }
        horizontal.ValueChanged += (_, args) => Synchronize(args.NewValue);
        scroll.ViewChanged += (_, _) => Synchronize(scroll.HorizontalOffset);
        headerScroll.ViewChanged += (_, _) => Synchronize(headerScroll.HorizontalOffset);
        vertical.ValueChanged += (_, args) =>
        {
            if (Math.Abs(scroll.VerticalOffset - args.NewValue) > 0.1)
                scroll.ChangeView(null, args.NewValue, null, true);
        };
        scroll.ViewChanged += (_, _) =>
        {
            vertical.Maximum = scroll.ScrollableHeight;
            vertical.ViewportSize = scroll.ViewportHeight;
            vertical.Value = scroll.VerticalOffset;
        };
        // The viewport must hug the intrinsic table width, not the wider response column.
        // MaxWidth still allows the parent to constrain wide tables on narrow windows.
        void UpdateViewport()
        {
            horizontal.Maximum = scroll.ScrollableWidth;
            horizontal.ViewportSize = scroll.ViewportWidth;
            horizontal.LargeChange = scroll.ViewportWidth;
            horizontal.Visibility = scroll.ScrollableWidth > 0 ? Visibility.Visible : Visibility.Collapsed;
            vertical.Maximum = scroll.ScrollableHeight;
            vertical.ViewportSize = scroll.ViewportHeight;
            vertical.LargeChange = scroll.ViewportHeight;
            if (overflows)
            {
                var height = grid.RowDefinitions.Take(10).Sum(row => row.ActualHeight);
                if (height > 0) scroll.MaxHeight = height;
            }
        }
        grid.Loaded += (_, _) => UpdateViewport();
        grid.SizeChanged += (_, _) => UpdateViewport();
        scroll.SizeChanged += (_, _) => UpdateViewport();
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(scroll,
            overflows ? "Response table. Ten data rows visible; scroll down for more rows or horizontally for more columns."
                : "Response table. Scroll horizontally to see more columns.");
        Content = outline;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
    }
}
