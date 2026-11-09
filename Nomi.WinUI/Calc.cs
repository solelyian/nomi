using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Nomi;

public sealed record CalcStep(string Id, string Expression, string Unit = "");

public sealed record CalcResult(string Id, string Expression, decimal Value, string Unit, bool Verified);

public sealed class CalcException(string message) : Exception(message);

public static partial class Calc
{
    public const int MaxSteps = 12;
    private const int MaxLength = 200;

    public static IReadOnlyList<CalcStep> ParsePlan(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start) return [];
        try
        {
            using var json = JsonDocument.Parse(text[start..(end + 1)]);
            if (!json.RootElement.TryGetProperty("steps", out var steps) || steps.ValueKind != JsonValueKind.Array) return [];
            var plan = new List<CalcStep>();
            foreach (var step in steps.EnumerateArray().Take(MaxSteps))
            {
                var id = step.TryGetProperty("id", out var i) ? i.GetString() ?? "" : "";
                var expression = step.TryGetProperty("expr", out var e) ? e.GetString() ?? "" : "";
                var unit = step.TryGetProperty("unit", out var u) ? u.GetString() ?? "" : "";
                if (!IdPattern().IsMatch(id) || expression.Length is 0 or > MaxLength) continue;
                plan.Add(new CalcStep(id, expression, unit.Length > 12 ? unit[..12] : unit));
            }
            return plan;
        }
        catch (JsonException) { return []; }
    }

    public static IReadOnlyList<CalcResult> Run(IEnumerable<CalcStep> plan)
    {
        var exact = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var check = new Dictionary<string, double>(StringComparer.Ordinal);
        var results = new List<CalcResult>();
        foreach (var step in plan.Take(MaxSteps))
        {
            try
            {
                var value = new Exact(step.Expression, exact).Parse();
                var again = Recheck(step.Expression, check);
                var verified = Math.Abs((double)value - again) <= 1e-9 * Math.Max(1, Math.Abs(again));
                exact[step.Id] = value;
                check[step.Id] = again;
                results.Add(new CalcResult(step.Id, step.Expression, value, step.Unit, verified));
            }
            catch (Exception error) when (error is CalcException or OverflowException or DivideByZeroException or FormatException)
            {
                results.Add(new CalcResult(step.Id, step.Expression, 0, step.Unit, false));
            }
        }
        return results;
    }

    public static decimal Evaluate(string expression) => new Exact(expression, new Dictionary<string, decimal>()).Parse();

    public static string Format(decimal value, CultureInfo culture)
    {
        var rounded = Math.Round(value, Math.Abs(value) >= 100 ? 2 : 4, MidpointRounding.AwayFromZero);
        return rounded.ToString(rounded == Math.Truncate(rounded) ? "#,0" : "#,0.##", culture);
    }

    public static IReadOnlyList<string> Unverified(string answer, IEnumerable<decimal> allowed)
    {
        var known = allowed.Select(value => Math.Round(value, 2)).ToHashSet();
        var blocked = new List<string>();
        foreach (Match match in NumberPattern().Matches(answer))
        {
            if (!TryRead(match.Value, out var number)) continue;
            if (number == Math.Truncate(number) && number is >= 0 and <= 10) continue;
            if (!known.Contains(Math.Round(number, 2)) && !known.Contains(Math.Round(number / 100, 2))) blocked.Add(match.Value.Trim());
        }
        return blocked;
    }

    public static IEnumerable<decimal> Numbers(string text)
    {
        foreach (Match match in NumberPattern().Matches(text))
            if (TryRead(match.Value, out var number)) yield return number;
    }

    private static bool TryRead(string text, out decimal number)
    {
        var clean = text.Replace("\u202F", "").Replace("\u00A0", "").Replace(" ", "");
        if (clean.Contains(',') && clean.Contains('.')) clean = clean.Replace(",", "");
        else if (clean.Count(c => c == ',') == 1 && clean.Length - clean.IndexOf(',') - 1 != 3) clean = clean.Replace(',', '.');
        else clean = clean.Replace(",", "");
        return decimal.TryParse(clean, NumberStyles.Number, CultureInfo.InvariantCulture, out number);
    }

    private static double Recheck(string expression, IReadOnlyDictionary<string, double> names)
    {
        var output = new Stack<double>();
        var operators = new Stack<char>();
        var tokens = Tokens(expression).ToList();
        var expectValue = true;
        void Apply()
        {
            var op = operators.Pop();
            if (op == '~') { output.Push(-output.Pop()); return; }
            if (op == '%') { output.Push(output.Pop() / 100); return; }
            var right = output.Pop();
            var left = output.Pop();
            output.Push(op switch
            {
                '+' => left + right,
                '-' => left - right,
                '*' => left * right,
                '/' => right == 0 ? throw new DivideByZeroException() : left / right,
                '^' => Math.Pow(left, right),
                _ => throw new CalcException("operator")
            });
        }
        static int Rank(char op) => op switch { '+' or '-' => 1, '*' or '/' => 2, '~' => 3, '^' => 4, _ => 0 };
        foreach (var token in tokens)
        {
            if (char.IsDigit(token[0]) || token[0] == '.')
            {
                output.Push(double.Parse(token, CultureInfo.InvariantCulture));
                expectValue = false;
            }
            else if (char.IsLetter(token[0]))
            {
                output.Push(names.TryGetValue(token, out var value) ? value : throw new CalcException($"unknown {token}"));
                expectValue = false;
            }
            else if (token == "(") { operators.Push('('); expectValue = true; }
            else if (token == ")")
            {
                while (operators.Count > 0 && operators.Peek() != '(') Apply();
                if (operators.Count == 0) throw new CalcException("parenthesis");
                operators.Pop();
                expectValue = false;
            }
            else if (token == "%") { operators.Push('%'); Apply(); }
            else
            {
                var op = token[0];
                if (expectValue && op == '-') op = '~';
                else if (expectValue && op == '+') continue;
                while (operators.Count > 0 && operators.Peek() != '(' && op != '~'
                    && (Rank(operators.Peek()) > Rank(op) || Rank(operators.Peek()) == Rank(op) && op != '^')) Apply();
                operators.Push(op);
                expectValue = true;
            }
        }
        while (operators.Count > 0)
        {
            if (operators.Peek() == '(') throw new CalcException("parenthesis");
            Apply();
        }
        if (output.Count != 1 || double.IsNaN(output.Peek()) || double.IsInfinity(output.Peek())) throw new CalcException("expression");
        return output.Pop();
    }

    private static IEnumerable<string> Tokens(string expression)
    {
        var text = Normalize(expression);
        for (var i = 0; i < text.Length;)
        {
            var c = text[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            if (char.IsDigit(c) || c == '.')
            {
                var start = i;
                while (i < text.Length && (char.IsDigit(text[i]) || text[i] == '.')) i++;
                yield return text[start..i];
                continue;
            }
            if (char.IsLetter(c) || c == '_')
            {
                var start = i;
                while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_')) i++;
                yield return text[start..i];
                continue;
            }
            if ("+-*/^()%".Contains(c)) { yield return c.ToString(); i++; continue; }
            throw new CalcException($"character {c}");
        }
    }

    private static string Normalize(string expression)
    {
        if (expression.Length > MaxLength) throw new CalcException("length");
        var text = expression.Replace('×', '*').Replace('÷', '/').Replace('−', '-').Replace("\u202F", "").Replace("\u00A0", "");
        return DecimalComma().Replace(ThousandsSpace().Replace(text, "$1"), "$1.$2");
    }

    private sealed class Exact(string expression, IReadOnlyDictionary<string, decimal> names)
    {
        private readonly List<string> tokens = [.. Tokens(expression)];
        private int at;

        public decimal Parse()
        {
            var value = Sum();
            if (at != tokens.Count) throw new CalcException("trailing");
            return value;
        }

        private string? Peek => at < tokens.Count ? tokens[at] : null;

        private decimal Sum()
        {
            var value = Product();
            while (Peek is "+" or "-") value = tokens[at++] == "+" ? value + Product() : value - Product();
            return value;
        }

        private decimal Product()
        {
            var value = Unary();
            while (Peek is "*" or "/")
            {
                var op = tokens[at++];
                var right = Unary();
                value = op == "*" ? value * right : right == 0 ? throw new DivideByZeroException() : value / right;
            }
            return value;
        }

        private decimal Unary()
        {
            if (Peek == "-") { at++; return -Unary(); }
            if (Peek == "+") { at++; return Unary(); }
            return Power();
        }

        private decimal Power()
        {
            var value = Postfix();
            if (Peek != "^") return value;
            at++;
            var exponent = Unary();
            if (exponent != Math.Truncate(exponent) || Math.Abs(exponent) > 64) throw new CalcException("exponent");
            var result = 1m;
            for (var i = 0; i < Math.Abs(exponent); i++) result *= value;
            return exponent < 0 ? 1 / result : result;
        }

        private decimal Postfix()
        {
            var value = Atom();
            while (Peek == "%") { at++; value /= 100; }
            return value;
        }

        private decimal Atom()
        {
            var token = Peek ?? throw new CalcException("end");
            at++;
            if (token == "(")
            {
                var value = Sum();
                if (Peek != ")") throw new CalcException("parenthesis");
                at++;
                return value;
            }
            if (char.IsDigit(token[0]) || token[0] == '.') return decimal.Parse(token, NumberStyles.Number, CultureInfo.InvariantCulture);
            if (char.IsLetter(token[0]) && names.TryGetValue(token, out var named)) return named;
            throw new CalcException($"token {token}");
        }
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]{0,15}$")]
    private static partial Regex IdPattern();

    [GeneratedRegex(@"(?<![\w.,])\d{1,3}(?:[\u202F\u00A0 ]\d{3})+(?:[.,]\d+)?|(?<![\w.,])\d+(?:[.,]\d+)?")]
    private static partial Regex NumberPattern();

    [GeneratedRegex(@"(\d) (?=\d{3}(?!\d))")]
    private static partial Regex ThousandsSpace();

    [GeneratedRegex(@"(\d),(\d)")]
    private static partial Regex DecimalComma();
}
