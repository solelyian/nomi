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
    private sealed record ResultConsistency(string HeadingPrefix, string ConclusionPrefix, string[] Quantities);
    private sealed record PolicyConfig(string Version, int MaxCharacters, string RegenerationInstruction, string ForbiddenCharacters, string NumbersPattern, ResultConsistency ResultConsistency, Normalization[] Normalizations, BlockPattern[] BlockedPatterns);
}
