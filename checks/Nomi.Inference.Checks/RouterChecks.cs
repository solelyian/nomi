using Nomi;

static class RouterChecks
{
    public static void Run()
    {
        var count = 0;
        void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("Router: " + message);
            count++;
        }
        var plan = Router.Decide("Par quoi je commence ce matin ?", 0, Levels.Auto);
        Check(plan.Primitive == "plan" && plan.Action == "chat" && plan.Level == Levels.Fast && plan.Reason == "routePlan", "planning stays fast and keeps task actions");
        Check(plan.Steps.SequenceEqual([Steps.Understood, Steps.Answer]), "planning has no calculation steps");
        var time = Router.Decide("Combien de temps me reste-t-il ?", 0, Levels.Auto);
        Check(time.Calculates && time.Primitive == "verify" && time.Level == Levels.Fast, "time left is calculated without escalation");
        Check(time.Steps.SequenceEqual([Steps.Understood, Steps.Calculate, Steps.Check, Steps.Answer]), "calculation is checked");
        var doc = Router.Decide("Calcule le total de ce devis", 1, Levels.Auto);
        Check(doc.Reads && doc.Level == Levels.Applied && doc.Steps[0] == Steps.Read && doc.Reason == "routeDocument", "documents are read first then applied");
        var deep = Router.Decide("Pourquoi cet ordre est-il le meilleur ?", 0, Levels.Auto);
        Check(deep.Level == Levels.Deep && deep.Reason == "routeDeep", "hard questions reason");
        var chosen = Router.Decide("Bonjour", 0, Levels.Deep);
        Check(chosen.Level == Levels.Deep && chosen.Reason == "routeChosen" && chosen.Primitive == "talk", "a chosen level wins");
        Check(Router.Decide("Bonjour", 0, "bogus").Level == Levels.Fast, "unknown levels fall back to auto");
        Check(Router.Decide("Rédige un mail à Claire", 0, Levels.Auto).Action == "communicate", "writing uses Communicate");
        Check(Router.Decide("Convert 90 minutes in hours", 0, Levels.Auto).Action == "convert", "English conversion");
        foreach (var plain in new[] { "Je veux rester concentré, comment faire ?", "I felt left out today", "Écris un mail à Paul pour le budget", "Can you describe the panel on the left?" })
            Check(!Router.Decide(plain, 0, Levels.Auto).Calculates, $"« {plain} » is not a calculation");
        Check(Router.Decide("Quel est 15 % de 80 ?", 0, Levels.Auto).Calculates && Router.Decide("80 €, remise de 15 %", 0, Levels.Auto).Calculates, "amounts with figures are calculated");
        Check(Router.Decide("Restart my laptop", 0, Levels.Auto).Primitive == "talk", "keywords match whole words");

        var nl = ((char)10).ToString();
        Check(ChatThread.TitleFrom("  Devis Acme" + nl + "détails") == "Devis Acme", "title is the first line");
        Check(ChatThread.TitleFrom(new string('a', 80)).Length == 48, "long titles are cut");
        var folder = Path.Combine(Path.GetTempPath(), $"nomi-threads-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        var legacy = Path.Combine(folder, "conversation.json");
        var old = new ChatStore(legacy);
        old.Add(new ChatEntry { Role = "user", Text = "Ancienne question" });
        old.Add(new ChatEntry { Role = "nomi", Text = "Ancienne réponse" });
        var file = Path.Combine(folder, "conversations.json");
        var store = new ThreadStore(file, legacy);
        store.Load();
        Check(store.Threads.Count == 1 && store.Threads[0].Title == "Ancienne question" && !File.Exists(legacy), "the old conversation is migrated");
        var fresh = new ChatThread { Title = "Budget" };
        store.Add(fresh, new ChatEntry { Role = "user", Text = "Budget du séminaire", Time = DateTimeOffset.Now.AddMinutes(1) });
        var reloaded = new ThreadStore(file, legacy);
        reloaded.Load();
        Check(reloaded.Threads.Count == 2 && reloaded.Threads[0].Title == "Budget", "newest thread first");
        Check(reloaded.Search("séminaire").Single().Title == "Budget" && reloaded.Search("").Count() == 2, "search looks in titles and messages");
        Check(!OperatingSystem.IsWindows() || !File.ReadAllText(file).Contains("Budget"), "threads are encrypted on disk");
        reloaded.Delete(reloaded.Threads[0]);
        Check(reloaded.Threads.Count == 1 && File.Exists(file), "deleting one keeps the others");
        reloaded.Clear();
        Check(!File.Exists(file), "clear removes the file");
        Directory.Delete(folder, true);
        Console.WriteLine($"{count} C# router and thread checks passed.");
    }
}
