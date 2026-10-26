using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;

namespace Nomi;

public sealed partial class MainWindow
{
    private static readonly HttpClient nomiWeb = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly RoutineStore routines = new();
    private readonly string agendaCache = Path.Combine(SecureFile.Folder, "agenda.ics");
    private PhraseResult? routineDraft;
    private string routinePhrase = "";
    private Routine? readyRoutine;
    private double lastIdle;
    private DateTimeOffset? agendaFetched;
    private IReadOnlyList<Meeting> todayMeetings = [];
    private DateTime meetingsDate;
    private string agendaStatus = "";
    private bool agendaBusy;
    private string updateStatus = "";
    private Release? latest;
    private IReadOnlyList<RescueLine>? rescue;

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInput
    {
        public uint Size;
        public uint Time;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LastInput info);

    private static double IdleMinutes()
    {
        var info = new LastInput { Size = (uint)Marshal.SizeOf<LastInput>() };
        return GetLastInputInfo(ref info) ? unchecked((uint)Environment.TickCount - info.Time) / 60000.0 : 0;
    }

    private string TriggerName(string trigger) => T($"trigger-{trigger}");

    private string DaysText(IReadOnlyCollection<DayOfWeek> days)
    {
        if (days.Count == 7) return T("routineEveryDay");
        if (days.Count == 5 && Routine.WorkingDays.All(days.Contains)) return T("routineWeekdays");
        var names = CultureInfo.GetCultureInfo(Current.Locale).DateTimeFormat;
        return string.Join(", ", days.OrderBy(day => ((int)day + 6) % 7).Select(day => names.GetDayName(day)));
    }

    private string RoutineSentence(Routine routine) =>
        $"{TriggerName(routine.Trigger)} · {DaysText(routine.Days)} · {Minutes(routine.Minutes)}";

    private IReadOnlyList<Meeting> Meetings(DateTimeOffset now)
    {
        if (meetingsDate == now.Date) return todayMeetings;
        meetingsDate = now.Date;
        todayMeetings = profile.AgendaSource is not null && SecureFile.Read(agendaCache) is { } ics ? Agenda.On(ics, now.Date) : [];
        return todayMeetings;
    }

    private void CheckRoutines(string? happened = null)
    {
        var now = Now;
        if (readyRoutine is { } shown && (!routines.Items.Contains(shown) || shown.LastRun?.Date == now.Date)) readyRoutine = null;
        var idle = IdleMinutes();
        var back = lastIdle >= 10 && idle < 1;
        if (happened is null) lastIdle = idle;
        if (readyRoutine is not null) return;
        var moment = new RoutineMoment(now, profile.Day, happened, happened is null ? idle : 0, back, Agenda.FirstMeetingEnd(Meetings(now), profile.Day));
        if (routines.Ready(moment).FirstOrDefault() is not { } due) return;
        readyRoutine = due;
        NotifyAway(T("routineReady"), due.Name, "routines");
        if (happened is not null) return;
        if (!Busy) ShowToast(string.Format(CultureInfo.CurrentCulture, T("routineReadyToast"), due.Name), T("routineSee"), () => Show("routines"));
        if (view == "routines") Refresh();
        else { BuildRail(); BuildCompanion(); }
    }

    private StackPanel RoutineReadyContent(Routine routine)
    {
        var stack = new StackPanel { Spacing = 8 };
        var name = Text(routine.Name, 13.5);
        name.FontWeight = FontWeights.SemiBold;
        stack.Children.Add(name);
        stack.Children.Add(Text(RoutineSentence(routine), 12, "NomiInk3"));
        foreach (var step in routine.Steps.Take(4)) stack.Children.Add(Text($"• {step.Title} — {Minutes(step.Minutes)}", 12.5, "NomiInk2"));
        if (routine.Document is { } document) stack.Children.Add(Text(string.Format(CultureInfo.CurrentCulture, T("routineDocumentReady"), Path.GetFileName(document)), 12, "NomiInk3"));
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var start = Primary(T("routineStart"), "RoutineStart");
        start.Click += async (_, _) => await StartRoutine(routine);
        var later = Chip(T("routineLater"), "NomiChip", null, "RoutineLater");
        later.Click += (_, _) => { routines.Snooze(routine, Now); readyRoutine = null; Refresh(); };
        var skip = Chip(T("routineSkip"), "NomiGhost", null, "RoutineSkip");
        skip.Click += (_, _) => { routines.Ran(routine, Now); readyRoutine = null; Refresh(); };
        buttons.Children.Add(start);
        buttons.Children.Add(later);
        buttons.Children.Add(skip);
        stack.Children.Add(buttons);
        stack.Children.Add(Text(T("routineReadyNote"), 11.5, "NomiInk3"));
        return stack;
    }

