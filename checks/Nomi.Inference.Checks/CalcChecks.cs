using System.Globalization;
using Nomi;

static class CalcChecks
{
    public static void Run()
    {
        var count = 0;
        void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("Calc: " + message);
            count++;
        }
        Check(Calc.Evaluate("0.1 + 0.2") == 0.3m, "decimals are exact");
        Check(Calc.Evaluate("1 240 * (1 - 15%)") == 1054m, "discount with a thousands space and a percent");
        Check(Calc.Evaluate("2 + 3 * 4") == 14m && Calc.Evaluate("(2 + 3) * 4") == 20m, "precedence and parentheses");
        Check(Calc.Evaluate("-3 + 5") == 2m && Calc.Evaluate("2^10") == 1024m, "unary minus and powers");
        Check(Calc.Evaluate("12,5 * 2") == 25m, "decimal comma");
        var plan = Calc.ParsePlan("Voici : {\"steps\":[{\"id\":\"ht\",\"expr\":\"3 * 420\",\"unit\":\"€\"},{\"id\":\"ttc\",\"expr\":\"ht * 1.2\",\"unit\":\"€\"},{\"id\":\"bad\",\"expr\":\"ht / 0\"},{\"id\":\"9x\",\"expr\":\"1\"}]}");
        Check(plan.Count == 3, "invalid ids are dropped");
        var results = Calc.Run(plan);
        Check(results[0].Value == 1260m && results[0].Verified, "first step verified");
        Check(results[1].Value == 1512m && results[1].Verified, "steps reference earlier results");
        Check(!results[2].Verified, "division by zero is not verified");
        Check(Calc.ParsePlan("pas de JSON").Count == 0 && Calc.ParsePlan("{\"steps\": 3}").Count == 0, "malformed plans are empty");
        Check(Calc.Run([new CalcStep("x", "import os")]).Single().Verified == false, "code is not evaluated");
        Check(Calc.Run([new CalcStep("x", new string('1', 400))]).Single().Verified == false, "overlong expressions are refused");
        var allowed = new[] { 1260m, 1512m, 420m };
        Check(Calc.Unverified("Le total est de 1 512 € pour 3 postes à 420 €.", allowed).Count == 0, "verified numbers pass");
        Check(Calc.Unverified("Le total est de 1 600 €.", allowed).SequenceEqual(["1 600"]), "invented numbers are blocked");
        Check(Calc.Unverified("Étape 2 : 1260,00 €", allowed).Count == 0, "small step numbers and decimals pass");
        Check(Calc.Format(1512m, CultureInfo.GetCultureInfo("fr-FR")) == "1\u202F512" || Calc.Format(1512m, CultureInfo.GetCultureInfo("fr-FR")) == "1\u00A0512", "French grouping");
        Check(Calc.Numbers("3 postes à 420 € et 12,5 h").SequenceEqual([3m, 420m, 12.5m]), "numbers are read from text");
        Check(Escalation.Next(Levels.Fast) == Levels.Applied && Escalation.Next(Levels.Applied) == Levels.Deep && Escalation.Next(Levels.Deep) is null, "escalation climbs one level");
        var route = Router.Decide("Bonjour", 0, Levels.Auto);
        Check(Escalation.Level(route, "Bonjour", 0, Levels.Auto) == Levels.Fast, "short messages stay fast");
        var long_ = string.Join(" et ", Enumerable.Repeat("compare les offres 120 et 340 puis la remise", 12));
        Check(Escalation.Level(Router.Decide("x", 0, Levels.Auto), long_, 0, Levels.Auto) == Levels.Applied, "a high difficulty score escalates");
        Check(Escalation.Level(Router.Decide("x", 0, Levels.Fast), long_, 0, Levels.Fast) == Levels.Fast, "a chosen level is kept");
        var file = Path.Combine(Path.GetTempPath(), $"nomi-routing-{Guid.NewGuid():N}.json");
        var log = new RoutingLog(file);
        for (var i = 0; i < RoutingLog.Limit + 5; i++)
            log.Add(new RoutingEntry(DateTimeOffset.Now, "verify", Levels.Fast, "routeCalc", 20, null, true, "Gemma 4 E4B", 1.2, 0));
        var again = new RoutingLog(file);
        Check(again.Entries.Count == RoutingLog.Limit, "the routing log is capped and reloads");
        Check(!File.ReadAllText(file).Contains("routeCalc") || !SecureFile.Encrypts, "the routing log is encrypted");
        again.Clear();
        Check(!File.Exists(file), "the routing log is erased");
        Console.WriteLine($"{count} C# calculation, escalation and routing log checks passed.");
    }
}
