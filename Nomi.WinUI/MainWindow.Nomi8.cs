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
            var prompt = chatting ? Conversation.Prompt(context, earlier, question) : $"{context.Text}\n\n{question}";
            liveStep = Math.Max(0, route.Steps.Length - 2);
            BuildNomi();
            var result = await inference.GenerateNomiAsync(prompt, route.Action, language, "summary", controller.Token,
                documents: documents.Count > 0 ? documents : null, reasoning: route.Level == Levels.Deep,
                instruction: $"{(chatting ? Conversation.Instruction() : "")} {AdaptiveInstruction()}".Trim());
            var (answer, actions) = result.Accepted
                ? chatting ? Conversation.Read(result.Text, context, board.Tasks) : (result.Text.Trim(), new List<ChatAction>())
                : ("", []);
            if (answer.Length == 0 && actions.Count == 0) throw new InferenceException("chatError");
            entry.Text = answer;
            entry.Actions = actions;
            AnnounceNomi(string.Format(CultureInfo.CurrentCulture, T("chatReplied"), answer));
        }
        catch (OperationCanceledException)
        {
            entry.Stopped = true;
            entry.Text = entry.Text.Length > 0 ? entry.Text : T("nomiStoppedShort");
        }
        catch (Exception error) when (error is InferenceException or HttpRequestException or IOException or System.Text.Json.JsonException)
        {
            entry.Failed = true;
            entry.Text = T("chatError");
            AnnounceNomi(T("chatError"));
        }
        finally
        {
            entry.Seconds = Math.Round(clock.Elapsed.TotalSeconds, 1);
            thread.Items.Remove(entry);
            liveEntry = null;
            nomiRun = null;
            threads.Add(thread, entry);
            if (view == "nomi") BuildNomi();
        }
    }

    private void AnnounceNomi(string message)
    {
        if (FrameworkElementAutomationPeer.FromElement(NomiPanel) is { } peer || (peer = FrameworkElementAutomationPeer.CreatePeerForElement(NomiPanel)) is not null)
            peer.RaiseNotificationEvent(AutomationNotificationKind.ActionCompleted, AutomationNotificationProcessing.ImportantMostRecent, message, "NomiStatus");
    }
}