    private async Task StartRoutine(Routine routine)
    {
        var now = Now;
        var task = board.Add(routine.Name, Columns.Todo, null, "routine");
        task.Steps = routine.Steps.Select(step => new WorkStep { Title = step.Title, Minutes = step.Minutes }).ToList();
        task.Estimate = routine.Minutes;
        board.Start(task, now);
        routines.Ran(routine, now);
        if (readyRoutine == routine) readyRoutine = null;
        Refresh();
        ShowToast(string.Format(CultureInfo.CurrentCulture, T("routineStarted"), routine.Name), T("open"), () => OpenSheet(task.Id));
        if (routine.Document is not { } document) return;
        try
        {
            if (AgendaSource.Remote(document) is { } uri) await Launcher.LaunchUriAsync(uri);
            else if (File.Exists(document)) await Launcher.LaunchFileAsync(await StorageFile.GetFileFromPathAsync(document));
            else ShowToast(T("routineDocumentMissing"), null, null);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or COMException)
        {
            ShowToast(T("routineDocumentMissing"), null, null);
        }
    }

    private void BuildRoutines()
    {
        RoutinesBody.Children.Clear();
        var titles = new StackPanel { Spacing = 4 };
        titles.Children.Add(Eyebrow(T("routinesEyebrow")));
        titles.Children.Add(Heading(T("routines"), 28));
        titles.Children.Add(Text(T("routinesIntro"), 13.5, "NomiInk2"));
        RoutinesBody.Children.Add(titles);

        var compose = new StackPanel { Spacing = 10 };
        compose.Children.Add(Eyebrow(T("routineNew")));
        var box = new TextBox { Text = routinePhrase, PlaceholderText = T("routinePhraseHint"), TextWrapping = TextWrapping.Wrap };
        AutomationProperties.SetName(box, T("routineNew"));
        AutomationProperties.SetAutomationId(box, "RoutinePhrase");
        var understand = Primary(T("routineUnderstand"), "RoutineUnderstand");
        void Understand()
        {
            routinePhrase = box.Text.Trim();
            if (routinePhrase.Length == 0) return;
            routineDraft = RoutinePhrase.Parse(routinePhrase);
            Refresh();
        }
        box.KeyDown += (_, args) =>
        {
            if (args.Key != VirtualKey.Enter) return;
            args.Handled = true;
            Understand();
        };
        understand.Click += (_, _) => Understand();
        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(box);
        Grid.SetColumn(understand, 1);
        understand.VerticalAlignment = VerticalAlignment.Top;
        row.Children.Add(understand);
        compose.Children.Add(row);
        compose.Children.Add(Text(T("routinePhraseExamples"), 12, "NomiInk3"));
        if (routineDraft is { } draft) compose.Children.Add(RoutinePreview(draft));
        var composer = Glass(compose, 18);
        Mark(composer, "RoutineComposer", T("routineNew"));
        RoutinesBody.Children.Add(composer);

        if (routineDraft is null && RoutineStore.Suggest(board.Tasks, routines.Items, Now) is { } suggested
            && !board.Dismissed.Contains($"routine:{RoutineStore.Key(suggested.Name)}"))
        {
            var idea = new StackPanel { Spacing = 8 };
            idea.Children.Add(Eyebrow(T("routineSuggestTitle")));
            idea.Children.Add(Text(string.Format(CultureInfo.CurrentCulture, T("routineSuggestBody"), suggested.Name, DaysText(suggested.Days)), 13));
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            var create = Chip(T("routineSuggestCreate"), "NomiChip", null, "RoutineSuggest");
            create.Click += (_, _) => { routineDraft = new(suggested, true, true); routinePhrase = ""; Refresh(); };
            var no = Chip(T("routineSuggestDismiss"), "NomiGhost", null, "RoutineSuggestDismiss");
            no.Click += (_, _) => { board.Dismissed.Add($"routine:{RoutineStore.Key(suggested.Name)}"); SaveProfile(); board.Save(); Refresh(); };
            buttons.Children.Add(create);
            buttons.Children.Add(no);
            idea.Children.Add(buttons);
            var card = Glass(idea, 18);
            Mark(card, "RoutineSuggestion", T("routineSuggestTitle"));
            RoutinesBody.Children.Add(card);
        }

        RoutinesBody.Children.Add(Eyebrow(T("routinesSaved")));
        if (routines.Items.Count == 0)
        {
            var empty = Glass(Text(T("routinesEmpty"), 13, "NomiInk2"), 18);
            Mark(empty, "RoutinesEmpty", T("routinesEmpty"));
            RoutinesBody.Children.Add(empty);
        }
        foreach (var routine in routines.Items.OrderBy(item => Array.IndexOf(Triggers.All, item.Trigger)).ThenBy(item => item.Created))
            RoutinesBody.Children.Add(RoutineCard(routine));
        RoutinesBody.Children.Add(AgendaCard());
    }

