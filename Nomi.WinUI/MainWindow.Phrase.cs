using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;

namespace Nomi;

public sealed partial class MainWindow
{
    private readonly HashSet<string> phraseIgnored = [];
    private TaskPhraseResult? phraseProposal;
    private string phraseProposalFor = "";
    private CancellationTokenSource? phraseReading;
    private StackPanel? phrasePreview;
    private TextBlock? phraseLive;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? phraseTimer;

    private TaskPhraseResult ReadPhrase(string text, IReadOnlySet<string>? ignored = null) =>
        TaskPhrase.Parse(text, Now, profile.Day, Current.Contexts.Select(item => new PhraseSpace(item.Id, item.Title)).ToArray(), language, ignored);

    private TaskPhraseResult CurrentPhrase(string text) =>
        phraseProposal is not null && phraseProposalFor == text.Trim() ? phraseProposal : ReadPhrase(text, phraseIgnored);

    private string TaskAddedText(WorkTask task) => task.Due is DateTimeOffset due
        ? string.Format(CultureInfo.CurrentCulture, T("taskAddedDue"), task.Title, At(due))
        : string.Format(CultureInfo.CurrentCulture, T("taskAdded"), task.Title);

    private StackPanel PhrasePreview()
    {
        phrasePreview = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Visibility = Visibility.Collapsed };
        AutomationProperties.SetAutomationId(phrasePreview, "PhrasePreview");
        phraseLive = new TextBlock { FontSize = 12, Foreground = Palette("NomiInk3"), VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetLiveSetting(phraseLive, AutomationLiveSetting.Polite);
        AutomationProperties.SetAutomationId(phraseLive, "PhraseUnderstood");
        return phrasePreview;
    }

    private void ResetPhrase()
    {
        phraseIgnored.Clear();
        phraseProposal = null;
        phraseReading?.Cancel();
    }

    private void PhraseChanged(TextBox box)
    {
        var text = box.Text.Trim();
        if (phraseProposalFor != text) phraseProposal = null;
        if (text.Length == 0) phraseIgnored.Clear();
        BuildPhrasePreview(box);
        if (phraseTimer is null)
        {
            phraseTimer = DispatcherQueue.CreateTimer();
            phraseTimer.IsRepeating = false;
            phraseTimer.Interval = TimeSpan.FromMilliseconds(900);
            phraseTimer.Tick += (_, _) => AnnouncePhrase();
        }
        phraseTimer.Stop();
        phraseTimer.Start();
    }

