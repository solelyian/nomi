using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Nomi;

public sealed record PolicyResult(string Text, string Version, string[] Changes, string[] Reasons, bool Accepted);

public static class ResponsePolicy
{
    private static readonly PolicyConfig Config = JsonSerializer.Deserialize<PolicyConfig>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "content", "response-policy.json")),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new InvalidDataException("Invalid response policy.");
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant;
    public static int MaxCharacters => Config.MaxCharacters;
    public static string RegenerationInstruction => Config.RegenerationInstruction;
    public static readonly string[] RetriedReasons = ["conflicting-result", "wrong-arithmetic"];

    private static Regex Pattern(string pattern) => new(pattern, Options, TimeSpan.FromSeconds(1));
    private static PolicyResult Blocked(params string[] reasons) => new("", Config.Version, [], reasons, false);
    private static string[] Numbers(string text) => Pattern(Config.NumbersPattern)
        .Matches(text).Select(match => match.Value).ToArray();

    private static BigInteger CurrencyAmount(string amount)
    {
        var parts = Pattern("[ \u00a0\u202f]").Replace(amount, "")
            .Replace('−', '-').Replace(',', '.').Split('.');
        return BigInteger.Parse(parts[0] + (parts.Length > 1 ? parts[1] : "").PadRight(2, '0'),
            CultureInfo.InvariantCulture);
    }

    private static bool HasConflictingResult(string text)
    {
        var rules = Config.ResultConsistency;
        const string ending = @"[ \t]*[.!]?$";
        Match? heading = null;
        foreach (var quantity in rules.Quantities)
        {
            var match = Pattern($"^{rules.HeadingPrefix}{quantity}{ending}").Match(text.Split('\n')[0]);
            if (match.Success) { heading = match; break; }
        }
        if (heading is null) return false;
        var unit = heading.Groups["unit"].Value;
        var amount = CurrencyAmount(heading.Groups["amount"].Value);
        foreach (var quantity in rules.Quantities)
        {
            foreach (Match match in Pattern($"^{rules.ConclusionPrefix}{quantity}{ending}").Matches(text))
            {
                if (match.Groups["unit"].Value == unit && CurrencyAmount(match.Groups["amount"].Value) != amount)
                    return true;
            }
        }
        return false;
    }

    private static readonly Regex Grouped = new("^[0-9]{1,3}[.,][0-9]{3}$", RegexOptions.CultureInvariant);

    private static double DecimalValue(string value, bool grouped) => double.Parse(
        grouped && Grouped.IsMatch(value) ? value.Replace(".", "").Replace(",", "") : value.Replace(',', '.'),
        CultureInfo.InvariantCulture);

    private static int DecimalPlaces(string value, bool grouped)
    {
        if (grouped && Grouped.IsMatch(value)) return 0;
        var separator = value.LastIndexOfAny(['.', ',']);
        return separator < 0 ? 0 : value.Length - separator - 1;
    }

    private static bool Holds(string left, string right, bool grouped)
    {
        var token = Pattern(Config.Arithmetic.Token);
        var terms = new List<(double Value, bool Percent)>();
        var operators = new List<string>();
        foreach (Match match in token.Matches(left))
        {
            if (match.Groups["operator"].Success) operators.Add(match.Groups["operator"].Value);
            else if (match.Groups["number"].Success)
                terms.Add((DecimalValue(match.Groups["number"].Value, grouped), match.Groups["unit"].Value.Contains('%')));
        }
        var result = token.Matches(right).FirstOrDefault(match => match.Groups["number"].Success);
        if (result is null || terms.Count != operators.Count + 1) return true;
        var negative = Pattern(@"^[ \t]*[-−]").IsMatch(right);
        static bool Additive(string op) => op is "+" or "-" or "−";
        var additive = operators.Any(Additive);
        var resultPercent = result.Groups["unit"].Value.Contains('%');
        var percents = terms.Count(term => term.Percent) + (resultPercent ? 1 : 0);
        if (percents > 0 && additive && percents < terms.Count + 1) return true;
        static double Scale(bool percent) => percent ? 0.01 : 1;
        double sum = 0, sign = 1, product = terms[0].Value * Scale(terms[0].Percent);
        for (var index = 0; index < operators.Count; index++)
        {
            var op = operators[index];
            var value = terms[index + 1].Value * Scale(terms[index + 1].Percent);
            if (Additive(op))
            {
                sum += sign * product;
                sign = op == "+" ? 1 : -1;
                product = value;
            }
            else if (op is "/" or "÷")
            {
                if (value == 0) return true;
                product /= value;
            }
            else product *= value;
        }
        var expected = sum + sign * product;
        var shown = (negative ? -1 : 1) * DecimalValue(result.Groups["number"].Value, grouped) * Scale(resultPercent);
        var places = DecimalPlaces(result.Groups["number"].Value, grouped) + (resultPercent ? 2 : 0);
        return Math.Abs(expected - shown) <= 0.5 * Math.Pow(10, -places) + 1e-9 * Math.Max(1, Math.Abs(expected));
    }

    private static bool HasWrongArithmetic(string text) =>
        new Regex(Config.Arithmetic.Expression, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)).Matches(text)
            .Any(match => !Holds(match.Groups["left"].Value, match.Groups["right"].Value, false)
                && !Holds(match.Groups["left"].Value, match.Groups["right"].Value, true));

    public static PolicyResult Apply(string source, string format)
    {
        if (source.Length > Config.MaxCharacters) return Blocked("length");
        if (Pattern(Config.ForbiddenCharacters).IsMatch(source))
            return Blocked("hidden-characters");
        var changes = new List<string>();
        var text = source;
        foreach (var rule in Config.Normalizations)
        {
            var next = Pattern(rule.Pattern).Replace(text, rule.Replacement);
            if (next != text && !changes.Contains(rule.Id)) changes.Add(rule.Id);
            text = next;
        }
        var trimmed = text.Trim();
        if (text != trimmed && !changes.Contains("layout")) changes.Add("layout");
        text = trimmed;
        if (text.Length == 0) return Blocked("empty-response");
        var reasons = Config.BlockedPatterns.Where(rule => Pattern(rule.Pattern).IsMatch(text.Normalize()))
            .Select(rule => rule.Id).ToArray();
        if (reasons.Length > 0) return Blocked(reasons);
        if (HasConflictingResult(text.Normalize())) return Blocked("conflicting-result");
        if (HasWrongArithmetic(text.Normalize())) return Blocked("wrong-arithmetic");
        if (format == "steps")
        {
            var spaced = Pattern(@"\n+").Replace(text, "\n\n");
            if (spaced != text) changes.Add("reading-spacing");
            text = spaced;
        }
        if (!Numbers(source).SequenceEqual(Numbers(text))) return Blocked("numbers-changed");
        return new(text, Config.Version, changes.ToArray(), [], true);
    }

    private sealed record Normalization(string Id, string Pattern, string Replacement);
    private sealed record BlockPattern(string Id, string Pattern);
    private sealed record ArithmeticRules(string Expression, string Token);
    private sealed record ResultConsistency(string HeadingPrefix, string ConclusionPrefix, string[] Quantities);
    private sealed record PolicyConfig(string Version, int MaxCharacters, string RegenerationInstruction, string ForbiddenCharacters, string NumbersPattern, ResultConsistency ResultConsistency, ArithmeticRules Arithmetic, Normalization[] Normalizations, BlockPattern[] BlockedPatterns);
}
