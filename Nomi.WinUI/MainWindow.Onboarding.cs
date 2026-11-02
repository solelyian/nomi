using System.Globalization;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.System;

namespace Nomi;

public sealed partial class MainWindow
{
    private Grid? onboarding;
    private StackPanel? onboardingBody;
    private StackPanel? onboardingRail;
    private Grid? onboardingFooter;
    private string onboardingStep = Onboarding.Welcome;
    private string onboardingTasks = "";
    private ProgressBar? onboardingProgress;
    private TextBlock? onboardingModelStatus;

    private string StepTitle(string step) => T($"oobe-{step}");

    private string WelcomePreview(string mode)
    {
        var day = profile.Day;
        var start = day.At(Now, day.LunchEnd).AddMinutes(15);
        var now = day.At(Now, day.LunchStart).AddMinutes(-30);
        return mode == NumberModes.Figures
            ? string.Format(CultureInfo.CurrentCulture, T("welcomePreviewFigures"),
                Readable.Span(start, start.AddMinutes(40), now, mode, language, day), Readable.Duration(40, mode, language))
            : string.Format(CultureInfo.CurrentCulture, T("welcomePreview"),
                Readable.Clock(start, now, mode, language, day), Readable.Duration(40, mode, language));
    }

    private void ShowOnboarding(bool restart = false)
    {
        if (restart) profile.OnboardingStep = Onboarding.Welcome;
        onboardingStep = profile.OnboardingStep;
        if (onboarding is not null) Root.Children.Remove(onboarding);
        ClosePalette();
        CloseSheet();
        onboarding = new Grid { Background = Palette("NomiCanvas"), Padding = new Thickness(0, 44, 0, 0) };
        AutomationProperties.SetAutomationId(onboarding, "Onboarding");
        Grid.SetRowSpan(onboarding, 16);
        Grid.SetColumnSpan(onboarding, 16);
        onboarding.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
        onboarding.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        onboarding.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        onboarding.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        onboardingRail = new StackPanel { Spacing = 4, Padding = new Thickness(32, 28, 20, 28) };
        var rail = new Border { Background = Palette("NomiSurface2"), Child = new ScrollViewer { Content = onboardingRail } };
        Grid.SetRowSpan(rail, 2);
        onboarding.Children.Add(rail);

        onboardingBody = new StackPanel { Spacing = 16, MaxWidth = 640, HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(56, 40, 56, 32) };
        var scroll = new ScrollViewer { Content = onboardingBody, HorizontalScrollMode = ScrollMode.Disabled };
        Grid.SetColumn(scroll, 1);
        onboarding.Children.Add(scroll);

        onboardingFooter = new Grid { Padding = new Thickness(56, 14, 40, 22), ColumnSpacing = 10 };
        Grid.SetColumn(onboardingFooter, 1);
        Grid.SetRow(onboardingFooter, 1);
        onboarding.Children.Add(onboardingFooter);

        onboarding.KeyDown += OnboardingKey;
        Root.Children.Add(onboarding);
        BuildOnboarding();
    }

