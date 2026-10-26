using System.Globalization;
using System.Text.Json;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Storage.Pickers;
using Windows.System;

namespace Nomi;

public sealed partial class MainWindow
{
    private const string FeedbackUrl = "https://github.com/solelyian/nomi/issues/new";
    private const string ReleasesUrl = "https://github.com/solelyian/nomi/releases/latest";

    private static readonly string[] SuggestionKinds = ["overrun", "due", "late", "pace", "overload", "split"];

    private readonly FontFamily readingFont = new("ms-appx:///Assets/Fonts/AtkinsonHyperlegible-Regular.ttf#Atkinson Hyperlegible");

    private void ApplyReading()
    {
        Application.Current.Resources["ContentControlThemeFontFamily"] = readingFont;
    }

    private string NumberModeName(string mode) => T(mode switch
    {
        NumberModes.Proportions => "numbersProportions",
        NumberModes.Figures => "numbersFigures",
        _ => "numbersWords"
    });

    private void SettingsSection(string title, string? help = null)
    {
        var heading = Text(title, 16);
        heading.FontWeight = FontWeights.SemiBold;
        heading.Margin = new Thickness(0, 14, 0, 0);
        AutomationProperties.SetHeadingLevel(heading, AutomationHeadingLevel.Level2);
        SettingsBody.Children.Add(heading);
        if (help is not null) SettingsBody.Children.Add(Text(help, 12.5, "NomiInk2"));
    }

    private void ChangeProfile(Action change)
    {
        change();
        SaveProfile();
        ApplyReading();
        ApplyPresence();
        Refresh();
    }

    private void BuildAdaptationSettings()
    {
        SettingsSection(T("numbersSection"), T("numbersSectionHelp"));
        var modes = NumberModes.All;
        AddSetting(T("numbers"), T("numbersHelp"), [.. modes.Select(NumberModeName)], Array.IndexOf(modes, profile.Numbers),
            value => ChangeProfile(() => profile.Numbers = modes[value]));
        SettingsBody.Children.Add(Text(string.Format(CultureInfo.CurrentCulture, T("numbersExample"),
            Readable.Clock(profile.Day.At(Now, profile.Day.LunchEnd).AddMinutes(20), profile.Day.At(Now, profile.Day.LunchStart), profile.Numbers, language, profile.Day),
            Readable.Duration(40, profile.Numbers, language)), 12, "NomiInk3"));

        SettingsSection(T("daySection"), T("daySectionHelp"));
        AddTime(T("dayStart"), profile.DayStart, value => profile.DayStart = value);
        AddTime(T("lunchStart"), profile.LunchStart, value => profile.LunchStart = value);
        AddTime(T("lunchEnd"), profile.LunchEnd, value => profile.LunchEnd = value);
        AddTime(T("dayEnd"), profile.DayEnd, value => profile.DayEnd = value);
        AddSetting(T("stepSize"), T("stepSizeHelp"), [T("stepSmall"), T("stepMedium")], profile.StepSize == "medium" ? 1 : 0,
            value => ChangeProfile(() => profile.StepSize = value == 1 ? "medium" : "small"));
        AddSetting(T("calm"), T("calmHelp"), [T("on"), T("off")], profile.Calm ? 0 : 1,
            value => ChangeProfile(() => profile.Calm = value == 0));
        AddSetting(T("chain"), T("chainHelp"), [T("on"), T("off")], profile.Chain ? 0 : 1,
            value => ChangeProfile(() => profile.Chain = value == 0));

        SettingsSection(T("readingSection"), T("readingSectionHelp"));
        AddSetting(T("spacing"), T("spacingHelp"), [T("spacingStandard"), T("spacingAiry")], profile.Spacing == "airy" ? 1 : 0,
            value => ChangeProfile(() => profile.Spacing = value == 1 ? "airy" : "standard"));
        AddSetting(T("contrast"), T("contrastHelp"), [T("contrastStandard"), T("contrastStrong")], profile.Contrast == "strong" ? 1 : 0,
            value => ChangeProfile(() => profile.Contrast = value == 1 ? "strong" : "standard"));
        AddSetting(T("motionSetting"), T("motionHelp"), [T("motionSystem"), T("motionReduced")], profile.Motion == "reduced" ? 1 : 0,
            value => ChangeProfile(() => profile.Motion = value == 1 ? "reduced" : "system"));

        BuildPresence();
        BuildMemory();
        BuildData();
    }

    private void BuildPresence()
    {
        SettingsSection(T("presenceSection"), T("presenceSectionHelp"));
        AddSetting(T("keepRunning"), T("keepRunningHelp"), [T("on"), T("off")], profile.KeepRunning ? 0 : 1,
            value => ChangeProfile(() => profile.KeepRunning = value == 0));
        AddSetting(T("startWithWindows"), T("startWithWindowsHelp"), [T("on"), T("off")], AutoStart.Enabled ? 0 : 1,
            value => { AutoStart.Set(value == 0); Refresh(); });
        AddSetting(T("captureShortcut"), T("captureShortcutHelp"), [T("on"), T("off")], profile.CaptureHotKey ? 0 : 1,
            value => ChangeProfile(() => profile.CaptureHotKey = value == 0));
        if (hotKeyTaken) SettingsBody.Children.Add(Text(T("captureShortcutTaken"), 12, "NomiAccent"));
        if (!profile.KeepRunning) return;
        AddSetting(T("notifications"), T("notificationsHelp"), [T("on"), T("off")], profile.Notifications ? 0 : 1,
            value => ChangeProfile(() => profile.Notifications = value == 0));
    }

