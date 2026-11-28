using System.Text.Json;
using Nomi.CalcEngine;

// Harnais de conformité : exécute calc-spec/vectors.json contre le noyau Windows
// et vérifie le recoupement noyau/vérificateur sur chaque vecteur.
var corpus = JsonDocument.Parse(File.ReadAllText(args.Length > 0 ? args[0] : "../../calc-spec/vectors.json"));
var vectors = corpus.RootElement.GetProperty("vectors");
var kernel = new Kernel();
int pass = 0, fail = 0, disagree = 0;
var failures = new List<string>();
foreach (var vector in vectors.EnumerateArray())
{
    var id = vector.GetProperty("id").GetString()!;
    var input = vector.GetProperty("input").GetString()!;
    var expect = vector.GetProperty("expect");
    var status = expect.GetProperty("status").GetString()!;
    var names = new Dictionary<string, Rational>();
    if (vector.TryGetProperty("names", out var namesElement))
        foreach (var pair in namesElement.EnumerateObject())
            if (Rational.ParseLiteral(pair.Value.GetString()!) is { } parsed) names[pair.Name] = parsed;

    // Recoupement noyau/vérificateur sur chaque vecteur.
    var cross = CrossCheck.Compare(input, names);
    if (!cross.Consistent) { disagree++; failures.Add($"CROSS {id}: {cross.Detail}"); }

    try
    {
        var measured = kernel.EvaluateDimensional(input, names.Count > 0
            ? names.ToDictionary(pair => pair.Key, pair => Measured.Plain(pair.Value))
            : null);
        var result = measured.Value;
        // Vecteurs de dimension : la dimension attendue est comparée exactement.
        if (expect.TryGetProperty("dimension", out var wantDim))
        {
            var dimOk = wantDim.ValueKind == JsonValueKind.String
                ? measured.Dim.Kind == DimKind.None
                : measured.Dim.Kind == DimKind.Currency && measured.Dim.Code == wantDim.GetProperty("currency").GetString();
            if (!dimOk) { fail++; failures.Add($"{id}: dimension {measured.Dim.Kind}/{measured.Dim.Code} != {wantDim.GetRawText()}"); continue; }
        }
        if (status == "error")
        {
            fail++;
            failures.Add($"{id}: expected error {expect.GetProperty("error").GetString()}, got {result}");
            continue;
        }
        var rational = result.ToString();
        var ok = true;
        if (expect.TryGetProperty("rational", out var wantRational) && rational != wantRational.GetString())
        {
            failures.Add($"{id}: rational {rational} != {wantRational.GetString()}");
            ok = false;
        }
        if (expect.TryGetProperty("display_fr", out var wantDisplay))
        {
            var places = expect.TryGetProperty("display_places", out var p) ? p.GetInt32() : 4;
            var display = result.DisplayFrench(places);
            if (display != wantDisplay.GetString())
            {
                failures.Add($"{id}: display '{display}' != '{wantDisplay.GetString()}'");
                ok = false;
            }
        }
        if (ok) pass++; else fail++;
    }
    catch (CalculationException error)
    {
        var wanted = status == "error" && expect.TryGetProperty("error", out var we) ? we.GetString() : "ok";
        if (status == "error" && error.Code == wanted) pass++;
        else { fail++; failures.Add($"{id}: error {error.Code} (expected {wanted})"); }
    }
}
Console.WriteLine($"CORPUS {pass}/{pass + fail} pass, cross-check disagreements: {disagree}");
foreach (var failure in failures.Take(30)) Console.WriteLine("  " + failure);
return fail > 0 || disagree > 0 ? 1 : 0;