    private Border RoutinePreview(PhraseResult draft)
    {
        var routine = draft.Routine;
        var stack = new StackPanel { Spacing = 10, Padding = new Thickness(14, 12, 14, 12) };
        var title = Text(T(draft.Understood ? "routineUnderstood" : "routineNotUnderstood"), 13.5);
        title.FontWeight = FontWeights.SemiBold;
        stack.Children.Add(title);
        if (!draft.Understood) stack.Children.Add(Text(T(draft.HasName ? "routineNeedMoment" : "routineNeedAction"), 12.5, "NomiInk2"));
        var trigger = new ComboBox
        {
            ItemsSource = Triggers.All.Select(TriggerName).ToArray(),
            SelectedIndex = draft.HasTrigger ? Array.IndexOf(Triggers.All, routine.Trigger) : -1,
            PlaceholderText = T("routineTrigger"),
            MinWidth = 260
        };
        AutomationProperties.SetName(trigger, T("routineTrigger"));
        AutomationProperties.SetAutomationId(trigger, "RoutineTrigger");
        trigger.SelectionChanged += (_, _) =>
        {
            if (updating || trigger.SelectedIndex < 0) return;
            routine.Trigger = Triggers.All[trigger.SelectedIndex];
            routineDraft = draft with { HasTrigger = true };
            Refresh();
        };
        stack.Children.Add(Field(T("routineTrigger"), trigger));
        var names = CultureInfo.GetCultureInfo(Current.Locale).DateTimeFormat;
        var days = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        foreach (var day in Routine.WorkingDays.Append(DayOfWeek.Saturday).Append(DayOfWeek.Sunday))
        {
            var toggle = new ToggleButton { Content = names.GetAbbreviatedDayName(day), IsChecked = routine.Days.Contains(day), MinWidth = 46, Padding = new Thickness(8, 4, 8, 4) };
            AutomationProperties.SetName(toggle, names.GetDayName(day));
            AutomationProperties.SetAutomationId(toggle, $"RoutineDay_{(int)day}");
            toggle.Click += (_, _) =>
            {
                if (toggle.IsChecked == true) { if (!routine.Days.Contains(day)) routine.Days.Add(day); }
                else if (routine.Days.Count > 1) routine.Days.Remove(day);
                else toggle.IsChecked = true;
            };
            days.Children.Add(toggle);
        }
        stack.Children.Add(Field(T("routineDays"), days));
        if (routine.Steps.Count > 0)
        {
            var steps = new StackPanel { Spacing = 3 };
            for (var index = 0; index < routine.Steps.Count; index++)
                steps.Children.Add(Text($"{index + 1}. {routine.Steps[index].Title} — {Minutes(routine.Steps[index].Minutes)}", 12.5, "NomiInk2"));
            stack.Children.Add(Field(T("routineSteps"), steps));
        }
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var save = Primary(T("routineSave"), "RoutineSave");
        save.IsEnabled = draft.HasName && (draft.HasTrigger || trigger.SelectedIndex >= 0);
        save.Click += (_, _) =>
        {
            var index = routines.Items.FindIndex(item => item.Id == routine.Id);
            if (index >= 0) routines.Items[index] = routine;
            else routines.Items.Add(routine);
            routines.Save();
            routineDraft = null;
            routinePhrase = "";
            Refresh();
            ShowToast(string.Format(CultureInfo.CurrentCulture, T("routineSaved"), routine.Name), null, null);
        };
        var discard = Chip(T("routineDiscard"), "NomiGhost", null, "RoutineDiscard");
        discard.Click += (_, _) => { routineDraft = null; Refresh(); };
        buttons.Children.Add(save);
        buttons.Children.Add(discard);
        stack.Children.Add(buttons);
        var card = new Border { Background = Palette("NomiGlassSoft"), CornerRadius = new CornerRadius(12), Child = stack };
        Mark(card, "RoutinePreview", T(draft.Understood ? "routineUnderstood" : "routineNotUnderstood"));
        return card;
    }