    private void AnnouncePhrase()
    {
        if (phraseLive is null || phrasePreview?.Visibility != Visibility.Visible) return;
        FrameworkElementAutomationPeer.FromElement(phraseLive)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    private void BuildPhrasePreview(TextBox box)
    {
        if (phrasePreview is null || phraseLive is null) return;
        phrasePreview.Children.Clear();
        var text = box.Text.Trim();
        var phrase = text.Length == 0 ? null : CurrentPhrase(text);
        if (phrase is null || !(phrase.Understood || phrase.Recurring || phrase.Unclear || phrase.InvalidDate || phraseIgnored.Count > 0 || phraseReading is not null))
        {
            phrasePreview.Visibility = Visibility.Collapsed;
            return;
        }
        var spoken = new List<string>();
        void Chip(string label, Action remove)
        {
            spoken.Add(label);
            var chip = new Button { Content = label + "  ✕", Style = (Style)Application.Current.Resources["NomiChip"], FontSize = 12 };
            AutomationProperties.SetName(chip, string.Format(CultureInfo.CurrentCulture, T("phraseRemove"), label));
            chip.Click += (_, _) => { remove(); BuildPhrasePreview(box); box.Focus(FocusState.Programmatic); };
            phrasePreview.Children.Add(chip);
        }
        void Act(string label, string id, Action run)
        {
            var button = new Button { Content = label, Style = (Style)Application.Current.Resources["NomiChip"], FontSize = 12 };
            AutomationProperties.SetAutomationId(button, id);
            button.Click += (_, _) => run();
            phrasePreview.Children.Add(button);
        }
        phrasePreview.Visibility = Visibility.Visible;
        phrasePreview.Children.Add(phraseLive);
        if (phrase.Understood) spoken.Add($"« {phrase.Title} »");
        if (phrase.Due is DateTimeOffset due)
            Chip($"{T("dueAt")} {At(due)}", () => Remove(phrase, PhraseKinds.Date));
        if (phrase.Minutes is int minutes) Chip(Minutes(minutes), () => Remove(phrase, PhraseKinds.Duration));
        if (phrase.Space is string space)
            Chip("#" + (Current.Contexts.FirstOrDefault(item => item.Id == space)?.Title ?? space), () => Remove(phrase, PhraseKinds.Space));
        if (phrase.InvalidDate) { spoken.Add(T("phraseInvalidDate")); phrasePreview.Children.Add(Text(T("phraseInvalidDate"), 12, "NomiAccent")); }
        if (phrase.Recurring)
            Act(T("phraseRoutine"), "PhraseRoutine", () => { routinePhrase = text; ResetPhrase(); box.Text = ""; Show("routines"); });
        if (phraseReading is not null) { spoken.Add(T("phraseReading")); phrasePreview.Children.Add(Text(T("phraseReading"), 12, "NomiInk3")); }
        else if (phrase.Unclear && !phrase.FromModel) Act(T("phraseAskNomi"), "PhraseAskNomi", () => _ = AskPhrase(box, text));
        if (phrase.FromModel) Act(T("phraseDiscard"), "PhraseDiscard", () => { phraseProposal = null; BuildPhrasePreview(box); });
        var heading = T(phrase.FromModel ? "phraseProposed" : phrase.Understood ? "phraseUnderstood" : "phraseNothing");
        phraseLive.Text = phrase.Understood ? $"{heading} « {phrase.Title} »" : heading;
        AutomationProperties.SetName(phraseLive, string.Join(", ", new[] { heading }.Concat(spoken)));
    }

    private void Remove(TaskPhraseResult phrase, string kind)
    {
        if (phrase.FromModel)
        {
            phraseProposal = kind switch
            {
                PhraseKinds.Date => phrase with { Due = null },
                PhraseKinds.Duration => phrase with { Minutes = null },
                _ => phrase with { Space = null }
            };
            return;
        }
        phraseIgnored.Add(kind);
        if (kind == PhraseKinds.Date) phraseIgnored.Add(PhraseKinds.Time);
    }

    private async Task AskPhrase(TextBox box, string text)
    {
        if (phraseReading is not null || generation is not null) return;
        if (!inference.Ready)
        {
            if (!inference.HasModel)
            {
                ShowToast(T("phraseNeedsModel"), T("assistant"), () => Show("workspace"));
                return;
            }
            await PrepareModel();
            if (!inference.Ready) return;
        }
        phraseReading = new CancellationTokenSource();
        BuildPhrasePreview(box);
        var now = Now;
        try
        {
            var result = await inference.GenerateNomiAsync(text, "plan", language, "summary", phraseReading.Token,
                instruction: TaskPhrase.ModelInstruction(now, profile.Day));
            var proposal = result.Accepted ? TaskPhrase.FromModel(result.Text, now, profile.Day, ReadPhrase(text).Space) : null;
            if (proposal is null) ShowToast(T("phraseModelEmpty"), null, null);
            else if (box.Text.Trim() == text) { phraseProposal = proposal; phraseProposalFor = text; }
        }
        catch (OperationCanceledException) { }
        catch (InferenceException) { ShowToast(T("phraseModelEmpty"), null, null); }
        finally
        {
            phraseReading.Dispose();
            phraseReading = null;
        }
        BuildPhrasePreview(box);
        AnnouncePhrase();
    }

    private void CheckReminders()
    {
        if (!profile.DueReminders || onboarding is not null) return;
        if (Reminders.Next(board.Tasks, Now, profile.Day, profile.ReminderLead, Busy) is not ReminderNotice notice) return;
        var task = notice.Task;
        Reminders.Mark(task, notice.Key);
        board.Save();
        var late = notice.Kind == Reminders.LateKind;
        var text = string.Format(CultureInfo.CurrentCulture, T(late ? "reminderLate" : "reminderStart"), task.Title, At(task.Due!.Value));
        NotifyAway("Nomi", text, "board", notice.Discreet);
        if (late) ShowToast(text, T("open"), () => OpenSheet(task.Id));
        else ShowToast(text, T("reminderBegin"), () => { Move(task, Columns.Doing); Refresh(); },
            T("reminderLater"), () => { Reminders.Snooze(task, Now); board.Save(); });
    }

    private Action? toastLater;

    private void ToastLaterClicked(object sender, RoutedEventArgs args)
    {
        var run = toastLater;
        Toast.Visibility = Visibility.Collapsed;
        toastAction = null;
        toastLater = null;
        run?.Invoke();
    }
}
