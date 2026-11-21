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

        Check(Router.Decide("Convertis les durées de cette fiche en heures", 0, Levels.Auto).Primitive == "convert", "converting a sheet's durations");
        Check(Router.Decide("Fiche sur les convertisseurs", 0, Levels.Auto).Primitive == "sheet", "convertisseurs is a topic, not a command");
        Check(Router.Decide("Convert this lecture into a revision sheet", 0, Levels.Auto).Primitive == "sheet", "sheet named as conversion target");
        Check(Router.Decide("Combien font 90 minutes en heures ?", 0, Levels.Auto).Primitive == "convert", "unit question converts");
        Check(Router.Decide("Relis ma fiche", 0, Levels.Auto).Primitive == "review", "proofreading a sheet");
        Check(Router.Decide("Relis mes calculs", 0, Levels.Auto).Primitive == "verify", "checking calculations");
        Check(Router.Decide("Fais une relecture de ma fiche", 0, Levels.Auto).Primitive == "review", "asked proofread of a sheet");
        Check(Router.Decide("Make a revision sheet so I can review", 0, Levels.Auto).Primitive == "sheet", "sheet named by creator");
        Check(Router.Decide("Rédige un mail pour demander une fiche", 0, Levels.Auto).Primitive == "communicate", "mail about a sheet stays mail");
        Check(Router.Decide("Fiche de révision sur la relecture", 0, Levels.Auto).Primitive == "sheet", "relecture as topic stays sheet");
        Check(Router.Decide("Summarize this Excel sheet", 0, Levels.Auto).Primitive != "sheet", "Excel sheets are not revision sheets");
        Check(Router.Decide("Mon cours sur les réseaux : fais une fiche", 0, Levels.Auto).Primitive == "sheet", "intent after a subject phrase");
        Check(Router.Decide("Sous-total sur la facture 300", 0, Levels.Auto).Primitive == "verify", "total named before its subject");
        Check(Router.Decide("Convertis ce message en fiche de révision", 0, Levels.Auto).Primitive == "sheet", "message as conversion source");
        Check(Router.Decide("Relis ce courriel", 0, Levels.Auto).Primitive == "review", "mail as proofread source");
        Check(Router.Decide("Convertis ce cours en une courte fiche de révision", 0, Levels.Auto).Primitive == "sheet", "adjective inside the sheet target");
        Check(Router.Decide("Convert this lecture to a cheat sheet", 0, Levels.Auto).Primitive == "sheet", "cheat sheet target");
        Check(Router.Decide("Convertis ce cours en des fiches", 0, Levels.Auto).Primitive == "sheet", "plural sheet target");
        Check(Router.Decide("Explain how to read sheet music", 0, Levels.Auto).Primitive == "learn", "sheet music is not a revision sheet");
        Check(Router.Decide("Explique-moi le chapitre sur la guerre de 1914", 1, Levels.Auto).Primitive == "learn", "a year in a topic is not calculation evidence");
        Check(Router.Decide("Convert these notes to cheat sheet format", 0, Levels.Auto).Primitive == "sheet", "cheat sheet without an article");
        Check(Router.Decide("Explain how to organize an Excel workbook into sheets by month", 0, Levels.Auto).Primitive == "learn", "spreadsheet tabs are not revision sheets");
        Check(Router.Decide("What is 15% of 80?", 0, Levels.Auto).Primitive == "verify", "an arithmetic question is calculated");
        Check(Router.Decide("Make a revision sheet so I can learn the material", 0, Levels.Auto).Primitive == "sheet", "a purpose clause is not the request");
        Check(Router.Decide("Convert lecture notes into sheets for studying", 0, Levels.Auto).Primitive == "sheet", "bare plural is a target outside spreadsheets");
        Check(Router.Decide("Explain how to add a row to an Excel sheet", 0, Levels.Auto).Primitive == "learn", "an Excel sheet is a spreadsheet tab");

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
