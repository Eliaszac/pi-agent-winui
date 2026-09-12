using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Markdig.Extensions.Tables;

namespace PiAgentGui.Utilities;

/// <summary>Stable, typed column sorting and lossless table serialization independent of native controls.</summary>
public sealed class MarkdownTableData
{
    private readonly string[][] rows;
    private int[] order;
    public IReadOnlyList<string> Headers { get; }
    public IReadOnlyList<int> Order => order;
    public int? SortColumn { get; private set; }
    public bool Descending { get; private set; }
    public string SortType { get; private set; } = "text";
    private static readonly string[] DateFormats = ["yyyy-MM-dd", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd'T'HH:mm:ssK", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK", "d MMM yyyy", "d MMMM yyyy", "MMM d, yyyy", "MMMM d, yyyy"];

    public MarkdownTableData(Table table)
    {
        var source = table.Cast<TableRow>().ToArray();
        var columns = Math.Max(table.ColumnDefinitions.Count, source.Select(row => row.Count).DefaultIfEmpty().Max());
        var heading = source.FirstOrDefault(row => row.IsHeader);
        Headers = Enumerable.Range(0, columns).Select(index => heading is not null && index < heading.Count
            ? MarkdownCellText.Read((TableCell)heading[index]) : "").ToArray();
        rows = source.Where(row => !row.IsHeader).Select(row => Enumerable.Range(0, columns)
            .Select(index => index < row.Count ? MarkdownCellText.Read((TableCell)row[index]) : "").ToArray()).ToArray();
        order = Enumerable.Range(0, rows.Length).ToArray();
    }

    public void Reset()
    {
        SortColumn = null; Descending = false;
        order = Enumerable.Range(0, rows.Length).ToArray();
    }

    public void Sort(int column)
    {
        if (column < 0 || column >= Headers.Count) throw new ArgumentOutOfRangeException(nameof(column));
        Descending = SortColumn == column && !Descending;
        SortColumn = column;
        var values = rows.Select(row => row[column].Trim()).ToArray();
        var populated = values.Where(value => value.Length > 0).ToArray();
        SortType = populated.Length > 0 && populated.All(value => Number(value, out _)) ? "number"
            : populated.Length > 0 && populated.All(value => Date(value, out _)) ? "date" : "text";
        order = Enumerable.Range(0, rows.Length).ToArray();
        Array.Sort(order, (left, right) =>
        {
            var a = values[left]; var b = values[right];
            if (a.Length == 0 || b.Length == 0)
                return a.Length == b.Length ? left.CompareTo(right) : a.Length == 0 ? 1 : -1;
            var comparison = StringComparer.OrdinalIgnoreCase.Compare(a, b);
            if (SortType == "number") { Number(a, out var x); Number(b, out var y); comparison = x.CompareTo(y); }
            if (SortType == "date") { Date(a, out var x); Date(b, out var y); comparison = x.CompareTo(y); }
            return comparison == 0 ? left.CompareTo(right) : Descending ? -Math.Sign(comparison) : Math.Sign(comparison);
        });
    }

    private static bool Number(string value, out decimal number)
    {
        number = 0;
        return Regex.IsMatch(value, @"^[+-]?(?:(?:\d{1,3}(?:,\d{3})+|\d+)(?:\.\d+)?|\.\d+)(?:[eE][+-]?\d+)?$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)
            && decimal.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out number);
    }
    private static bool Date(string value, out DateTimeOffset date) => DateTimeOffset.TryParseExact(value, DateFormats,
        CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal, out date);

    public string ExportCsv()
    {
        static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
        var output = new StringBuilder();
        output.AppendJoin(',', Headers.Select(Quote)).Append("\r\n");
        foreach (var index in order) output.AppendJoin(',', rows[index].Select(Quote)).Append("\r\n");
        return output.ToString();
    }

    public string ExportJson()
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        var keys = Headers.Select((header, index) =>
        {
            var basis = string.IsNullOrWhiteSpace(header) ? $"Column {index + 1}" : header;
            var key = basis;
            for (var suffix = 2; !used.Add(key); suffix++) key = $"{basis} ({suffix})";
            return key;
        }).ToArray();
        return JsonSerializer.Serialize(order.Select(index => keys.Select((key, column) => (key, value: rows[index][column]))
            .ToDictionary(pair => pair.key, pair => pair.value)), new JsonSerializerOptions { WriteIndented = true });
    }
}
