using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Nomi;

public sealed record CalcStep(string Id, string Expression, string Unit = "");

public sealed record CalcResult(string Id, string Expression, decimal Value, string Unit, bool Verified, string Dimension = "");

/// <summary>Certificat de calcul — structure vérifiable, jamais du texte libre (SPEC 7.3).</summary>
public sealed record CalcCertificate(
    string Engine,
    IReadOnlyList<CalcCertificateStep> Steps,
    string Grounding,
    string Verifier,
    string Status);

public sealed record CalcCertificateStep(string Id, string Expression, string Value, string Unit, string Dimension);

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
                var expression = step.TryGetProperty("expr", out var e) ? (e.GetString() ?? "").Replace("{", "").Replace("}", "") : "";
                var unit = step.TryGetProperty("unit", out var u) ? u.GetString() ?? "" : "";
                if (!IdPattern().IsMatch(id) || expression.Length is 0 or > MaxLength) continue;
                plan.Add(new CalcStep(id, expression, unit.Length > 12 ? unit[..12] : unit));
            }
            return plan;
        }
        catch (JsonException) { return []; }
    }

    public static IReadOnlyList<CalcResult> Run(IEnumerable<CalcStep> plan, bool? decimalComma = null)
    {
        // Noyau exact recoupé par le vérificateur indépendant — CalcEngine :
        // l'accord est l'égalité exacte des fractions réduites, pas une approximation.
        var kernel = new CalcEngine.Kernel();
        var verifier = new CalcEngine.VerifierEngine();
        // SPEC 3.1 : les dimensions se propagent par les identificateurs —
        // « remise » référencé dans une étape suivante porte sa dimension monétaire.
        var bindings = new Dictionary<string, CalcEngine.Measured>(StringComparer.Ordinal);
        var results = new List<CalcResult>();
        foreach (var step in plan.Take(MaxSteps))
        {
            try
            {
                // Normalisation frontière (SPEC 3.3) : les littéraux sont traduits
                // en forme canonique avant le noyau — la locale explicite tranche
                // les formes ambiguës (« 1,200 »), refusées sans elle.
                var canonical = Canonical(step.Expression, decimalComma);
                var kernelValue = kernel.EvaluateDimensional(canonical, bindings);
                var verifierValue = verifier.EvaluateDimensional(canonical, bindings);
                var verified = kernelValue == verifierValue;
                if (verified) bindings[step.Id] = kernelValue;
                var dimension = kernelValue.Dim.Kind == CalcEngine.DimKind.Currency ? kernelValue.Dim.Code : "";
                results.Add(new CalcResult(step.Id, step.Expression, verified ? ToDecimal(kernelValue.Value) : 0, step.Unit, verified, dimension));
            }
            catch (Exception error) when (error is CalcException or CalcEngine.CalculationException or OverflowException or DivideByZeroException or FormatException)
            {
                results.Add(new CalcResult(step.Id, step.Expression, 0, step.Unit, false));
            }
        }
        return results;
    }

    // Traduit chaque littéral numérique en forme canonique invariante (point
    // décimal, sans groupement) en préservant le reste de l'expression.
    private static string Canonical(string expression, bool? decimalComma)
    {
        var text = Normalize(expression);
        var builder = new System.Text.StringBuilder(text.Length);
        for (var i = 0; i < text.Length;)
        {
            var c = text[i];
            if (char.IsDigit(c) || c == '.')
            {
                var start = i;
                while (i < text.Length && (char.IsDigit(text[i]) || text[i] is '.' or ',')) i++;
                builder.Append(Literal(text[start..i], decimalComma));
                continue;
            }
            builder.Append(c);
            i++;
        }
        return builder.ToString();
    }

    // Conversion d'affichage uniquement : l'exactitude vit dans le rationnel,
    // le decimal ne sert qu'au formatage et au masquage — jamais au calcul.
    private static decimal ToDecimal(CalcEngine.Rational value)
    {
        try
        {
            return (decimal)value.Numerator / (decimal)value.Denominator;
        }
        catch (OverflowException)
        {
            return decimal.Parse(value.DisplayFrench().Replace("\u202F", "").Replace(',', '.'),
                System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    // Numbers a plan may use without finding them in the request: counts and the usual units of time and percent.
    private static readonly decimal[] Constants = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 24, 30, 31, 52, 60, 100, 365, 1000];

    // A step whose numbers do not all come from the request, its documents or an earlier step is not verified.
    public static IReadOnlyList<CalcResult> Ground(IReadOnlyList<CalcResult> results, IEnumerable<decimal> known, bool? decimalComma = null)
    {
        var given = known.Select(Math.Abs).Concat(Constants).ToArray();
        // A rate read as 20 may be written 0.2, 1.2 or 0.8 in a step.
        var values = given.Concat(given.SelectMany(value => new[] { value / 100, 1 + value / 100, 1 - value / 100 })).ToHashSet();
        return results.Select(result =>
        {
            if (!result.Verified) return result;
            try
            {
                var grounded = Tokens(result.Expression, decimalComma)
                    .Where(token => char.IsDigit(token[0]) || token[0] == '.')
                    .All(token => values.Contains(decimal.Parse(token, NumberStyles.Number, CultureInfo.InvariantCulture)));
                return grounded ? result : result with { Verified = false };
            }
            catch (Exception error) when (error is CalcException or FormatException or OverflowException) { return result with { Verified = false }; }
        }).ToList();
    }

    /// <summary>Plan de classification lu depuis la réponse du modèle — strate 1 (§8.2).</summary>
    public sealed record SchemaPlan(string Schema, IReadOnlyDictionary<string, decimal> Slots, string Answer);

    public static SchemaPlan? ParseSchemaPlan(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start) return null;
        try
        {
            using var json = JsonDocument.Parse(text[start..(end + 1)]);
            if (!json.RootElement.TryGetProperty("schema", out var schemaElement) || schemaElement.ValueKind != JsonValueKind.String) return null;
            if (!json.RootElement.TryGetProperty("slots", out var slotsElement) || slotsElement.ValueKind != JsonValueKind.Object) return null;
            var slots = new Dictionary<string, decimal>();
            foreach (var slot in slotsElement.EnumerateObject())
            {
                if (slot.Name.Length > 32 || !slot.Name.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_')) return null;
                var literal = slot.Value.ValueKind == JsonValueKind.String ? slot.Value.GetString()!
                    : slot.Value.ValueKind == JsonValueKind.Number ? slot.Value.GetRawText() : null;
                if (literal is null) return null;
                if (!decimal.TryParse(literal.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)) return null;
                slots[slot.Name] = parsed;
            }
            var answer = json.RootElement.TryGetProperty("answer", out var answerElement) && answerElement.ValueKind == JsonValueKind.String
                ? answerElement.GetString() ?? "" : "";
            return new SchemaPlan(schemaElement.GetString()!, slots, answer);
        }
        catch (JsonException) { return null; }
    }

    // A slot value must trace to the request, a whitelisted constant or a rate variant — same rule as expressions.
    public static bool SlotsGrounded(IReadOnlyDictionary<string, decimal> slots, IEnumerable<decimal> known)
    {
        var given = known.Select(Math.Abs).Concat(Constants).ToArray();
        var values = given.Concat(given.SelectMany(value => new[] { value / 100, 1 + value / 100, 1 - value / 100 }))
            .Select(Math.Abs).ToHashSet();
        return slots.Values.All(value => values.Contains(Math.Abs(value)));
    }

    /// <summary>Applique un plan de classification : ancrage exigé, calcul mécanique
    /// du schéma avec invariants et vérification inverse — tout écart est bloquant.</summary>
    public static CalcEngine.SchemaOutcome? RunSchema(SchemaPlan plan, IEnumerable<decimal> known)
    {
        if (!CalcEngine.Schemas.Catalog.Contains(plan.Schema)) return null;
        if (!SlotsGrounded(plan.Slots, known)) return null;
        try
        {
            // Les slots décimaux frontière sont convertis en rationnels exacts —
            // decimal est un type à virgule fixe base 10, la conversion ne perd rien.
            var exact = plan.Slots.ToDictionary(pair => pair.Key, pair => DecimalRational(pair.Value));
            var outcome = CalcEngine.Schemas.Apply(plan.Schema, exact);
            return outcome.InvariantsHeld && outcome.InverseVerified ? outcome : null;
        }
        catch (Exception error) when (error is CalcEngine.SchemaException or CalcEngine.CalculationException or OverflowException)
        {
            return null;
        }
    }

    // Conversion exacte decimal -> rationnel : le decimal est un type à virgule
    // fixe base 10, la conversion ne perd jamais rien.
    private static CalcEngine.Rational DecimalRational(decimal value)
    {
        var bits = decimal.GetBits(value);
        var scale = (bits[3] >> 16) & 0xFF;
        // décimal 96 bits : reconstruction exacte de la mantisse par les trois mots
        var mantissa = new System.Numerics.BigInteger(bits[2]);
        mantissa = (mantissa << 32) | new System.Numerics.BigInteger((uint)bits[1]);
        mantissa = (mantissa << 32) | new System.Numerics.BigInteger((uint)bits[0]);
        if (bits[3] < 0) mantissa = -mantissa;
        return new CalcEngine.Rational(mantissa, System.Numerics.BigInteger.Pow(10, scale));
    }

    /// <summary>Certificat de la voie catalogue — la forme la plus forte (§8).</summary>
    public static CalcCertificate CertifySchema(CalcEngine.SchemaOutcome outcome, string engineVersion = "nomi-calc/1.1")
    {
        var steps = outcome.Values.OrderBy(pair => pair.Key)
            .Select(pair => new CalcCertificateStep(pair.Key, outcome.Schema, pair.Value.DisplayFrench(), "", ""))
            .ToList();
        return new CalcCertificate(engineVersion, steps, "anchored", "inverse", "verified");
    }

    /// <summary>Assemble le certificat de calcul d'une réponse — la structure
    /// affichée et consignée, que le texte généré ne peut pas imiter (SPEC 7.3).</summary>
    public static CalcCertificate Certify(IReadOnlyList<CalcResult> results, bool grounded, string engineVersion = "nomi-calc/1.1")
    {
        var steps = results.Where(result => result.Verified)
            .Select(result => new CalcCertificateStep(
                result.Id, result.Expression,
                result.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                result.Unit, result.Dimension))
            .ToList();
        // Le statut reflète la voie : `verified` exige le catalogue — la voie
        // expressions libres ne produit que `interpreted` (strate 5, §8.6).
        var status = results.Count > 0 && results.All(result => result.Verified) && grounded ? "interpreted" : "ungrounded";
        return new CalcCertificate(engineVersion, steps, grounded ? "anchored" : "unanchored", "agree", status);
    }

    // The sentence the model writes around the figures, with {id} where each verified result goes.
    public static string PlanAnswer(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start) return "";
        try
        {
            using var json = JsonDocument.Parse(text[start..(end + 1)]);
            return json.RootElement.TryGetProperty("answer", out var answer) && answer.ValueKind == JsonValueKind.String
                ? (answer.GetString() ?? "").Trim() : "";
        }
        catch (JsonException) { return ""; }
    }

    // Puts the engine's figures in the model's sentence; null when it names a step that is missing or unverified.
    public static string? Compose(string template, IReadOnlyList<CalcResult> results, CultureInfo culture)
    {
        var byId = results.Where(result => result.Verified).GroupBy(result => result.Id).ToDictionary(group => group.Key, group => group.Last().Value);
        var missing = false;
        var text = PlaceholderPattern().Replace(template, match =>
        {
            if (byId.TryGetValue(match.Groups[1].Value, out var value)) return Format(value, culture);
            missing = true;
            return match.Value;
        });
        return missing || text.Length == 0 ? null : text;
    }

    // The posed calculation shown under the answer: one line per step, with the engine's result.
    public static IReadOnlyList<string> Posed(IReadOnlyList<CalcResult> results, CultureInfo culture) =>
        results.Where(result => result.Verified)
            .Select(result => $"{result.Id} = {Pretty(result.Expression)} = {Format(result.Value, culture)} {result.Unit}".TrimEnd())
            .ToList();

    private static string Pretty(string expression) =>
        expression.Replace("*", " × ").Replace("/", " ÷ ").Replace("  ", " ").Trim();

    public static decimal Evaluate(string expression, bool? decimalComma = null)
    {
        var canonical = Canonical(expression, decimalComma);
        return ToDecimal(new CalcEngine.Kernel().Evaluate(canonical, new Dictionary<string, CalcEngine.Rational>()));
    }

    public static string Format(decimal value, CultureInfo culture)
    {
        var rounded = Math.Round(value, Math.Abs(value) >= 100 ? 2 : 4, MidpointRounding.AwayFromZero);
        return rounded.ToString(rounded == Math.Truncate(rounded) ? "#,0" : "#,0.##", culture);
    }

    public static IReadOnlyList<string> Unverified(string answer, IEnumerable<decimal> allowed) =>
        Blocked(answer, allowed).Select(span => span.Text.Trim()).ToList();

    public static string Mask(string answer, IEnumerable<decimal> allowed, out int hidden)
    {
        var blocked = Blocked(answer, allowed);
        hidden = blocked.Count;
        var text = new System.Text.StringBuilder(answer);
        foreach (var span in blocked.OrderByDescending(span => span.Index))
            text.Remove(span.Index, span.Length).Insert(span.Index, '…');
        return text.ToString();
    }

    private readonly record struct Span(int Index, int Length, string Text);

    private static List<Span> Blocked(string answer, IEnumerable<decimal> allowed)
    {
        var known = allowed.Select(Math.Abs).Distinct().ToArray();
        var blocked = new List<Span>();
        foreach (Match match in NumberPattern().Matches(answer))
        {
            if (Dated(answer, match)) continue;
            var readings = Readings(match.Value, false);
            if (readings.Count == 0) continue;
            if (match.Value.All(char.IsDigit) && readings[0] <= 10) continue;
            if (!readings.Any(number => known.Any(value => Same(value, number))))
                blocked.Add(new Span(match.Index, match.Length, match.Value));
        }
        // Nombres en toutes lettres — SPEC 7.2 : couverture bornée au français
        // usuel ; toute séquence reconnue et intraçable est masquée.
        var wordMatches = WordPattern().Matches(answer);
        var words = wordMatches.Select(match => (match.Value, Index: match.Index, Length: match.Length)).ToList();
        var at = 0;
        while (at < words.Count)
        {
            var remaining = words.Skip(at).Select(word => word.Value).ToArray();
            if (CalcEngine.NumberWords.ParseSequence(remaining) is { } parsed && parsed.Consumed > 0)
            {
                var first = words[at];
                var last = words[at + parsed.Consumed - 1];
                var value = (decimal)parsed.Value;
                // 0–10 sont des constantes structurelles — traçables sans provenance.
                var traceable = value <= 10 || known.Any(knownValue => Same(knownValue, value));
                var overlaps = blocked.Any(span => first.Index < span.Index + span.Length && span.Index < first.Index + (last.Index + last.Length - first.Index));
                if (!traceable && !overlaps)
                {
                    var length = last.Index + last.Length - first.Index;
                    blocked.Add(new Span(first.Index, length, answer.Substring(first.Index, length)));
                }
                at += parsed.Consumed;
            }
            else at++;
        }
        return blocked;
    }

    private static bool Dated(string text, Match match)
    {
        foreach (Match date in DatePattern().Matches(text))
            if (match.Index >= date.Index && match.Index < date.Index + date.Length) return true;
        return false;
    }

    private static bool Same(decimal value, decimal number)
    {
        if (Math.Abs(value) > 1e20m) return value == number;
        var places = Math.Min(6, (int)number.Scale);
        return Math.Round(value, places, MidpointRounding.AwayFromZero) == number
            || Math.Round(value * 100, places, MidpointRounding.AwayFromZero) == number;
    }

    public static IEnumerable<decimal> Numbers(string text)
    {
        foreach (Match match in NumberPattern().Matches(text))
            foreach (var number in Readings(match.Value, false)) yield return number;
    }

    private static IReadOnlyList<decimal> Readings(string text, bool expression, bool? comma = null)
    {
        var clean = text.Replace("\u202F", "").Replace("\u00A0", "").Replace(" ", "");
        var commas = clean.Count(c => c == ',');
        var dots = clean.Count(c => c == '.');
        string[] forms;
        if (commas + dots == 0) forms = [clean];
        else if (commas > 0 && dots > 0)
        {
            var decimalComma = clean.LastIndexOf(',') > clean.LastIndexOf('.');
            var (group, mark) = decimalComma ? ('.', ',') : (',', '.');
            var whole = clean[..clean.LastIndexOf(mark)];
            forms = clean.Count(c => c == mark) == 1 && Grouped().IsMatch(whole)
                ? [whole.Replace(group.ToString(), "") + "." + clean[(clean.LastIndexOf(mark) + 1)..]] : [];
        }
        else
        {
            var mark = commas > 0 ? ',' : '.';
            var whole = clean.Replace(mark.ToString(), "");
            var fraction = clean.Replace(mark, '.');
            var tail = clean.Length - clean.LastIndexOf(mark) - 1;
            forms = commas + dots > 1 ? Grouped().IsMatch(clean) ? [whole] : []
                : tail != 3 || clean.StartsWith('0') || expression && mark == '.' ? [fraction]
                : expression ? comma is null ? [] : comma.Value ? [fraction] : [whole]
                : mark == ',' ? [whole, fraction] : [fraction, whole];
        }
        var numbers = new List<decimal>();
        foreach (var form in forms)
            if (decimal.TryParse(form, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number)) numbers.Add(number);
        return numbers;
    }

    private static string Literal(string token, bool? comma) =>
        Readings(token, true, comma) is [var number] ? number.ToString(CultureInfo.InvariantCulture) : throw new CalcException($"number {token}");

    private static IEnumerable<string> Tokens(string expression, bool? comma = null)
    {
        var text = Normalize(expression);
        for (var i = 0; i < text.Length;)
        {
            var c = text[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            if (char.IsDigit(c) || c == '.')
            {
                var start = i;
                while (i < text.Length && (char.IsDigit(text[i]) || text[i] is '.' or ',')) i++;
                yield return Literal(text[start..i], comma);
                continue;
            }
            if (char.IsLetter(c))
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
        return ThousandsSpace().Replace(text, "$1");
    }

    [GeneratedRegex(@"[{]([\p{L}][\p{L}\p{N}_]{0,31})[}]")]
    private static partial Regex PlaceholderPattern();

    [GeneratedRegex(@"^[a-z][a-z0-9_]{0,31}$")]
    private static partial Regex IdPattern();

    [GeneratedRegex(@"(?<![\w.,])\d{1,3}(?:[\u202F\u00A0 ]\d{3})+(?:[.,]\d+)?|(?<![\w.,])\d+(?:[.,]\d+)*")]
    private static partial Regex NumberPattern();

    [GeneratedRegex(@"(\d) (?=\d{3}(?!\d))")]
    private static partial Regex ThousandsSpace();

    [GeneratedRegex(@"\b\d{1,2}/\d{1,2}(?:/\d{2,4})?\b|\b\d{4}-\d{2}-\d{2}\b|\b\d{1,2} ?[:hH] ?\d{2}\b")]
    private static partial Regex DatePattern();

    [GeneratedRegex(@"^\d{1,3}(?:[.,]\d{3})+$")]
    private static partial Regex Grouped();

    [GeneratedRegex(@"[A-Za-zÀ-ÖØ-öø-ÿ]+(?:-[A-Za-zÀ-ÖØ-öø-ÿ]+)*")]
    private static partial Regex WordPattern();
}
