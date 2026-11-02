using Nomi;

static class ConversationChecks
{
    public static void Run()
    {
        var count = 0;
        void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("Conversation: " + message);
            count++;
        }
        var nl = ((char)10).ToString();
        var now = new DateTimeOffset(2026, 11, 2, 10, 0, 0, TimeSpan.FromHours(1));

        Check(FirstStep.Kind("Répondre à Marc", "") == DraftKinds.Mail, "French mail");
        Check(FirstStep.Kind("Reply to the client", "") == DraftKinds.Mail, "English mail");
        Check(FirstStep.Kind("Prévenir l'équipe sur Slack", "") == DraftKinds.Message, "message");
        Check(FirstStep.Kind("Rédiger le rapport trimestriel", "") == DraftKinds.Outline, "outline");
        Check(FirstStep.Kind("Préparer l'atelier client", "") == DraftKinds.Plan, "plan");
        Check(FirstStep.Kind("Ranger le bureau", "") == DraftKinds.Checklist, "checklist fallback");
        Check(FirstStep.Kind("Planche de tests", "") == DraftKinds.Checklist, "whole words only");
        Check(FirstStep.Kind("Appeler Marc", "puis lui envoyer un mail") == DraftKinds.Mail, "note is read");
        var task = new WorkTask { Title = "Rapport", Note = new string('x', 3000), Due = now.AddHours(-2) };
        task.Steps.Add(new WorkStep { Title = "Plan", Done = true });
        task.Steps.Add(new WorkStep { Title = "Introduction" });
        Check(FirstStep.Target(task) == "Introduction", "first open step is the target");
        var prompt = FirstStep.Prompt(task, "Acme", now);
        Check(prompt.Contains("First step to write: Introduction") && prompt.Contains("already passed") && prompt.Contains("Space: Acme"), "prompt facts");
        Check(prompt.Length < 2000, "note is capped");
        Check(DraftKinds.All.All(kind => FirstStep.Instruction(kind).Contains("square brackets")), "every draft forbids invented facts");

        var doing = new WorkTask { Title = "Appeler Marc", Column = Columns.Doing, Estimate = 20, Space = "acme" };
        var late = new WorkTask { Title = "Devis Acme", Due = now.AddDays(-1), Order = 5 };
        var later = new WorkTask { Title = "Lire le contrat", Due = now.AddDays(3), Order = 1 };
        var done = new WorkTask { Title = "Fini", Column = Columns.Done };
        WorkTask[] tasks = [later, done, late, doing];
        var context = Conversation.Context(tasks, now, Day.Default, id => id == "acme" ? "Acme" : null);
        Check(context.Count == 3 && context.Refs["T1"] == doing.Id && context.Refs["T2"] == late.Id && context.Refs["T3"] == later.Id, "doing, overdue, then later");
        Check(context.Text.Contains("(overdue)") && context.Text.Contains("space Acme") && !context.Text.Contains("Fini"), "context lines");
        Check(context.Text.Contains("Free working time left today: 420 min"), "free time from the profile day");
        var many = Enumerable.Range(0, 30).Select(i => new WorkTask { Title = $"Tâche {i}", Order = i }).ToArray();
        Check(Conversation.Context(many, now, Day.Default, _ => null).Count == Conversation.MaxTasks, "task list is capped");
        Check(Conversation.Context([], now, Day.Default, _ => null).Text.Contains("no open tasks"), "empty board");

        var history = Enumerable.Range(0, 40).Select(i => new ChatMessage
        {
            Role = i % 2 == 0 ? "user" : "nomi",
            Text = $"Sujet {i} " + new string('a', 600)
        }).ToList();
        var full = Conversation.Prompt(context, history, "Et maintenant ?");
        Check(full.Length <= Conversation.Budget, "prompt fits the model");
        Check(full.Contains("Sujet 39") && full.EndsWith("User: Et maintenant ?"), "newest messages are kept");
        Check(full.Contains("Earlier topics, summarised: Sujet 0"), "older messages are summarised");
        var shortPrompt = Conversation.Prompt(context, [new ChatMessage { Text = "Bonjour" }], "Ça va ?");
        Check(shortPrompt.Contains("User: Bonjour") && !shortPrompt.Contains("Earlier topics"), "short conversations are complete");
        Check(Conversation.Prompt(context, [], new string('q', 5000)).Length <= Conversation.Budget, "long questions are cut");

        var (text, actions) = Conversation.Read(string.Join(nl,
            "Commencez par le devis, il est en retard.",
            "",
            "Action: focus | T2",
            "- Action: postpone | T3",
            "Action: create | relancer Marc demain 10h",
            "Action: split | T9",
            "Action: delete | T1"), context, tasks);
        Check(text == "Commencez par le devis, il est en retard.", "action lines are removed from the text");
        Check(actions.Count == 3 && actions[0] is { Kind: ChatActions.Focus } && actions[0].TaskId == late.Id, "focus action");
        Check(actions[1].Kind == ChatActions.Postpone && actions[1].Title == "Lire le contrat", "postpone action keeps the title");
        Check(actions[2].Kind == ChatActions.Create && actions[2].Title == "relancer Marc demain 10h", "create action");
        var (_, rejected) = Conversation.Read(string.Join(nl, "Action: split | T9", "Action: create | T1", "Action: create | x", "Action: run | T1"), context, tasks);
        Check(rejected.Count == 0, "unknown tasks, references as titles, short titles and unknown kinds are rejected");
        doing.Column = Columns.Done;
        Check(Conversation.Read("Action: focus | T1", context, tasks).Actions.Count == 0, "finished tasks get no action");
        var (_, capped) = Conversation.Read(string.Join(nl, Enumerable.Range(0, 6).Select(i => $"Action: create | tâche numéro {i}")), context, tasks);
        Check(capped.Count == Conversation.MaxActions, "at most three actions");
        Check(Conversation.Read("Action: create | Appeler Marc" + nl + "Action: create | appeler marc", context, tasks).Actions.Count == 1, "duplicates are merged");

        var file = Path.Combine(Path.GetTempPath(), $"nomi-chat-{Guid.NewGuid():N}.json");
        var store = new ChatStore(file);
        for (var i = 0; i < ChatStore.Limit + 5; i++) store.Add(new ChatMessage { Text = $"m{i}" });
        var reloaded = new ChatStore(file);
        reloaded.Load();
        Check(reloaded.Items.Count == ChatStore.Limit && reloaded.Items[0].Text == "m5", "conversation is capped and reloaded");
        Check(!OperatingSystem.IsWindows() || !File.ReadAllText(file).Contains("m10"), "conversation is encrypted on disk");
        reloaded.Clear();
        Check(!File.Exists(file) && reloaded.Items.Count == 0, "new conversation deletes the file");
        Console.WriteLine($"{count} C# conversation and first-step checks passed.");
    }
}
