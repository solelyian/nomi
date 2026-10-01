using System.Globalization;
using System.Text.RegularExpressions;

namespace Nomi;

public sealed record TableIssue(string Row, string Column, string Formula, decimal Expected, decimal Shown);

public sealed record TableReport(int Rows, IReadOnlyList<TableIssue> Issues);

public static class TableCheck
{
    private static readonly Regex TotalLabel = new(@"^\s*(total|totaux|sous-total|subtotal|sum|somme|grand total)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex NumberCell = new(@"^[\s(]*[-−+]?[\s€$£]*\d[\d\s\u00a0\u202f.,']*\s*(%|€|\$|£|eur|usd|k€)?[\s)]*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static IReadOnlyList<string[]> ParseRows(IEnumerable<string> lines)
    {
        var rows = lines
            .Select(line => line.Trim().Trim('|').Trim())
            .Where(line => line.Contains('|', StringComparison.Ordinal))
            .Where(line => !Regex.IsMatch(line, @"^[\s|:\-–—]+$"))
            .Select(line => line.Split('|').Select(cell => cell.Trim()).ToArray())
            .Take(16)
            .ToArray();
        return rows.Length == 1 ? Unfold(rows[0]) : rows;
    }

    private static IReadOnlyList<string[]> Unfold(string[] cells)
    {
        var first = Array.FindIndex(cells, cell => NumberCell.IsMatch(cell));
        foreach (var width in new[] { first - 1, first })
        {
            if (width < 2 || cells.Length < width * 3 || cells.Length % width != 0) continue;
            var rows = cells.Chunk(width).Take(16).ToArray();
            if (rows.Skip(1).All(row => row.Any(cell => NumberCell.IsMatch(cell)))) return rows;
        }
        return [cells];
    }

    public static bool TryNumber(string cell, string language, out decimal value, out bool percent)
    {
        value = 0;
        percent = cell.Contains('%', StringComparison.Ordinal);
        if (!NumberCell.IsMatch(cell)) return false;
        var negative = cell.Contains('-', StringComparison.Ordinal) || cell.Contains('−', StringComparison.Ordinal)
            || (cell.Contains('(', StringComparison.Ordinal) && cell.Contains(')', StringComparison.Ordinal));
        var digits = new string(cell.Where(c => char.IsDigit(c) || c is '.' or ',').ToArray());
        if (digits.Length == 0) return false;
        var comma = digits.LastIndexOf(',');
        var dot = digits.LastIndexOf('.');
        string normalized;
        if (comma >= 0 && dot >= 0)
        {
            var decimalMark = comma > dot ? ',' : '.';
            var group = decimalMark == ',' ? "." : ",";
            normalized = digits.Replace(group, "", StringComparison.Ordinal).Replace(',', '.');
        }
        else if (comma >= 0)
        {
            var thousands = language == "en" && Regex.IsMatch(digits, @"^\d{1,3}(,\d{3})+$");
            normalized = thousands ? digits.Replace(",", "", StringComparison.Ordinal) : digits.Replace(',', '.');
        }
        else if (dot >= 0)
        {
            var thousands = language == "fr" && Regex.IsMatch(digits, @"^\d{1,3}(\.\d{3})+$");
            normalized = thousands ? digits.Replace(".", "", StringComparison.Ordinal) : digits;
        }
        else normalized = digits;
        if (normalized.Count(c => c == '.') > 1) return false;
        if (!decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value)) return false;
        if (negative) value = -value;
        if (percent) value /= 100;
        return true;
    }

    public static TableReport Check(IReadOnlyList<string[]> rows, string language)
    {
        if (rows.Count < 2) return new(0, []);
        var width = rows.Max(row => row.Length);
        var parsed = rows.Select(row => Enumerable.Range(0, width).Select(index =>
        {
            if (index >= row.Length) return ((decimal?)null, false);
            return TryNumber(row[index], language, out var value, out var percent) ? (value, percent) : ((decimal?)null, false);
        }).ToArray()).ToArray();
        var header = parsed[0].Count(cell => cell.Item1 is not null) == 0;
        var names = Enumerable.Range(0, width)
            .Select(index => header && index < rows[0].Length ? Regex.Replace(rows[0][index], @"\s*\(.*?\)\s*", "").Trim() : $"#{index + 1}")
            .ToArray();
        var start = header ? 1 : 0;
        string Label(int row) => rows[row].FirstOrDefault(cell => !TryNumber(cell, language, out _, out _)) ?? $"#{row + 1}";
        bool IsTotal(int row) => TotalLabel.IsMatch(Label(row));
        var issues = new List<TableIssue>();

        var data = Enumerable.Range(start, rows.Count - start).Where(row => !IsTotal(row)).ToArray();
        for (var total = start; total < rows.Count; total++)
        {
            if (!IsTotal(total)) continue;
            var above = data.Where(row => row < total).ToArray();
            for (var column = 0; column < width; column++)
            {
                var (shown, percent) = parsed[total][column];
                if (shown is null || percent) continue;
                var values = above.Select(row => parsed[row][column]).Where(cell => cell.Item1 is not null && !cell.Item2)
                    .Select(cell => cell.Item1!.Value).ToArray();
                if (values.Length < 2 || values.Length < above.Length - 1) continue;
                var sum = values.Sum();
                if (Math.Abs(sum - shown.Value) > Tolerance(shown.Value))
                    issues.Add(new(Label(total), names[column], $"Σ {names[column]}", Math.Round(sum, 2), shown.Value));
            }
        }

        var best = (Matches: 0, Base: -1, Rate: -1, Result: -1);
        for (var rate = 0; rate < width; rate++)
        for (var source = 0; source < width; source++)
        for (var result = 0; result < width; result++)
        {
            if (rate == source || rate == result || source == result) continue;
            var candidates = data.Where(row => parsed[row][rate] is { Item1: not null, Item2: true }
                && parsed[row][source] is { Item1: not null, Item2: false }
                && parsed[row][result] is { Item1: not null, Item2: false }).ToArray();
            var matches = candidates.Count(row => Product(parsed[row][source].Item1!.Value, parsed[row][rate].Item1!.Value, parsed[row][result].Item1!.Value));
            if (matches >= 2 && matches * 2 >= candidates.Length && matches > best.Matches) best = (matches, source, rate, result);
        }
        if (best.Matches > 0)
        {
            foreach (var row in data)
            {
                var source = parsed[row][best.Base].Item1;
                var rate = parsed[row][best.Rate].Item1;
                var shown = parsed[row][best.Result].Item1;
                if (source is null || rate is null || shown is null || parsed[row][best.Rate].Item2 == false) continue;
                if (Product(source.Value, rate.Value, shown.Value)) continue;
                issues.Add(new(Label(row), names[best.Result], $"{names[best.Base]} × {names[best.Rate]}",
                    Math.Round(source.Value * rate.Value, 2), shown.Value));
            }
        }
        return new(rows.Count - start, issues.Take(4).ToArray());
    }

    private static bool Product(decimal source, decimal rate, decimal shown) =>
        Math.Abs(source * rate - shown) <= Tolerance(shown);

    private static decimal Tolerance(decimal value) => Math.Max(0.011m, Math.Abs(value) * 0.0005m);

    public static string Format(decimal value, CultureInfo culture) =>
        value.ToString(value == Math.Round(value) ? "#,##0" : "#,##0.00", culture);
}
