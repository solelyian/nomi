using System.Text.Json;
using Nomi;

static class EvalChecks
{
    private sealed record Case(string Text, int Documents, string Primitive, string Level, string? Chosen, bool Reads, bool Calculates);

    public static void Run()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "checks", "evals", "primitives.json"))) folder = folder.Parent;
        if (folder is null) throw new InvalidOperationException("Evals: checks/evals/primitives.json not found");
        var cases = JsonSerializer.Deserialize<Case[]>(File.ReadAllText(Path.Combine(folder.FullName, "checks", "evals", "primitives.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        var failures = new List<string>();
        foreach (var item in cases)
        {
            var chosen = item.Chosen ?? Levels.Auto;
            var route = Router.Decide(item.Text, item.Documents, chosen);
            var level = Escalation.Level(route, item.Text, item.Documents, chosen);
            if (route.Primitive != item.Primitive || level != item.Level || route.Reads != item.Reads || route.Calculates != item.Calculates)
                failures.Add($"« {item.Text} » → {route.Primitive}/{level}/reads={route.Reads}/calc={route.Calculates}, expected {item.Primitive}/{item.Level}/reads={item.Reads}/calc={item.Calculates}");
        }
        var primitives = cases.Select(item => item.Primitive).Distinct().Count();
        var levels = cases.Select(item => item.Level).Distinct().Count();
        if (primitives < 6 || levels < 3) failures.Add($"the eval set covers {primitives} primitives and {levels} levels");
        foreach (var language in new[] { "fr", "en" })
        {
            using var strings = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder.FullName, "content", $"{language}.json")));
            var text = strings.RootElement.ToString();
            foreach (var key in new[] { "calcPlanInstruction", "calcResults", "calcBlocked", "routePlan", "routeDeep", "routeDocument", "routeChosen" })
                if (!text.Contains($"\"{key}\"", StringComparison.Ordinal)) failures.Add($"{language}.json lacks {key}");
            if (language == "fr" && System.Text.RegularExpressions.Regex.IsMatch(text, @"""calc[A-Za-z]+"":\s*""[^""]*\b(tu|ton|ta|tes|toi)\b")) failures.Add("calculation strings must use vous");
        }
        if (failures.Count > 0) throw new InvalidOperationException("Evals:\n" + string.Join("\n", failures));
        Console.WriteLine($"{cases.Length} routing evals over {primitives} primitives and {levels} levels passed, with the shared tone check.");
    }
}
