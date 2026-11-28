using System.Text;
using System.Diagnostics;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;

namespace Nomi;

public sealed partial class MainWindow
{
    private void OpenNomi(string? text = null)
    {
        CloseSheet();
        Show("nomi");
        if (text is { Length: > 0 }) _ = SendNomi(text);
        else DispatcherQueue.TryEnqueue(() => nomiBox?.Focus(FocusState.Programmatic));
    }

    private void ToggleDeepLevel()
    {
        nomiLevel = nomiLevel == Levels.Deep ? Levels.Auto : Levels.Deep;
        if (view == "nomi") BuildNomi();
        ShowToast($"{T("nomiLevel")} : {LevelName(nomiLevel)}", null, null);
    }

    private async Task SendNomi(string text, ChatEntry? asked = null)
    {
        var question = text.Trim();
        if (question.Length == 0 || nomiRun is not null) return;
        if (view != "nomi") Show("nomi");
        if (!inference.Ready && !inference.HasModel) { BuildNomi(); return; }
        var thread = openThread ?? new ChatThread { Title = ChatThread.TitleFrom(question) };
        openThread = thread;
        var documents = asked is null ? nomiDocuments.ToList() : [];
        if (asked is null)
        {
            asked = new ChatEntry { Role = "user", Text = question[..Math.Min(question.Length, Conversation.QuestionLimit)], Time = Now, Documents = [.. documents.Select(document => document.Name)] };
            threads.Add(thread, asked);
            nomiDocuments.Clear();
            if (nomiBox is not null) nomiBox.Text = "";
        }
        var route = Router.Decide(question, asked.Documents.Length, nomiLevel);
        var entry = new ChatEntry { Role = "nomi", Time = Now, Level = route.Level, Reason = route.Reason, Steps = route.Steps, Documents = asked.Documents, Model = inference.Model };
        using var controller = new CancellationTokenSource();
        nomiRun = controller;
        liveEntry = entry;
        liveStep = 0;
        thread.Items.Add(entry);
        BuildNomi();
        AnnounceNomi(T("chatThinking"));
        var clock = Stopwatch.StartNew();
        try
        {
            if (!inference.Ready) await PrepareModel();
            if (!inference.Ready) throw new InferenceException("local-engine-error");
            entry.Model = inference.Model;
            var context = ChatContextNow();
            var earlier = thread.Items.Where(item => item != entry && item != asked).ToArray();
            var chatting = route.Action == "chat";
            var prompt = chatting ? Conversation.Prompt(context, earlier, question) : Conversation.Single(context, question);
            var level = Escalation.Level(route, question, asked.Documents.Length, nomiLevel);
            string answer;
            List<ChatAction> actions;
            while (true)
            {
                var engine = await Engine(level, controller.Token);
                entry.Model = engine == fast ? fast.Model : inference.Model;
                entry.Level = level;
                var asking = prompt;
                IReadOnlyList<CalcResult> results = [];
                if (route.Calculates)
                {
                    liveStep = 1;
                    BuildNomi();
                    var comma = language == "fr";
                    var (planned, template, schemaPlan) = await PlanAsync(engine, Conversation.Single(context, question), route.Action, documents, comma, controller.Token);
                    var known = Calc.Numbers(question).Concat(Calc.Numbers(context.Text)).Concat(documents.SelectMany(document => Calc.Numbers(document.Text)));
                    // Strate 1 — voie catalogue : classification + slots, invariants et
                    // vérification inverse exacts. C'est la seule voie « verified ».
                    CalcEngine.SchemaOutcome? schemaOutcome = null;
                    if (schemaPlan is not null)
                        schemaOutcome = Calc.RunSchema(schemaPlan, known);
                    if (schemaOutcome is not null)
                    {
                        entry.Verified = true;
                        var outputs = schemaOutcome.Values.OrderBy(pair => pair.Key)
                            .Select(pair => $"{pair.Key} = {pair.Value.DisplayFrench()}").ToArray();
                        entry.Calculation = [$"schema = {schemaOutcome.Schema}", .. outputs];
                        entry.Certificate = System.Text.Json.JsonSerializer.Serialize(Calc.CertifySchema(schemaOutcome));
                        var composedSchema = template;
                        var missingSchemaOutput = false;
                        foreach (var (key, value) in schemaOutcome.Values)
                            composedSchema = composedSchema.Replace("{" + key + "}", value.DisplayFrench());
                        if (composedSchema.Contains('{')) missingSchemaOutput = true;
                        if (!missingSchemaOutput && composedSchema.Length > 0
                            && ResponsePolicy.Apply(composedSchema, "summary") is { Accepted: true } schemaText)
                        {
                            answer = schemaText.Text;
                            actions = [];
                            break;
                        }
                    }
                    // Strate 5 — voie expressions libres : exactitude prouvée, pertinence
                    // non garantie — certificat « interpreted », jamais « verified ».
                    results = Calc.Ground(planned, known, comma);
                    entry.Verified = results.Count > 0 && results.All(result => result.Verified);
                    entry.Calculation = entry.Verified == true ? [.. Calc.Posed(results, CultureInfo.CurrentCulture)] : [];
                    if (entry.Verified == true)
                    {
                        var certificate = Calc.Certify(results, grounded: true);
                        entry.Certificate = System.Text.Json.JsonSerializer.Serialize(certificate);
                    }
                    // One generation: the model's sentence with the engine's figures, when it holds no other figure.
                    if (entry.Verified == true && Calc.Compose(template, results, CultureInfo.CurrentCulture) is { } composed
                        && ResponsePolicy.Apply(composed, "summary") is { Accepted: true } checkedText)
                    {
                        var figures = results.Select(result => result.Value).Concat(Calc.Numbers(question)).Concat(Calc.Numbers(context.Text)).ToArray();
                        Calc.Mask(checkedText.Text, figures, out var invented);
                        if (invented == 0)
                        {
                            answer = checkedText.Text;
                            actions = [];
                            break;
                        }
                    }
                    if (entry.Verified != true && nomiLevel == Levels.Auto && entry.EscalatedFrom is null && Escalation.Next(level) is { } higher)
                    {
                        entry.EscalatedFrom = level;
                        level = higher;
                        continue;
                    }
                    var shown = results.Where(result => result.Verified)
                        .Select(result => $"{result.Id} = {Calc.Format(result.Value, CultureInfo.CurrentCulture)} {result.Unit}".Trim());
                    if (entry.Verified == true) asking += "\n\n" + string.Format(CultureInfo.CurrentCulture, T("calcResults"), string.Join("; ", shown));
                }
                liveStep = Math.Max(0, route.Steps.Length - 2);
                BuildNomi();
                var result = await engine.GenerateNomiAsync(asking, route.Action, language, "summary", controller.Token,
                    documents: documents.Count > 0 ? documents : null, reasoning: level == Levels.Deep,
                    instruction: $"{(chatting ? Conversation.Instruction() : "")} {AdaptiveInstruction()}".Trim(), allowPartial: true);
                (answer, actions) = result.Accepted
                    ? chatting ? Conversation.Read(result.Text, context, board.Tasks) : (result.Text.Trim(), new List<ChatAction>())
                    : ("", []);
                if (answer.Length == 0 && actions.Count == 0) throw new InferenceException("chatError");
                if (result.Changes.Contains(ResponsePolicy.Truncated)) answer += "\n\n" + T("nomiCut");
                if (!route.Calculates) break;
                var allowed = results.Where(result => result.Verified).Select(result => result.Value)
                    .Concat(Calc.Numbers(question)).Concat(Calc.Numbers(context.Text)).ToArray();
                var masked = Calc.Mask(answer, allowed, out var hidden);
                if (hidden == 0) break;
                entry.Verified = false;
                if (nomiLevel == Levels.Auto && entry.EscalatedFrom is null && Escalation.Next(level) is { } next)
                {
                    entry.EscalatedFrom = level;
                    level = next;
                    continue;
                }
                answer = masked + "\n\n" + T("calcBlocked");
                break;
            }
            entry.Level = level;
            entry.Text = answer;
            entry.Actions = actions;
            AnnounceNomi(string.Format(CultureInfo.CurrentCulture, T("chatReplied"), answer));
        }
        catch (OperationCanceledException)
        {
            entry.Stopped = true;
            entry.Text = entry.Text.Length > 0 ? entry.Text : T("nomiStoppedShort");
        }
        catch (Exception error)
        {
            StartupDiagnostics.Write($"Nomi answer failed: {error.GetType().Name} {error.Message}");
            entry.Failed = true;
            entry.Text = T("chatError");
            AnnounceNomi(T("chatError"));
        }
        finally
        {
            entry.Seconds = Math.Round(clock.Elapsed.TotalSeconds, 1);
            routingLog.Add(new RoutingEntry(Now, route.Primitive, entry.Level, entry.Reason, Escalation.Score(question, asked.Documents.Length),
                entry.EscalatedFrom, entry.Verified, entry.Model, entry.Seconds, asked.Documents.Length));
            thread.Items.Remove(entry);
            liveEntry = null;
            nomiRun = null;
            threads.Add(thread, entry);
            if (view == "nomi") BuildNomi();
        }
    }