    private static Routine Clone(Routine routine) =>
        System.Text.Json.JsonSerializer.Deserialize<Routine>(System.Text.Json.JsonSerializer.Serialize(routine))!;

    private Border RoutineCard(Routine routine)
    {
        var stack = new StackPanel { Spacing = 6 };
        var head = new Grid { ColumnSpacing = 12 };
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var titles = new StackPanel { Spacing = 2 };
        var name = Text(routine.Name, 14.5);
        name.FontWeight = FontWeights.SemiBold;
        titles.Children.Add(name);
        titles.Children.Add(Text(RoutineSentence(routine), 12.5, "NomiInk2"));
        head.Children.Add(titles);
        var toggle = new ToggleSwitch { IsOn = routine.Enabled, OnContent = T("on"), OffContent = T("off"), MinWidth = 0 };
        AutomationProperties.SetName(toggle, string.Format(CultureInfo.CurrentCulture, T("routineEnabled"), routine.Name));
        AutomationProperties.SetAutomationId(toggle, "RoutineEnabled");
        toggle.Toggled += (_, _) => { if (updating) return; routine.Enabled = toggle.IsOn; routines.Save(); };
        Grid.SetColumn(toggle, 1);
        head.Children.Add(toggle);
        stack.Children.Add(head);
        foreach (var step in routine.Steps.Take(6)) stack.Children.Add(Text($"• {step.Title} — {Minutes(step.Minutes)}", 12.5, "NomiInk2"));
        if (routine.Document is { } document) stack.Children.Add(Text(string.Format(CultureInfo.CurrentCulture, T("routineDocumentReady"), Path.GetFileName(document)), 12, "NomiInk3"));
        var culture = CultureInfo.GetCultureInfo(Current.Locale);
        stack.Children.Add(Text(routine.LastRun is { } last
            ? string.Format(CultureInfo.CurrentCulture, T("routineLast"), last.Date == Now.Date ? At(last) : last.ToString("dddd d MMMM", culture))
            : T("routineNever"), 11.5, "NomiInk3"));
        stack.Children.Add(RoutineButtons(routine));
        var card = Glass(stack, 18);
        Mark(card, "RoutineCard", routine.Name);
        return card;
    }

    private StackPanel RoutineButtons(Routine routine)
    {
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var run = Chip(T("routineRun"), "NomiChip", "\uE768", "RoutineRun");
        run.Click += async (_, _) => await StartRoutine(routine);
        var edit = Chip(T("routineEdit"), "NomiChip", "\uE70F", "RoutineEdit");
        edit.Click += (_, _) => { routineDraft = new(Clone(routine), true, true); routinePhrase = ""; Refresh(); RoutinesPanel.ChangeView(null, 0, null); };
        var link = Chip(routine.Document is null ? T("routineDocument") : T("routineDocumentRemove"), "NomiGhost", "\uE8A5", "RoutineDocument");
        link.Click += async (_, _) =>
        {
            if (routine.Document is not null) { routine.Document = null; routines.Save(); Refresh(); return; }
            await PickRoutineDocument(routine);
        };
        var delete = Chip(T("routineDelete"), "NomiGhost", "\uE74D", "RoutineDelete");
        delete.Click += (_, _) =>
        {
            var index = routines.Items.IndexOf(routine);
            routines.Items.Remove(routine);
            routines.Save();
            if (readyRoutine == routine) readyRoutine = null;
            Refresh();
            ShowToast(string.Format(CultureInfo.CurrentCulture, T("routineDeleted"), routine.Name), T("undo"), () =>
            {
                routines.Items.Insert(Math.Clamp(index, 0, routines.Items.Count), routine);
                routines.Save();
                Refresh();
            });
        };
        foreach (var button in new[] { run, edit, link, delete }) buttons.Children.Add(button);
        return buttons;
    }

    private async Task PickRoutineDocument(Routine routine)
    {
        try
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add("*");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            var file = await picker.PickSingleFileAsync();
            if (file is null) return;
            routine.Document = file.Path;
            routines.Save();
            Refresh();
        }
        catch (COMException)
        {
            ShowToast(T("routineDocumentMissing"), null, null);
        }
    }
}
