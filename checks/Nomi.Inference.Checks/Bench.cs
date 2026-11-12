using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Nomi;

// Times real answers with the downloaded model: dotnet run -- --bench <models directory>
static class Bench
{
    public static async Task RunAsync(string directory)
    {
        var instruction = Find(JsonDocument.Parse(File.ReadAllText("content/fr.json")).RootElement, "calcPlanInstruction")
            ?? throw new InvalidOperationException("calcPlanInstruction");
        await using var client = new LocalInferenceClient(new ModelStore(directory));
        var clock = Stopwatch.StartNew();
        await client.PrepareAsync(false, null, CancellationToken.None);
        Console.WriteLine($"load {clock.Elapsed.TotalSeconds:0.0} s");
        var context = "Tâches du jour : " + string.Join("; ", Enumerable.Range(1, 30).Select(i => $"tâche {i} pour le client Acme, 20 min"));
        string[] questions =
        [
            "80 €, remise de 15 %, puis TVA de 20 %. Quel est le prix final ?",
            "3 postes à 420 € puis TVA de 20 %, quel total ?",
            "J'ai 90 minutes et trois tâches de 20 minutes. Combien de temps me reste-t-il ?"
        ];
        var fr = CultureInfo.GetCultureInfo("fr-FR");
        foreach (var question in questions)
        {
            var text = new StringBuilder();
            double? first = null;
            clock.Restart();
            await client.GenerateAsync($"{context}\n\n{question}", "verify", "fr", "summary",
                token => { first ??= clock.Elapsed.TotalSeconds; text.Append(token); }, CancellationToken.None, instruction);
            var results = Calc.Ground(Calc.Run(Calc.ParsePlan(text.ToString()), true), Calc.Numbers(question).Concat(Calc.Numbers(context)));
            var answer = results.Count > 0 && results.All(result => result.Verified) ? Calc.Compose(Calc.PlanAnswer(text.ToString()), results, fr) : null;
            Console.WriteLine($"first token {first:0.0} s, total {clock.Elapsed.TotalSeconds:0.0} s, reused {client.PrefixTokens} tokens");
            Console.WriteLine($"  {question}\n  -> {answer ?? "(not verified) " + text}\n  {string.Join(" | ", Calc.Posed(results, fr))}");
        }
    }

    private static string? Find(JsonElement element, string key)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name == key && property.Value.ValueKind == JsonValueKind.String) return property.Value.GetString();
            if (Find(property.Value, key) is { } found) return found;
        }
        return null;
    }
}