    private readonly RoutingLog routingLog = new();

    private void AnnounceNomi(string message)
    {
        if (FrameworkElementAutomationPeer.FromElement(NomiPanel) is { } peer || (peer = FrameworkElementAutomationPeer.CreatePeerForElement(NomiPanel)) is not null)
            peer.RaiseNotificationEvent(AutomationNotificationKind.ActionCompleted, AutomationNotificationProcessing.ImportantMostRecent, message, "NomiStatus");
    }

    // The plan is internal JSON, never shown, so it skips the answer policy; a bad plan only leaves the figures unverified.
    private async Task<(IReadOnlyList<CalcResult> Results, string Answer, Calc.SchemaPlan? Schema)> PlanAsync(NomiInferenceClient engine, string prompt, string action,
        IReadOnlyList<AttachedDocument> documents, bool decimalComma, CancellationToken token)
    {
        var plan = new StringBuilder();
        try
        {
            var truncated = await engine.GenerateAsync(prompt, action, language, "summary", text =>
            {
                plan.Append(text);
                if (plan.Length > 8000) throw new CalcException("plan");
            }, token, T("calcPlanInstruction"), documents.Count > 0 ? documents : null);
            var text = plan.ToString();
            return truncated ? ([], "", null) : (Calc.Run(Calc.ParsePlan(text), decimalComma), Calc.PlanAnswer(text), Calc.ParseSchemaPlan(text));
        }
        catch (Exception error) when (error is CalcException || error is InferenceException { Message: not "busy" }) { return ([], "", null); }
    }

    // Rapide answers with Gemma 4 when it is installed; otherwise, or if it fails to load, Qwen3 8B answers.
    private async Task<NomiInferenceClient> Engine(string level, CancellationToken token)
    {
        if (level != Levels.Fast || !fast.HasModel) return inference;
        if (fast.Ready) return fast;
        try
        {
            await fast.PrepareAsync(false, null, token);
            return fast;
        }
        catch (Exception error) when (error is InferenceException or IOException or InvalidDataException or LLama.Exceptions.RuntimeError)
        {
            return inference;
        }
    }
}
