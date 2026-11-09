using System.Globalization;
using System.Text;

namespace Nomi;

public static class Levels
{
    public const string Auto = "auto";
    public const string Fast = "fast";
    public const string Applied = "applied";
    public const string Deep = "deep";
    public static readonly string[] All = [Auto, Fast, Applied, Deep];
}

public static class Steps
{
    public const string Understood = "understood";
    public const string Read = "read";
    public const string Calculate = "calculate";
    public const string Check = "check";
    public const string Answer = "answer";
}

public sealed record Route(string Action, string Primitive, string Level, string[] Steps, string Reason, bool Reads, bool Calculates);

public static class Router
{
    private static readonly string[] DeepWords = ["pourquoi", "optimis", "compare", "démontre", "analyse", "why", "optimi", "prove", "trade-off"];
    private static readonly string[] CalcWords = ["combien", "total", "reste", "remise", "budget", "moyenne", "pourcent", "how much", "how many", "how long", "left", "discount", "average", "percent", "%", "€", "$"];
    private static readonly string[] ConvertWords = ["convert", "en heures", "en minutes", "en euros", "in hours", "in minutes"];
    private static readonly string[] PlanWords = ["commence", "journée", "priorit", "planifi", "organis", "start", "my day", "priority", "plan"];
    private static readonly string[] WriteWords = ["mail", "message", "rédige", "réponds", "brouillon", "write", "draft", "reply"];
    private static readonly string[] LearnWords = ["explique", "c'est quoi", "qu'est-ce", "apprendre", "apprends-moi", "comment fonctionne", "explain", "what is", "learn", "teach", "how does"];

    public static Route Decide(string text, int documents, string chosen)
    {
        var t = Normalize(text);
        var reads = documents > 0;
        var digits = t.Count(char.IsDigit) >= 2;
        var calculates = reads && (Has(t, CalcWords) || digits) || Has(t, CalcWords) || Has(t, ConvertWords);
        var primitive = Has(t, ConvertWords) ? "convert"
            : calculates ? "verify"
            : Has(t, PlanWords) ? "plan"
            : Has(t, WriteWords) ? "communicate"
            : Has(t, LearnWords) ? "learn"
            : reads ? "understand" : "talk";
        var deep = Has(t, DeepWords) || t.Length > 700;
        var level = Levels.All.Contains(chosen) && chosen != Levels.Auto ? chosen
            : deep ? Levels.Deep
            : reads || calculates && digits ? Levels.Applied
            : Levels.Fast;
        var reason = chosen != Levels.Auto && Levels.All.Contains(chosen) ? "routeChosen"
            : deep ? "routeDeep"
            : reads ? "routeDocument"
            : calculates ? "routeCalc"
            : primitive == "plan" ? "routePlan" : "routeTalk";
        var steps = new List<string> { reads ? Steps.Read : Steps.Understood };
        if (calculates) steps.AddRange([Steps.Calculate, Steps.Check]);
        steps.Add(Steps.Answer);
        var action = primitive is "plan" or "talk" ? "chat" : primitive;
        return new Route(action, primitive, level, [.. steps], reason, reads, calculates);
    }

    private static bool Has(string text, string[] words) => words.Any(text.Contains);

    private static string Normalize(string text) =>
        text.ToLower(CultureInfo.InvariantCulture).Replace('’', '\'').Normalize(NormalizationForm.FormC);
}
