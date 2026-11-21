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
    private static readonly string[] DeepWords = ["pourquoi", "optimis*", "compar*", "démontr*", "analys*", "why", "optimi*", "prove", "trade-off"];
    private static readonly string[] CalcWords = ["combien", "calcul*", "additionn*", "multipli*", "moyenne", "pourcent*", "how much", "how many", "how long", "average", "percent*"];
    private static readonly string[] AmountWords = ["total", "totaux", "reste", "restant", "remise", "budget", "tva", "left", "remaining", "discount", "vat", "%", "€", "$"];
    private static readonly string[] ConvertVerbs = ["convert*"];
    private static readonly string[] UnitWords = ["en heures", "en minutes", "en euros", "in hours", "in minutes"];
    private static readonly string[] SheetOutWords = ["une fiche", "en fiche", "des fiches", "fiche de révision", "fiche de synthèse", "a revision sheet", "a study sheet", "a cheat sheet", "into a sheet"];
    private static readonly string[] PlanWords = ["commenc*", "journée", "priorit*", "planifi*", "organis*", "start", "my day", "plan", "plans", "planning"];
    private static readonly string[] WriteWords = ["mail", "mails", "email", "e-mail", "message", "rédige*", "réponds", "brouillon", "write", "draft", "reply"];
    private static readonly string[] LearnWords = ["expliqu*", "c'est quoi", "qu'est-ce", "apprendre", "apprends-moi", "comment fonctionne", "explain*", "what is", "learn", "teach", "how does"];
    private static readonly string[] HardReviewWords = ["relis*", "relecture", "relire", "audite*", "lint*", "revue de code", "passe le code", "lis ce code", "lis ce fichier", "check this code", "check my code", "proofread*"];
    private static readonly string[] SoftReviewWords = ["review", "revue"];
    private static readonly string[] SheetWords = ["fiche*", "révis*", "revis*", "revise*", "revision", "revision sheet", "study sheet", "cheat sheet"];
    private static readonly string[] CreateWords = ["fais", "fait", "faites", "crée*", "génère*", "produis*", "make", "create*", "generate*", "produce*", "build"];

    public static Route Decide(string text, int documents, string chosen)
    {
        var t = Normalize(text);
        var reads = documents > 0;
        var count = t.Count(char.IsDigit);
        var digits = count >= 2;
        var amount = Has(t, AmountWords) && (count > 0 || reads) && !Has(t, WriteWords);
        var primitive = Has(t, ConvertVerbs) && !Has(t, SheetOutWords) ? "convert"
            : Has(t, SheetWords) && Has(t, HardReviewWords) ? "review"
            : Has(t, SheetOutWords) ? "sheet"
            : Has(t, SheetWords) && Has(t, SoftReviewWords) && !Has(t, CreateWords) ? "review"
            : Has(t, SheetWords) ? "sheet"
            : Has(t, UnitWords) ? "convert"
            : Has(t, CalcWords) ? "verify"
            : Has(t, PlanWords) ? "plan"
            : Has(t, WriteWords) ? "communicate"
            : Has(t, HardReviewWords) || Has(t, SoftReviewWords) ? "review"
            : amount || reads && digits ? "verify"
            : Has(t, LearnWords) ? "learn"
            : reads ? "understand" : "talk";
        var calculates = primitive is "verify" or "convert";
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

    private static bool Has(string text, string[] words) => words.Any(word => Found(text, word));

    private static bool Found(string text, string word)
    {
        var stem = word.EndsWith('*');
        var key = stem ? word[..^1] : word;
        if (!char.IsLetter(key[0])) return text.Contains(key, StringComparison.Ordinal);
        for (var i = text.IndexOf(key, StringComparison.Ordinal); i >= 0; i = text.IndexOf(key, i + 1, StringComparison.Ordinal))
        {
            var end = i + key.Length;
            if ((i == 0 || !char.IsLetter(text[i - 1])) && (stem || end == text.Length || !char.IsLetter(text[end]))) return true;
        }
        return false;
    }

    private static string Normalize(string text) =>
        text.ToLower(CultureInfo.InvariantCulture).Replace('’', '\'').Normalize(NormalizationForm.FormC);
}