    private void AddTime(string label, TimeSpan value, Action<TimeSpan> changed)
    {
        var card = new Border { Style = (Style)Application.Current.Resources["NomiCard"], Padding = new Thickness(18, 10, 18, 10) };
        var grid = new Grid { ColumnSpacing = 16 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = Text(label, 14);
        title.FontWeight = FontWeights.SemiBold;
        title.VerticalAlignment = VerticalAlignment.Center;
        grid.Children.Add(title);
        var picker = new TimePicker { ClockIdentifier = "24HourClock", MinuteIncrement = 15, SelectedTime = value, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(picker, label);
        picker.SelectedTimeChanged += (_, args) =>
        {
            if (updating || args.NewTime is not { } time || time == value) return;
            var before = (profile.DayStart, profile.LunchStart, profile.LunchEnd, profile.DayEnd);
            changed(time);
            if (!new Day(profile.DayStart, profile.LunchStart, profile.LunchEnd, profile.DayEnd).Valid)
            {
                (profile.DayStart, profile.LunchStart, profile.LunchEnd, profile.DayEnd) = before;
                ShowToast(T("dayInvalid"), null, null);
                Refresh();
                return;
            }
            ChangeProfile(() => { });
        };
        Grid.SetColumn(picker, 1);
        grid.Children.Add(picker);
        card.Child = grid;
        SettingsBody.Children.Add(card);
    }

    private static string Hm(TimeSpan time) => time.ToString(@"hh\:mm", CultureInfo.InvariantCulture);

    private static string KindKey(string kind) => kind switch
    {
        "overrun" => "kindOverrun",
        "due" => "kindDue",
        "late" => "kindLate",
        "pace" => "kindPace",
        "overload" => "kindOverload",
        _ => "kindSplit"
    };

    private Grid MemoryLine(string label, string value, Button? action = null)
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var text = new StackPanel { Spacing = 2 };
        text.Children.Add(Text(label, 12, "NomiInk3"));
        text.Children.Add(Text(value, 13.5));
        grid.Children.Add(text);
        if (action is not null)
        {
            action.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(action, 1);
            grid.Children.Add(action);
        }
        return grid;
    }

    private void BuildMemory()
    {
        SettingsSection(T("memorySection"), T("memoryHelp"));
        var stack = new StackPanel { Spacing = 12 };
        var (ratio, samples) = board.Pace();
        stack.Children.Add(MemoryLine(T("memoryNumbers"), NumberModeName(profile.Numbers)));
        stack.Children.Add(MemoryLine(T("memoryPace"), samples >= 3 ? Readable.Pace(ratio, language)
            : string.Format(CultureInfo.CurrentCulture, T("metricLearning"), samples, 3)));
        stack.Children.Add(MemoryLine(T("memoryDay"), string.Format(CultureInfo.CurrentCulture, T("memoryDayValue"),
            Hm(profile.DayStart), Hm(profile.LunchStart), Hm(profile.LunchEnd), Hm(profile.DayEnd))));
        stack.Children.Add(MemoryLine(T("memorySteps"), T(profile.StepSize == "medium" ? "stepMedium" : "stepSmall")));
        stack.Children.Add(MemoryLine(T("memoryCalm"), T(profile.Calm ? "memoryCalmOn" : "memoryCalmOff")));
        foreach (var kind in SuggestionKinds)
        {
            var muted = profile.Muted.Contains(kind);
            if (!muted && !profile.Signals.ContainsKey(kind)) continue;
            var signal = profile.Signals.GetValueOrDefault(kind) ?? new Signal();
            var value = string.Format(CultureInfo.CurrentCulture, T("memorySignal"), signal.Accepted, signal.Dismissed)
                + (muted ? $" · {T("memoryMuted")}" : "");
            Button? resume = null;
            if (muted)
            {
                resume = Chip(T("memoryResume"), "NomiGhost", null, $"Resume_{kind}");
                AutomationProperties.SetName(resume, $"{T("memoryResume")} : {T(KindKey(kind))}");
                resume.Click += (_, _) => ChangeProfile(() => profile.Resume(kind));
            }
            stack.Children.Add(MemoryLine(T(KindKey(kind)), value, resume));
        }
        stack.Children.Add(Text(T("memoryCorrections"), 12, "NomiInk3"));
        if (profile.Corrections.Count == 0) stack.Children.Add(Text(T("memoryNoCorrections"), 12.5, "NomiInk2"));
        foreach (var correction in profile.Corrections.ToArray())
        {
            var remove = Chip(T("remove"), "NomiGhost");
            AutomationProperties.SetName(remove, $"{T("remove")} : {correction}");
            remove.Click += (_, _) => ChangeProfile(() => profile.Corrections.Remove(correction));
            stack.Children.Add(MemoryLine("", correction, remove));
        }
        var add = new Grid { ColumnSpacing = 8 };
        add.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        add.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var input = new TextBox { PlaceholderText = T("memoryCorrectionPlaceholder"), MaxLength = 160 };
        AutomationProperties.SetName(input, T("memoryCorrections"));
        var save = Chip(T("memoryAdd"), "NomiChip", null, "MemoryAdd");
        void Add()
        {
            if (string.IsNullOrWhiteSpace(input.Text)) return;
            var value = input.Text.Trim();
            ChangeProfile(() => profile.Corrections.Add(value));
        }
        save.Click += (_, _) => Add();
        input.KeyDown += (_, args) => { if (args.Key == VirtualKey.Enter) { args.Handled = true; Add(); } };
        add.Children.Add(input);
        Grid.SetColumn(save, 1);
        add.Children.Add(save);
        stack.Children.Add(add);
        var forget = Chip(T("memoryForget"), "NomiGhost", null, "MemoryForget");
        forget.Click += (_, _) => ChangeProfile(() =>
        {
            profile.Signals.Clear();
            profile.Muted.Clear();
            profile.Dismissed.Clear();
            profile.Corrections.Clear();
            board.Dismissed.Clear();
        });
        stack.Children.Add(forget);
        var card = new Border { Style = (Style)Application.Current.Resources["NomiCard"], Padding = new Thickness(18, 14, 18, 14), Child = stack };
        AutomationProperties.SetAutomationId(card, "Memory");
        SettingsBody.Children.Add(card);
    }

    private void BuildData()
    {
        SettingsSection(T("dataSection"), T(SecureFile.Encrypts ? "dataHelp" : "dataHelpPlain"));
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var export = Chip(T("dataExport"), "NomiChip", "\uEDE1", "DataExport");
        export.Click += async (_, _) => await ExportData();
        var erase = Chip(T("dataDelete"), "NomiGhost", "\uE74D", "DataDelete");
        erase.Click += async (_, _) => await DeleteData();
        row.Children.Add(export);
        row.Children.Add(erase);
        SettingsBody.Children.Add(row);

        SettingsSection(T("aboutSection"));
        var links = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var feedback = Chip(T("feedback"), "NomiChip", "\uED15", "Feedback");
        feedback.Click += async (_, _) => await Launcher.LaunchUriAsync(new Uri(FeedbackUrl));
        links.Children.Add(feedback);
        SettingsBody.Children.Add(links);
        BuildUpdates();
        SettingsBody.Children.Add(Text(string.Format(CultureInfo.CurrentCulture, T("version"),
            typeof(App).Assembly.GetName().Version?.ToString(3) ?? "—"), 12, "NomiInk3"));
    }

    private async Task ExportData()
    {
        try
        {
            var picker = new FileSavePicker { SuggestedFileName = $"nomi-{DateTimeOffset.Now:yyyy-MM-dd}" };
            picker.FileTypeChoices.Add("JSON", [".json"]);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            var file = await picker.PickSaveFileAsync();
            if (file is null) return;
            var source = profile.AgendaSource;
            profile.AgendaSource = null;
            string json;
            try
            {
                json = JsonSerializer.Serialize(new { exported = DateTimeOffset.Now, profile, tasks = board.Tasks, routines = routines.Items, history },
                    new JsonSerializerOptions { WriteIndented = true });
            }
            finally
            {
                profile.AgendaSource = source;
            }
            await Windows.Storage.FileIO.WriteTextAsync(file, json);
            ShowToast(T("dataExported"), null, null);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            ShowToast(T("dataExportFailed"), null, null);
        }
    }

    private async Task DeleteData()
    {
        var dialog = new ContentDialog
        {
            Title = T("dataDeleteTitle"),
            Content = T("dataDeleteBody"),
            PrimaryButtonText = T("dataDeleteConfirm"),
            CloseButtonText = T("dataDeleteCancel"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Root.XamlRoot,
            RequestedTheme = Root.ActualTheme
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        CancelGeneration();
        board.Tasks.Clear();
        board.Dismissed.Clear();
        history.Clear();
        SecureFile.Delete(board.FilePath);
        SecureFile.Delete(HistoryStore.FilePath);
        SecureFile.Delete(profileStore.Path);
        routines.Items.Clear();
        readyRoutine = null;
        routineDraft = null;
        rescue = null;
        SecureFile.Delete(routines.Path);
        SecureFile.Delete(agendaCache);
        meetingsDate = default;
        todayMeetings = [];
        try { if (Directory.Exists(StartupDiagnostics.DirectoryPath)) Directory.Delete(StartupDiagnostics.DirectoryPath, true); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        profile = new Profile { Language = language };
        AutoStart.Set(false);
        ApplyPresence();
        summary = false;
        SyncBoard();
        ApplyReading();
        Refresh();
        ShowToast(T("dataDeleted"), null, null);
        ShowWelcome();
    }
}