    private void OnboardingKey(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == VirtualKey.Escape)
        {
            args.Handled = true;
            if (onboardingStep != Onboarding.Welcome) FinishOnboardingLater();
        }
        else if (args.Key == VirtualKey.Enter && args.OriginalSource is not (TextBox or Button or ComboBox or TimePicker))
        {
            args.Handled = true;
            NextOnboarding();
        }
    }

    private void GoToOnboarding(string step, string? focus = null)
    {
        onboardingStep = step;
        profile.OnboardingStep = step;
        SaveProfile();
        BuildOnboarding(focus);
    }

    private void NextOnboarding()
    {
        if (onboardingStep == Onboarding.Tasks) { CompleteOnboarding(); return; }
        if (Onboarding.Next(onboardingStep) is { } next) GoToOnboarding(next);
    }

    private void FinishOnboardingLater()
    {
        SaveProfile();
        CloseOnboarding();
        ShowToast(T("oobeLaterSaved"), null, null);
    }

    private void CloseOnboarding()
    {
        if (onboarding is not null) Root.Children.Remove(onboarding);
        onboarding = null;
        onboardingBody = null;
        onboardingProgress = null;
        onboardingModelStatus = null;
        Refresh();
    }

    private void CompleteOnboarding()
    {
        foreach (var title in Onboarding.TaskLines(onboardingTasks)) board.Add(ReadPhrase(title), Columns.Todo, null);
        onboardingTasks = "";
        profile.Onboarded = true;
        profile.OnboardingStep = Onboarding.Welcome;
        SaveProfile();
        board.Save();
        view = "today";
        CloseOnboarding();
        ShowToast(T("welcomeSaved"), null, null);
        if (!profile.TourShown) DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, ShowTour);
    }

    private void RefreshOnboarding()
    {
        if (onboarding is null) return;
        if (onboardingStep is "agenda") BuildOnboarding(FocusManager.GetFocusedElement(Root.XamlRoot) is FrameworkElement element ? AutomationProperties.GetAutomationId(element) : null);
    }

    private void BuildOnboarding(string? focus = null)
    {
        if (onboarding is null || onboardingBody is null || onboardingRail is null || onboardingFooter is null) return;
        onboarding.Background = Palette("NomiCanvas");
        onboardingProgress = null;
        onboardingModelStatus = null;
        var index = Onboarding.Index(onboardingStep);

        onboardingRail.Children.Clear();
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 0, 0, 24) };
        var logo = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "nomi-512.png");
        if (System.IO.File.Exists(logo)) brand.Children.Add(new Image { Source = new BitmapImage(new Uri(logo)), Width = 28, Height = 28 });
        var name = Text("Nomi", 18);
        name.FontWeight = FontWeights.SemiBold;
        name.VerticalAlignment = VerticalAlignment.Center;
        brand.Children.Add(name);
        onboardingRail.Children.Add(brand);
        var list = new StackPanel { Spacing = 2 };
        AutomationProperties.SetName(list, T("oobeSteps"));
        for (var i = 0; i < Onboarding.Steps.Length; i++)
        {
            var step = Onboarding.Steps[i];
            var state = i < index ? T("oobeStepDone") : i == index ? T("oobeStepCurrent") : T("oobeStepNext");
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            row.Children.Add(new FontIcon
            {
                Glyph = i < index ? "\uE73E" : i == index ? "\uE915" : "\uEA3A",
                FontSize = i == index ? 10 : 12,
                Width = 16,
                Foreground = Palette(i <= index ? "NomiAccent" : "NomiInk3")
            });
            var label = Text(StepTitle(step), 13.5, i == index ? "NomiInk" : i < index ? "NomiInk2" : "NomiInk3");
            if (i == index) label.FontWeight = FontWeights.SemiBold;
            row.Children.Add(label);
            if (i < index)
            {
                var jump = new Button { Content = row, Style = (Style)Application.Current.Resources["NomiGhost"], HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left };
                AutomationProperties.SetName(jump, $"{StepTitle(step)}, {state}");
                AutomationProperties.SetAutomationId(jump, $"OnboardingRail_{step}");
                jump.Click += (_, _) => GoToOnboarding(step);
                onboardingRail.Children.Add(jump);
            }
            else
            {
                var holder = new Border { Padding = new Thickness(11, 7, 11, 7), Child = row };
                AutomationProperties.SetName(holder, $"{StepTitle(step)}, {state}");
                onboardingRail.Children.Add(holder);
            }
        }

        onboardingBody.Children.Clear();
        var eyebrow = Eyebrow(StepTitle(onboardingStep));
        onboardingBody.Children.Add(eyebrow);
        var heading = Heading(T($"oobe-{onboardingStep}Title"), 28);
        AutomationProperties.SetAutomationId(heading, $"OnboardingStep_{onboardingStep}");
        onboardingBody.Children.Add(heading);
        onboardingBody.Children.Add(Text(T($"oobe-{onboardingStep}Intro"), 14.5, "NomiInk2"));
        switch (onboardingStep)
        {
            case Onboarding.Welcome: BuildWelcomeStep(); break;
            case "reading": BuildReadingStep(); break;
            case "numbers": BuildNumbersStep(); break;
            case "day": BuildDayStep(); break;
            case "pace": BuildPaceStep(); break;
            case "model": BuildModelStep(); break;
            case "presence": BuildPresenceStep(); break;
            case "agenda": onboardingBody.Children.Add(AgendaCard()); break;
            case "data": BuildDataStep(); break;
            case Onboarding.Tasks: BuildTasksStep(); break;
        }

        onboardingFooter.Children.Clear();
        onboardingFooter.ColumnDefinitions.Clear();
        foreach (var width in new[] { GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto, GridLength.Auto })
            onboardingFooter.ColumnDefinitions.Add(new ColumnDefinition { Width = width });
        void Add(Button button, int column) { Grid.SetColumn(button, column); onboardingFooter.Children.Add(button); }
        if (Onboarding.Previous(onboardingStep) is { } previous)
        {
            var back = Chip(T("oobeBack"), "NomiGhost", "\uE72B", "OnboardingBack");
            back.Click += (_, _) => GoToOnboarding(previous);
            Add(back, 0);
            var later = Chip(T("oobeLater"), "NomiGhost", null, "OnboardingLater");
            later.Click += (_, _) => FinishOnboardingLater();
            Add(later, 1);
        }
        if (Onboarding.CanSkip(onboardingStep) && onboardingStep is not ("data" or Onboarding.Tasks))
        {
            var skip = Chip(T("oobeSkip"), "NomiChip", null, "OnboardingSkip");
            skip.Click += (_, _) => NextOnboarding();
            Add(skip, 3);
        }
        var nextLabel = onboardingStep switch
        {
            Onboarding.Welcome => T("welcomeStart"),
            "data" => T("oobeUnderstood"),
            Onboarding.Tasks => T("oobeFinish"),
            _ => T("oobeContinue")
        };
        var next = Primary(nextLabel, "OnboardingNext");
        next.Click += (_, _) => NextOnboarding();
        Add(next, 4);

        Title = $"Nomi — {StepTitle(onboardingStep)}";
        Reveal(onboardingBody, 8);
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            if (onboardingBody is null) return;
            var target = focus is null ? null : FindById(onboardingBody, focus);
            target ??= FindFirstInput(onboardingBody) ?? next;
            if (target is Control control) control.Focus(FocusState.Keyboard);
            if (FrameworkElementAutomationPeer.FromElement(heading) is { } peer)
                peer.RaiseNotificationEvent(AutomationNotificationKind.Other, AutomationNotificationProcessing.ImportantMostRecent, $"{StepTitle(onboardingStep)}. {heading.Text}", "OnboardingStep");
        });
    }

    private static FrameworkElement? FindById(DependencyObject root, string id)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is FrameworkElement element && AutomationProperties.GetAutomationId(element) == id) return element;
            if (FindById(child, id) is { } found) return found;
        }
        return null;
    }

    private static Control? FindFirstInput(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is RadioButton or TextBox or ToggleSwitch or ComboBox or TimePicker or ToggleButton or Button && child is Control { IsEnabled: true, IsTabStop: true } control) return control;
            if (FindFirstInput(child) is { } found) return found;
        }
        return null;
    }

    private void ApplyOnboarding(Action change, string focus) => DispatcherQueue.TryEnqueue(() =>
    {
        if (onboarding is null) return;
        change();
        SaveProfile();
        ApplyReading();
        ApplyPresence();
        Refresh();
        BuildOnboarding(focus);
    });

    private void OnboardingChoice(string id, string label, string help, string[] options, int selected, Action<int> changed)
    {
        var header = new StackPanel { Spacing = 2 };
        var title = Text(label, 14.5);
        title.FontWeight = FontWeights.SemiBold;
        header.Children.Add(title);
        if (help.Length > 0) header.Children.Add(Text(help, 12.5, "NomiInk3"));
        var choice = new RadioButtons { Header = header, MaxColumns = Math.Min(options.Length, 3), SelectedIndex = selected };
        foreach (var option in options) choice.Items.Add(option);
        AutomationProperties.SetName(choice, label);
        AutomationProperties.SetAutomationId(choice, id);
        choice.SelectionChanged += (_, _) =>
        {
            if (choice.SelectedIndex < 0 || choice.SelectedIndex == selected) return;
            var value = choice.SelectedIndex;
            ApplyOnboarding(() => changed(value), id);
        };
        onboardingBody!.Children.Add(choice);
    }

    private void OnboardingSwitch(string id, string label, string help, bool value, Action<bool> changed)
    {
        var card = new Border { Style = (Style)Application.Current.Resources["NomiCard"], Padding = new Thickness(18, 12, 18, 12) };
        var grid = new Grid { ColumnSpacing = 16 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var text = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
        var title = Text(label, 14);
        title.FontWeight = FontWeights.SemiBold;
        text.Children.Add(title);
        text.Children.Add(Text(help, 12, "NomiInk3"));
        grid.Children.Add(text);
        var toggle = new ToggleSwitch { IsOn = value, OnContent = T("on"), OffContent = T("off"), VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(toggle, label);
        AutomationProperties.SetHelpText(toggle, help);
        AutomationProperties.SetAutomationId(toggle, id);
        toggle.Toggled += (_, _) => { if (toggle.IsOn != value) ApplyOnboarding(() => changed(toggle.IsOn), id); };
        Grid.SetColumn(toggle, 1);
        grid.Children.Add(toggle);
        card.Child = grid;
        onboardingBody!.Children.Add(card);
    }

    private void BuildWelcomeStep()
    {
        var tagline = Text(T("oobeTagline"), 18, "NomiAccent");
        tagline.FontWeight = FontWeights.SemiBold;
        onboardingBody!.Children.Insert(2, tagline);
        OnboardingChoice("OnboardingLanguage", T("language"), T("oobeLanguageHelp"), ["Français", "English"], language == "fr" ? 0 : 1, value =>
        {
            CancelGeneration();
            language = value == 0 ? "fr" : "en";
        });
        onboardingBody.Children.Add(Text(T("oobeWelcomeNote"), 12.5, "NomiInk3"));
    }

    private void BuildReadingStep()
    {
        var theme = profile.Theme switch { "light" => 1, "dark" => 2, _ => 0 };
        OnboardingChoice("OnboardingTheme", T("theme"), T("themeHelp"), [T("themeSystem"), T("themeLight"), T("themeDark")], theme, value =>
        {
            profile.Theme = value switch { 1 => "light", 2 => "dark", _ => "system" };
            Root.RequestedTheme = value switch { 1 => ElementTheme.Light, 2 => ElementTheme.Dark, _ => ElementTheme.Default };
        });
        OnboardingChoice("OnboardingSpacing", T("spacing"), T("spacingHelp"), [T("spacingStandard"), T("spacingAiry")], profile.Spacing == "airy" ? 1 : 0,
            value => profile.Spacing = value == 1 ? "airy" : "standard");
        OnboardingChoice("OnboardingContrast", T("contrast"), T("contrastHelp"), [T("contrastStandard"), T("contrastStrong")], profile.Contrast == "strong" ? 1 : 0,
            value => profile.Contrast = value == 1 ? "strong" : "standard");
        OnboardingChoice("OnboardingMotion", T("motionSetting"), T("motionHelp"), [T("motionSystem"), T("motionReduced")], profile.Motion == "reduced" ? 1 : 0,
            value => profile.Motion = value == 1 ? "reduced" : "system");
        var sample = new StackPanel { Spacing = 6 };
        var title = Text(T("welcomeTask"), 15);
        title.FontWeight = FontWeights.SemiBold;
        sample.Children.Add(title);
        sample.Children.Add(Text(T("oobeReadingSample"), 14));
        sample.Children.Add(Text(T("oobeReadingSampleNote"), 12.5, "NomiInk3"));
        onboardingBody!.Children.Add(Glass(sample, 18));
    }

    private void BuildNumbersStep()
    {
        var modes = NumberModes.All;
        OnboardingChoice("OnboardingNumbers", T("welcomeTitle"), "", [.. modes.Select(NumberModeName)], Array.IndexOf(modes, profile.Numbers),
            value => profile.Numbers = modes[value]);
        var sample = new StackPanel { Spacing = 6 };
        var task = Text(T("welcomeTask"), 15);
        task.FontWeight = FontWeights.SemiBold;
        sample.Children.Add(task);
        var preview = Text(WelcomePreview(profile.Numbers), 14);
        AutomationProperties.SetLiveSetting(preview, AutomationLiveSetting.Polite);
        sample.Children.Add(preview);
        onboardingBody!.Children.Add(Glass(sample, 18));
        onboardingBody.Children.Add(Text(T("welcomeNote"), 12.5, "NomiInk3"));
    }

    private void BuildDayStep()
    {
        var day = profile.Day;
        var grid = new Grid { ColumnSpacing = 16, RowSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition());
        void Picker(string key, TimeSpan value, int row, int column, Func<TimeSpan, Day> change)
        {
            var picker = new TimePicker { Header = T(key), ClockIdentifier = "24HourClock", MinuteIncrement = 5, Time = value, HorizontalAlignment = HorizontalAlignment.Stretch };
            var id = $"OnboardingDay_{key}";
            AutomationProperties.SetName(picker, T(key));
            AutomationProperties.SetAutomationId(picker, id);
            picker.TimeChanged += (_, args) =>
            {
                var next = change(args.NewTime);
                if (!next.Valid) { ShowToast(T("dayInvalid"), null, null); picker.Time = args.OldTime; return; }
                ApplyOnboarding(() =>
                {
                    profile.DayStart = next.Start;
                    profile.LunchStart = next.LunchStart;
                    profile.LunchEnd = next.LunchEnd;
                    profile.DayEnd = next.End;
                }, id);
            };
            Grid.SetRow(picker, row);
            Grid.SetColumn(picker, column);
            grid.Children.Add(picker);
        }
        Picker("dayStart", day.Start, 0, 0, value => day with { Start = value });
        Picker("dayEnd", day.End, 0, 1, value => day with { End = value });
        Picker("lunchStart", day.LunchStart, 1, 0, value => day with { LunchStart = value });
        Picker("lunchEnd", day.LunchEnd, 1, 1, value => day with { LunchEnd = value });
        onboardingBody!.Children.Add(grid);

        var label = Text(T("oobeWorkingDays"), 14.5);
        label.FontWeight = FontWeights.SemiBold;
        label.Margin = new Thickness(0, 8, 0, 0);
        onboardingBody.Children.Add(label);
        var days = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        AutomationProperties.SetName(days, T("oobeWorkingDays"));
        var culture = CultureInfo.GetCultureInfo(language == "fr" ? "fr-FR" : "en-US");
        foreach (var weekday in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday })
        {
            var on = profile.WorkingDays.Contains(weekday);
            var full = culture.TextInfo.ToTitleCase(culture.DateTimeFormat.GetDayName(weekday));
            var toggle = new ToggleButton { Content = culture.TextInfo.ToTitleCase(culture.DateTimeFormat.GetAbbreviatedDayName(weekday)), IsChecked = on, MinWidth = 56, CornerRadius = new CornerRadius(10) };
            var id = $"OnboardingWorkday_{weekday}";
            AutomationProperties.SetName(toggle, full);
            AutomationProperties.SetAutomationId(toggle, id);
            void Changed(object sender, RoutedEventArgs args)
            {
                if (toggle.IsChecked == on) return;
                if (on && profile.WorkingDays.Count == 1) { toggle.IsChecked = true; ShowToast(T("oobeWorkingDaysOne"), null, null); return; }
                ApplyOnboarding(() =>
                {
                    if (on) profile.WorkingDays.Remove(weekday);
                    else profile.WorkingDays.Add(weekday);
                    profile.Normalize();
                }, id);
            }
            toggle.Checked += Changed;
            toggle.Unchecked += Changed;
            days.Children.Add(toggle);
        }
        onboardingBody.Children.Add(days);
        onboardingBody.Children.Add(Text(T("oobeDayNote"), 12.5, "NomiInk3"));
    }

    private void BuildPaceStep()
    {
        OnboardingChoice("OnboardingStepSize", T("stepSize"), T("stepSizeHelp"), [T("stepSmall"), T("stepMedium")], profile.StepSize == "medium" ? 1 : 0,
            value => profile.StepSize = value == 1 ? "medium" : "small");
        OnboardingChoice("OnboardingResponse", T("response"), T("responseHelp"), [T("steps"), T("summary")], summary ? 1 : 0,
            value => summary = value == 1);
        OnboardingSwitch("OnboardingCalm", T("calm"), T("calmHelp"), profile.Calm, value => profile.Calm = value);
        OnboardingSwitch("OnboardingChain", T("chain"), T("chainHelp"), profile.Chain, value => profile.Chain = value);
    }

    private void BuildPresenceStep()
    {
        OnboardingSwitch("OnboardingKeepRunning", T("keepRunning"), T("keepRunningHelp"), profile.KeepRunning, value => profile.KeepRunning = value);
        OnboardingSwitch("OnboardingCapture", T("captureShortcut"), T("captureShortcutHelp"), profile.CaptureHotKey, value => profile.CaptureHotKey = value);
        OnboardingSwitch("OnboardingNotifications", T("notifications"), T("notificationsHelp"), profile.Notifications, value => profile.Notifications = value);
        OnboardingSwitch("OnboardingAutoStart", T("startWithWindows"), T("startWithWindowsHelp"), AutoStart.Enabled, value => AutoStart.Set(value));
        OnboardingSwitch("OnboardingUpdates", T("updatesSetting"), T("updatesHelp"), profile.CheckUpdates, value => profile.CheckUpdates = value);
    }

    private void BuildDataStep()
    {
        var list = new StackPanel { Spacing = 14 };
        foreach (var (glyph, key) in new[] { ("\uE7EF", "oobeDataLocal"), ("\uE72E", "oobeDataEncrypted"), ("\uE8F1", "oobeDataMemory"), ("\uEDE1", "oobeDataExport") })
        {
            var row = new Grid { ColumnSpacing = 14 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.Children.Add(new FontIcon { Glyph = glyph, FontSize = 18, Foreground = Palette("NomiAccent"), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 0, 0) });
            var text = new StackPanel { Spacing = 2 };
            var title = Text(T(key), 14.5);
            title.FontWeight = FontWeights.SemiBold;
            text.Children.Add(title);
            text.Children.Add(Text(T($"{key}Help"), 13, "NomiInk2"));
            Grid.SetColumn(text, 1);
            row.Children.Add(text);
            list.Children.Add(row);
        }
        onboardingBody!.Children.Add(Glass(list, 22));
    }

    private string onboardingModelState = "";

    private string OnboardingModelState() => inference.Ready ? "ready" : modelPreparation is not null ? "busy" : modelProgress.Stage;

    private void BuildModelStep()
    {
        onboardingModelState = OnboardingModelState();
        var points = new StackPanel { Spacing = 10 };
        foreach (var key in new[] { "oobeModelOffline", "oobeModelPrivate" })
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            row.Children.Add(new FontIcon { Glyph = "\uE73E", FontSize = 13, Foreground = Palette("NomiAccent") });
            row.Children.Add(Text(T(key), 13.5, "NomiInk2"));
            points.Children.Add(row);
        }
        onboardingBody!.Children.Add(points);
        onboardingBody.Children.Add(HardwareCard());

        var card = new StackPanel { Spacing = 12 };
        var name = Text(EngineLabel(), 15);
        name.FontWeight = FontWeights.SemiBold;
        card.Children.Add(name);
        onboardingModelStatus = Text("", 13.5, "NomiInk2");
        AutomationProperties.SetAutomationId(onboardingModelStatus, "OnboardingModelStatus");
        AutomationProperties.SetLiveSetting(onboardingModelStatus, AutomationLiveSetting.Polite);
        card.Children.Add(onboardingModelStatus);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        if (inference.Ready || inference.HasModel && modelPreparation is null && modelProgress.Stage == "local-model-ready")
        {
            onboardingModelStatus.Text = T("oobeModelReady");
        }
        else if (modelPreparation is not null)
        {
            onboardingProgress = new ProgressBar { Minimum = 0, Maximum = 100, Height = 6, CornerRadius = new CornerRadius(3) };
            AutomationProperties.SetName(onboardingProgress, T("engineDownloading"));
            card.Children.Insert(2, onboardingProgress);
            var pause = Chip(T("oobeModelPause"), "NomiChip", "\uE769", "OnboardingModelPause");
            pause.Click += (_, _) => modelPreparation?.Cancel();
            actions.Children.Add(pause);
            card.Children.Add(Text(T("oobeModelBackground"), 12.5, "NomiInk3"));
        }
        else
        {
            var resume = modelProgress.Stage is "model-paused" or "model-download-error";
            if (modelProgress.Stage is not ("local-model-missing" or "model-paused")) onboardingModelStatus.Text = T(modelProgress.Stage);
            else if (resume) onboardingModelStatus.Text = T("model-paused");
            else onboardingModelStatus.Text = inference.HasModel ? T("oobeModelPresent") : T("oobeModelSize");
            var download = Primary(inference.HasModel ? T("loadLocalModel") : resume ? T("oobeModelResume") : T("oobeDownloadNow"), "OnboardingDownload");
            download.Padding = new Thickness(22, 12, 22, 12);
            download.FontSize = 15;
            download.Click += (_, _) => _ = PrepareModel();
            actions.Children.Add(download);
            var later = Chip(T("routineLater"), "NomiGhost", null, "OnboardingModelLater");
            later.Click += (_, _) => NextOnboarding();
            actions.Children.Add(later);
            card.Children.Add(actions);
            if (!inference.HasModel && inference.DownloadBytes > 0) card.Children.Add(Text(DownloadLabel(), 12.5, "NomiInk3"));
            onboardingBody.Children.Add(Glass(card, 22, true));
            UpdateOnboardingModel();
            return;
        }
        if (actions.Children.Count > 0) card.Children.Add(actions);
        onboardingBody.Children.Add(Glass(card, 22, true));
        UpdateOnboardingModel();
    }

    private void UpdateOnboardingModel()
    {
        if (onboarding is null || onboardingStep != "model") return;
        if (OnboardingModelState() != onboardingModelState)
        {
            var focused = FocusManager.GetFocusedElement(Root.XamlRoot) is FrameworkElement element ? AutomationProperties.GetAutomationId(element) : null;
            BuildOnboarding(focused is "OnboardingDownload" or "OnboardingModelPause" ? null : focused);
            return;
        }
        if (onboardingProgress is null || onboardingModelStatus is null) return;
        var downloading = modelProgress.Stage == "model-downloading";
        onboardingProgress.IsIndeterminate = !downloading;
        onboardingProgress.Value = modelProgress.Fraction * 100;
        onboardingModelStatus.Text = downloading
            ? $"{T("engineDownloading")} · {Readable.Fraction(modelProgress.Fraction, language)}"
            : T(modelProgress.Stage);
    }

    private void BuildTasksStep()
    {
        var box = new TextBox
        {
            Text = onboardingTasks,
            PlaceholderText = T("oobeTasksHint"),
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 140,
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
            BorderBrush = Palette("NomiGlassEdge"),
            Background = Palette("NomiGlassStrong")
        };
        AutomationProperties.SetName(box, T("oobe-tasksTitle"));
        AutomationProperties.SetHelpText(box, T("oobeTasksHelp"));
        AutomationProperties.SetAutomationId(box, "OnboardingTasks");
        var finish = new KeyboardAccelerator { Key = VirtualKey.Enter, Modifiers = VirtualKeyModifiers.Control };
        finish.Invoked += (_, args) => { args.Handled = true; onboardingTasks = box.Text; CompleteOnboarding(); };
        box.KeyboardAccelerators.Add(finish);
        var count = Text("", 12.5, "NomiInk3");
        AutomationProperties.SetLiveSetting(count, AutomationLiveSetting.Polite);
        void Count()
        {
            var lines = Onboarding.TaskLines(box.Text).Count;
            count.Text = lines == 0 ? T("oobeTasksNone") : lines == 1 ? T("oobeTasksOne") : string.Format(CultureInfo.CurrentCulture, T("oobeTasksMany"), lines);
        }
        box.TextChanged += (_, _) => { onboardingTasks = box.Text; Count(); };
        Count();
        onboardingBody!.Children.Add(box);
        onboardingBody.Children.Add(count);
        onboardingBody.Children.Add(Text(T("oobeTasksHelp"), 12.5, "NomiInk3"));
    }

    private void ShowTour()
    {
        profile.TourShown = true;
        SaveProfile();
        ShowToast(T("oobeTour"), null, null);
    }
}
