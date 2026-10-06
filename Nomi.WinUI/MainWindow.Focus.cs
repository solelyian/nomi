using System.Globalization;
using System.Net.Http;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using WinRT.Interop;

namespace Nomi;

public sealed partial class MainWindow
{
    private const int FocusInterval = 45;
    private const double FocusChange = 0.015;
    private readonly VisionClient vision = new();
    private CancellationTokenSource? visionPreparation;
    private ModelProgress visionProgress = new("vision-idle");
    private string focusState = "idle";
    private bool focusBusy;
    private WindowChoice? focusWindow;
    private IReadOnlyList<WindowChoice> focusChoices = [];
    private bool focusListed;
    private CancellationTokenSource? focusLoop;
    private readonly SemaphoreSlim focusWake = new(0);
    private byte[]? lastPrint;
    private ScreenInsight? insight;
    private TableReport? tableReport;
    private DateTimeOffset focusStarted;
    private DateTimeOffset? nextAnalysis;
    private int focusAnalyses;
    private int focusSkipped;
    private string focusMessage = "";
    private string? focusTaskId;
    private TextBlock? focusCountdown;

    private string Label(string code) => Current.Labels.TryGetValue(code, out var value) ? value : T("focusCaptureError");

    private void BuildFocus()
    {
        FocusBody.Children.Clear();
        focusCountdown = null;
        var live = focusState != "idle";
        var header = new Grid { ColumnSpacing = 12 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var titles = new StackPanel { Spacing = 2 };
        titles.Children.Add(Text(T("focusEyebrow"), 13, "NomiInk3"));
        titles.Children.Add(Heading(T("focus"), 28));
        header.Children.Add(titles);
        FrameworkElement side;
        if (live)
        {
            var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Bottom };
            var pill = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7, VerticalAlignment = VerticalAlignment.Center };
            pill.Children.Add(new Ellipse { Width = 7, Height = 7, Fill = Palette(focusState == "paused" ? "NomiInk3" : "NomiOk"), VerticalAlignment = VerticalAlignment.Center });
            pill.Children.Add(Text(string.Format(CultureInfo.CurrentCulture, T("focusLocal"), FocusInterval), 12, "NomiInk2"));
            var local = new Border { Style = (Style)Application.Current.Resources["NomiPill"], Padding = new Thickness(11, 5, 11, 5), Child = pill, VerticalAlignment = VerticalAlignment.Center };
            Capsule(local);
            controls.Children.Add(local);
            var pause = Chip(focusState == "paused" ? T("resume") : T("pauseTimer"), "NomiChip", null, "FocusPause");
            pause.Click += (_, _) => PauseFocus(focusState != "paused");
            controls.Children.Add(pause);
            var stop = Chip(T("focusStopShare"), "NomiChip", null, "FocusStop");
            stop.Click += (_, _) => { StopFocus(); Refresh(); };
            controls.Children.Add(stop);
            side = controls;
        }
        else
        {
            var none = Text(focusWindow is null ? T("focusNoShare") : $"{focusWindow.Process} — {focusWindow.Title}", 13, "NomiInk3");
            none.MaxWidth = 260;
            none.MaxLines = 1;
            none.TextTrimming = TextTrimming.CharacterEllipsis;
            none.VerticalAlignment = VerticalAlignment.Bottom;
            none.Margin = new Thickness(0, 0, 0, 6);
            side = none;
        }
        Grid.SetColumn(side, 1);
        header.Children.Add(side);
        FocusBody.Children.Add(header);

        var privacy = new Grid { ColumnSpacing = 8 };
        privacy.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        privacy.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        privacy.Children.Add(new FontIcon { Glyph = "\uE72E", FontSize = 12, Foreground = Palette("NomiSage"), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 0, 0) });
        var privacyText = Text(T("focusPrivacy"), 12.5, "NomiInk3");
        Grid.SetColumn(privacyText, 1);
        privacy.Children.Add(privacyText);
        var privacyLine = new Border { Child = privacy, Margin = new Thickness(0, -4, 0, 0) };
        Mark(privacyLine, "FocusPrivacy");
        FocusBody.Children.Add(privacyLine);

        if (live) FocusBody.Children.Add(Stage());
        else
        {
            FocusBody.Children.Add(WindowPicker());
            if (insight is not null) FocusBody.Children.Add(InsightCard(insight));
        }
        FocusBody.Children.Add(VisionCard());
        if (focusMessage.Length > 0)
        {
            var message = Text(focusMessage, 12.5, "NomiAccent");
            AutomationProperties.SetAutomationId(message, "FocusMessage");
            AutomationProperties.SetLiveSetting(message, AutomationLiveSetting.Polite);
            FocusBody.Children.Add(message);
        }
        for (var index = 1; index < FocusBody.Children.Count; index++) Reveal(FocusBody.Children[index], 10, index * 40);
    }

    private Border VisionCard()
    {
        var stack = new StackPanel { Spacing = 8 };
        var row = new Grid { ColumnSpacing = 12 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var preparing = visionPreparation is not null;
        row.Children.Add(new Ellipse
        {
            Width = 8,
            Height = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Fill = Palette(vision.Ready ? "NomiOk" : preparing ? "NomiAccent" : vision.HasModel ? "NomiSand" : "NomiInk3")
        });
        var info = new StackPanel { Spacing = 2 };
        var name = Text(T("visionTitle"), 13.5);
        name.FontWeight = FontWeights.SemiBold;
        info.Children.Add(name);
        string status;
        if (vision.Ready) status = T("visionReady");
        else if (visionProgress.Stage == "model-downloading") status = $"{T("engineDownloading")} · {visionProgress.Fraction:P0}";
        else if (preparing) status = T("engineBusy");
        else if (vision.HasModel) status = T("visionInstalled");
        else status = string.Format(CultureInfo.CurrentCulture, T("visionMissing"), (vision.Bytes / 1_000_000_000.0).ToString("0.0", CultureInfo.CurrentCulture));
        var state = Text($"{vision.Model} · {status}", 12, "NomiInk3");
        AutomationProperties.SetAutomationId(state, "VisionStatus");
        AutomationProperties.SetLiveSetting(state, AutomationLiveSetting.Polite);
        info.Children.Add(state);
        Grid.SetColumn(info, 1);
        row.Children.Add(info);
        if (!vision.HasModel && !preparing)
        {
            var download = Chip(T("visionDownload"), "NomiChip", "\uE896", "VisionDownload");
            download.Click += async (_, _) => await PrepareVision(true);
            download.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(download, 2);
            row.Children.Add(download);
        }
        else if (preparing)
        {
            var cancel = Chip(T("stop"), "NomiGhost", null, "VisionCancel");
            cancel.Click += (_, _) => visionPreparation?.Cancel();
            Grid.SetColumn(cancel, 2);
            row.Children.Add(cancel);
        }
        stack.Children.Add(row);
        if (preparing)
        {
            var bar = new ProgressBar
            {
                IsIndeterminate = visionProgress.Stage != "model-downloading",
                Value = visionProgress.Fraction * 100,
                Height = 4,
                MinHeight = 4
            };
            AutomationProperties.SetName(bar, T("visionTitle"));
            stack.Children.Add(bar);
        }
        else if (visionProgress.Stage is not ("vision-idle" or "local-model-ready" or "model-downloading"))
            stack.Children.Add(Text(Label(visionProgress.Stage), 12, "NomiAccent"));
        if (!vision.HasModel) stack.Children.Add(Text(T("visionNote"), 11.5, "NomiInk3"));
        var card = Glass(stack, 16);
        Mark(card, "VisionCard", T("visionTitle"));
        return card;
    }

    private Border WindowPicker()
    {
        if (!focusListed) LoadWindows();
        var stack = new StackPanel { Spacing = 12 };
        var top = new Grid();
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var eyebrow = Eyebrow(T("focusWindow"));
        eyebrow.VerticalAlignment = VerticalAlignment.Center;
        top.Children.Add(eyebrow);
        var refresh = Chip(T("focusRefresh"), "NomiGhost", "\uE72C", "FocusRefresh");
        refresh.Click += (_, _) => { LoadWindows(); BuildFocus(); };
        Grid.SetColumn(refresh, 1);
        top.Children.Add(refresh);
        stack.Children.Add(top);

        if (focusChoices.Count == 0) stack.Children.Add(Text(T("focusNoWindows"), 13, "NomiInk3"));
        var grid = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        for (var column = 0; column < 3; column++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var shown = focusChoices.Take(9).ToArray();
        for (var index = 0; index < shown.Length; index++)
        {
            if (index % 3 == 0) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var choice = shown[index];
            var selected = focusWindow?.Handle == choice.Handle;
            var content = new StackPanel { Spacing = 10 };
            content.Children.Add(Thumbnail(choice.Process));
            var label = new TextBlock { FontSize = 12.5, TextWrapping = TextWrapping.Wrap, MaxLines = 2, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = Palette("NomiInk") };
            label.Inlines.Add(new Run { Text = choice.Process, FontWeight = FontWeights.SemiBold });
            label.Inlines.Add(new Run { Text = $" — {choice.Title}" });
            content.Children.Add(label);
            var card = new Button
            {
                Content = content,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Top,
                Padding = new Thickness(10, 10, 10, 12),
                CornerRadius = new CornerRadius(16),
                Background = Palette("NomiGlassStrong"),
                BorderBrush = Palette(selected ? "NomiAccent" : "NomiGlassEdge"),
                BorderThickness = new Thickness(selected ? 2 : 1)
            };
            AutomationProperties.SetName(card, $"{choice.Process}, {choice.Title}");
            AutomationProperties.SetAutomationId(card, $"FocusWindow_{index}");
            card.Click += (_, _) =>
            {
                focusWindow = choice;
                focusMessage = "";
                BuildFocus();
            };
            Springy(card, 1.02f);
            Grid.SetRow(card, index / 3);
            Grid.SetColumn(card, index % 3);
            grid.Children.Add(card);
        }
        stack.Children.Add(grid);

        var tasks = board.Tasks.Where(task => task.Column is Columns.Todo or Columns.Doing).OrderBy(task => task.Column == Columns.Doing ? 0 : 1).ThenBy(task => task.Order).ToArray();
        var link = new ComboBox { MinWidth = 240, PlaceholderText = T("focusLinkNone"), VerticalAlignment = VerticalAlignment.Center };
        link.Items.Add(T("focusLinkNone"));
        foreach (var task in tasks) link.Items.Add(task.Title);
        var linked = Array.FindIndex(tasks, task => task.Id == (focusTaskId ?? board.Active?.Id));
        link.SelectedIndex = linked + 1;
        focusTaskId = linked >= 0 ? tasks[linked].Id : null;
        AutomationProperties.SetName(link, T("focusLink"));
        AutomationProperties.SetAutomationId(link, "FocusLink");
        link.SelectionChanged += (_, _) => focusTaskId = link.SelectedIndex <= 0 ? null : tasks[link.SelectedIndex - 1].Id;
        var row = new Grid { ColumnSpacing = 10 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var linkLabel = Text(T("focusLink"), 12.5, "NomiInk2");
        linkLabel.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(linkLabel);
        Grid.SetColumn(link, 1);
        link.HorizontalAlignment = HorizontalAlignment.Left;
        row.Children.Add(link);
        var start = Primary(T("focusStart"), "FocusStart");
        start.IsEnabled = focusWindow is not null && vision.HasModel && visionPreparation is null;
        start.Click += async (_, _) => await StartFocus();
        Grid.SetColumn(start, 2);
        row.Children.Add(start);
        stack.Children.Add(row);
        if (!vision.HasModel) stack.Children.Add(Text(T("focusNeedsVision"), 12, "NomiInk3"));
        else if (focusWindow is null && focusChoices.Count > 0) stack.Children.Add(Text(T("focusChooseHelp"), 12, "NomiInk3"));

        var picker = new Border { Child = stack };
        Mark(picker, "FocusSetup");
        return picker;
    }

    private Border Thumbnail(string process)
    {
        var sheet = process.Contains("excel", StringComparison.OrdinalIgnoreCase) || process.Contains("calc", StringComparison.OrdinalIgnoreCase);
        var lines = new Grid { Margin = new Thickness(10, 8, 10, 8), IsHitTestVisible = false };
        var ink = Palette(sheet ? "NomiSage" : "NomiInk3");
        for (var index = 0; index < 6; index++)
        {
            lines.Children.Add(new Border
            {
                Height = 1,
                Background = ink,
                Opacity = sheet ? 0.45 : 0.25,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, index * 13, sheet ? 0 : (index % 3) * 22, 0)
            });
        }
        if (sheet)
        {
            for (var index = 1; index < 5; index++)
                lines.Children.Add(new Border { Width = 1, Background = ink, Opacity = 0.45, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(index * 40, 0, 0, 0) });
        }
        var thumb = new Border
        {
            Height = 86,
            CornerRadius = new CornerRadius(10),
            Background = Palette(sheet ? "NomiSageSoft" : "NomiSurface"),
            Child = lines
        };
        AutomationProperties.SetAccessibilityView(thumb, AccessibilityView.Raw);
        return thumb;
    }

    private void LoadWindows()
    {
        focusChoices = WindowCapture.List(WindowNative.GetWindowHandle(this));
        focusListed = true;
        if (focusWindow is not null && focusChoices.All(choice => choice.Handle != focusWindow.Handle)) focusWindow = null;
    }

    private Grid Stage()
    {
        var stack = new StackPanel { Spacing = 12, Padding = new Thickness(18, 16, 18, 18) };
        var top = new Grid { ColumnSpacing = 12 };
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var dot = new Ellipse { Width = 10, Height = 10, Fill = Palette(focusState == "paused" ? "NomiInk3" : "NomiAccent"), VerticalAlignment = VerticalAlignment.Center };
        top.Children.Add(dot);
        if (focusState == "watching" && motion)
        {
            var breathe = new Microsoft.UI.Xaml.Media.Animation.Storyboard { RepeatBehavior = Microsoft.UI.Xaml.Media.Animation.RepeatBehavior.Forever, AutoReverse = true };
            breathe.Children.Add(Animate(dot, "Opacity", 1, 0.35, 1100));
            breathe.Begin();
        }
        var info = new StackPanel { Spacing = 2 };
        var status = Text(focusBusy ? T("focusAnalyzing") : focusState == "paused" ? T("focusPausedLong") : string.Format(CultureInfo.CurrentCulture, T("focusWatching"), focusWindow?.Title ?? ""), 15);
        status.FontWeight = FontWeights.SemiBold;
        status.MaxLines = 2;
        status.TextTrimming = TextTrimming.CharacterEllipsis;
        AutomationProperties.SetAutomationId(status, "FocusStatus");
        AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite);
        info.Children.Add(status);
        focusCountdown = Text(FocusFacts(), 12, "NomiInk3");
        focusCountdown.FontFamily = (FontFamily)Application.Current.Resources["NomiMono"];
        info.Children.Add(focusCountdown);
        Grid.SetColumn(info, 1);
        top.Children.Add(info);
        stack.Children.Add(top);
        if (focusBusy)
        {
            var bar = new ProgressBar { IsIndeterminate = true, Height = 3, MinHeight = 3 };
            AutomationProperties.SetName(bar, T("focusAnalyzing"));
            stack.Children.Add(bar);
        }
        var now = Chip(T("focusNow"), "NomiChip", "\uE72C", "FocusAnalyze");
        now.IsEnabled = !focusBusy && focusState == "watching";
        now.HorizontalAlignment = HorizontalAlignment.Left;
        now.Click += (_, _) => { lastPrint = null; focusWake.Release(); };
        stack.Children.Add(now);
        if (insight is null) stack.Children.Add(Text(T("focusIntro"), 12.5, "NomiInk3"));

        var title = Text($"{focusWindow?.Title ?? ""} — {focusWindow?.Process ?? ""}", 12.5, "NomiAccentInk");
        title.MaxLines = 1;
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        var bar2 = new Border { Background = Palette("NomiAccent"), Padding = new Thickness(14, 8, 14, 8), Child = title };
        var frame = new Grid();
        frame.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        frame.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        frame.Children.Add(bar2);
        Grid.SetRow(stack, 1);
        frame.Children.Add(stack);
        var window = new Border
        {
            Background = Palette("NomiGlassStrong"),
            BorderBrush = Palette("NomiGlassEdge"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            MinHeight = 400,
            Child = frame,
            Shadow = (Shadow)Application.Current.Resources["NomiShadow"],
            Translation = new System.Numerics.Vector3(0, 0, 14)
        };
        Mark(window, "FocusSession", focusWindow?.Title);
        var stage = new Grid();
        stage.Children.Add(window);
        if (insight is not null)
        {
            var hud = InsightCard(insight);
            hud.Width = 360;
            hud.HorizontalAlignment = HorizontalAlignment.Right;
            hud.VerticalAlignment = VerticalAlignment.Bottom;
            hud.Margin = new Thickness(16, 150, 16, 16);
            hud.Shadow = (Shadow)Application.Current.Resources["NomiShadow"];
            hud.Translation = new System.Numerics.Vector3(0, 0, 28);
            stage.Children.Add(hud);
        }
        return stage;
    }

    private string FocusFacts()
    {
        var elapsed = Clock((DateTimeOffset.Now - focusStarted).TotalSeconds);
        var next = focusState == "watching" && !focusBusy && nextAnalysis is { } at
            ? $" · {T("focusNext")} {Math.Max(0, (int)(at - DateTimeOffset.Now).TotalSeconds)} s"
            : "";
        return $"{elapsed} · {string.Format(CultureInfo.CurrentCulture, T("focusCount"), focusAnalyses, focusSkipped)}{next}";
    }

    private Border InsightCard(ScreenInsight value)
    {
        var stack = new StackPanel { Spacing = 12 };
        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        head.Children.Add(new FontIcon { Glyph = "\uE945", FontSize = 13, Foreground = Palette("NomiAccent") });
        head.Children.Add(Eyebrow(T("understood")));
        head.Children.Add(Text($"· {value.Seconds:0} s", 11, "NomiInk3"));
        stack.Children.Add(head);
        var task = Text(value.Task, 18);
        task.FontWeight = FontWeights.SemiBold;
        AutomationProperties.SetAutomationId(task, "InsightTask");
        stack.Children.Add(task);
        if (value.Place.Length > 0) stack.Children.Add(Text(value.Place, 12.5, "NomiInk3"));
        if (value.Next.Length > 0) stack.Children.Add(Text($"{T("nextStep")} : {value.Next}", 13, "NomiInk2"));

        var open = board.Tasks.Where(item => item.Column is Columns.Todo or Columns.Doing).OrderBy(item => item.Order).Take(9).ToArray();
        var matched = value.Match > 0 && value.Match <= open.Length ? open[value.Match - 1] : null;
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        if (matched is not null)
        {
            stack.Children.Add(Text(string.Format(CultureInfo.CurrentCulture, T("insightMatch"), matched.Title), 12.5, "NomiInk2"));
            if (matched.RunningSince is null)
            {
                var track = Chip(T("insightTrack"), "NomiChip", "\uE768", "InsightTrack");
                track.Click += (_, _) => { board.Start(matched, DateTimeOffset.Now); Refresh(); };
                actions.Children.Add(track);
            }
        }
        else
        {
            var create = Chip(T("insightCreate"), "NomiChip", "\uE710", "InsightCreate");
            create.Click += (_, _) =>
            {
                var created = board.Add(value.Task, Columns.Doing, contextId, "vision");
                if (value.Next.Length > 0) created.Steps.Add(new WorkStep { Title = value.Next });
                board.Start(created, DateTimeOffset.Now);
                ShowToast(string.Format(CultureInfo.CurrentCulture, T("taskAdded"), created.Title), T("open"), () => OpenSheet(created.Id));
                Refresh();
            };
            actions.Children.Add(create);
        }
        if (actions.Children.Count > 0) stack.Children.Add(actions);

        var issues = tableReport?.Issues ?? [];
        if (value.Check.Length > 0 || issues.Count > 0)
        {
            var check = new StackPanel { Spacing = 8, Padding = new Thickness(14, 12, 14, 12) };
            check.Children.Add(Eyebrow(T("toVerify")));
            if (value.Check.Length > 0) check.Children.Add(Text(value.Check, 13));
            foreach (var issue in issues.Take(4))
            {
                var line = Text(IssueText(issue), 13, "NomiAccent");
                line.FontFamily = (FontFamily)Application.Current.Resources["NomiMono"];
                line.FontSize = 12.5;
                check.Children.Add(line);
            }
            if (tableReport is { Rows: > 0 } report)
                check.Children.Add(Text(string.Format(CultureInfo.CurrentCulture, T("tableRead"), report.Rows), 11.5, "NomiInk3"));
            var toTask = Chip(T("anomalyToTask"), "NomiChip", "\uE9D5", "AnomalyToTask");
            toTask.Click += (_, _) =>
            {
                var title = issues.Count > 0 ? $"{T("verifyPrefix")} {IssueText(issues[0])}" : $"{T("verifyPrefix")} {value.Check}";
                var created = board.Add(Short(title, 120), Columns.Todo, contextId, "vision");
                created.Estimate = 10;
                created.Note = string.Join("\n", new[] { value.Check }.Concat(issues.Select(IssueText)).Where(item => item.Length > 0));
                board.Save();
                ShowToast(string.Format(CultureInfo.CurrentCulture, T("taskAdded"), created.Title), T("open"), () => OpenSheet(created.Id));
                BuildRail();
                BuildCompanion();
            };
            check.Children.Add(toTask);
            var border = new Border { Background = Palette("NomiAccentSoft"), CornerRadius = new CornerRadius(12), Child = check };
            Mark(border, "InsightCheck", T("toVerify"));
            stack.Children.Add(border);
        }
        var card = Glass(stack, 18);
        Mark(card, "FocusInsight");
        return card;
    }

    private string IssueText(TableIssue issue)
    {
        var culture = CultureInfo.GetCultureInfo(Current.Locale);
        return string.Format(culture, T("issueLine"), issue.Row, issue.Column,
            TableCheck.Format(issue.Expected, culture), TableCheck.Format(issue.Shown, culture));
    }

    private async Task PrepareVision(bool download)
    {
        if (visionPreparation is not null) return;
        using var controller = new CancellationTokenSource();
        visionPreparation = controller;
        visionProgress = new("model-verifying");
        if (view == "focus") BuildFocus();
        try
        {
            var progress = new Progress<ModelProgress>(value =>
            {
                if (visionPreparation != controller) return;
                var redraw = value.Stage != visionProgress.Stage || Math.Abs(value.Fraction - visionProgress.Fraction) >= 0.01;
                visionProgress = value;
                if (redraw && view == "focus") BuildFocus();
            });
            await vision.PrepareAsync(download, progress, controller.Token);
            visionProgress = new("local-model-ready", 1);
        }
        catch (OperationCanceledException)
        {
            visionProgress = new(controller.IsCancellationRequested ? "model-paused" : "model-download-error");
        }
        catch (InferenceException error) { visionProgress = new(error.Message); }
        catch (HttpRequestException) { visionProgress = new("model-download-error"); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            visionProgress = new("model-storage-error");
        }
        catch (Exception error)
        {
            StartupDiagnostics.Write($"Vision model initialization: {error.GetType().Name} (0x{error.HResult:X8})");
            visionProgress = new("local-engine-error");
        }
        finally
        {
            visionPreparation = null;
        }
        if (focusState == "idle" && vision.Ready) await vision.UnloadAsync();
        if (view == "focus") BuildFocus();
    }

    private async Task StartFocus()
    {
        if (focusWindow is null || focusState != "idle") return;
        if (!WindowCapture.Exists(focusWindow.Handle))
        {
            focusMessage = Label("focusClosed");
            focusWindow = null;
            BuildFocus();
            return;
        }
        focusState = "watching";
        focusStarted = DateTimeOffset.Now;
        focusAnalyses = focusSkipped = 0;
        focusMessage = "";
        insight = null;
        tableReport = null;
        lastPrint = null;
        if (focusTaskId is not null && board.Find(focusTaskId) is { RunningSince: null } linked) board.Start(linked, DateTimeOffset.Now);
        Refresh();
        if (!vision.Ready)
        {
            focusBusy = true;
            BuildFocus();
            await PrepareVision(false);
            focusBusy = false;
            if (!vision.Ready)
            {
                focusMessage = Label(visionProgress.Stage);
                StopFocus();
                Refresh();
                return;
            }
        }
        focusLoop = new CancellationTokenSource();
        _ = RunFocus(focusLoop.Token);
    }

    private async Task RunFocus(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (focusState == "watching" && focusWindow is { } target)
            {
                ScreenFrame? frame = null;
                try
                {
                    frame = await Task.Run(() => WindowCapture.Capture(target.Handle), token);
                    var print = frame.Thumbprint();
                    if (lastPrint is not null && ScreenFrame.Difference(lastPrint, print) < FocusChange)
                    {
                        focusSkipped++;
                    }
                    else
                    {
                        focusBusy = true;
                        if (view == "focus") BuildFocus();
                        var titles = board.Tasks.Where(item => item.Column is Columns.Todo or Columns.Doing).OrderBy(item => item.Order).Take(9).Select(item => item.Title).ToArray();
                        var result = await vision.AnalyzeAsync(frame, language, titles, token);
                        lastPrint = print;
                        insight = result;
                        tableReport = result.Rows.Count >= 2 ? TableCheck.Check(result.Rows, language) : null;
                        focusAnalyses++;
                        focusMessage = "";
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
                catch (InferenceException error)
                {
                    focusMessage = Label(error.Message);
                    if (error.Message is "focusClosed" or "focusMinimized") focusState = "paused";
                }
                catch (Exception error)
                {
                    StartupDiagnostics.Write($"Focus analysis: {error.GetType().Name} (0x{error.HResult:X8})");
                    focusMessage = Label("focusCaptureError");
                }
                finally
                {
                    if (frame is not null) Array.Clear(frame.Pixels);
                    focusBusy = false;
                }
                if (token.IsCancellationRequested) break;
                if (view == "focus") BuildFocus();
                BuildCompanion();
                BuildRail();
            }
            nextAnalysis = DateTimeOffset.Now.AddSeconds(FocusInterval);
            try { await focusWake.WaitAsync(TimeSpan.FromSeconds(FocusInterval), token); }
            catch (OperationCanceledException) { break; }
        }
    }

    private void PauseFocus(bool pause)
    {
        if (focusState == "idle") return;
        focusState = pause ? "paused" : "watching";
        if (!pause)
        {
            focusMessage = "";
            if (focusWindow is null || !WindowCapture.Exists(focusWindow.Handle))
            {
                focusMessage = Label("focusClosed");
                focusState = "paused";
            }
            else focusWake.Release();
        }
        Refresh();
    }

    private void StopFocus()
    {
        if (focusState == "idle" && focusLoop is null) return;
        focusLoop?.Cancel();
        focusLoop?.Dispose();
        focusLoop = null;
        focusState = "idle";
        focusBusy = false;
        lastPrint = null;
        insight = null;
        tableReport = null;
        nextAnalysis = null;
        _ = vision.UnloadAsync();
    }

    private void UpdateFocusPill()
    {
        if (focusState == "idle")
        {
            FocusPill.Visibility = Visibility.Collapsed;
            return;
        }
        FocusPill.Visibility = Visibility.Visible;
        FocusPillDot.Fill = Palette(focusState == "paused" ? "NomiInk3" : "NomiAccent");
        var label = focusState == "paused" ? T("focusPaused") : T("focusLive");
        FocusPillText.Text = $"{label} · {Clock((DateTimeOffset.Now - focusStarted).TotalSeconds)}";
        AutomationProperties.SetName(FocusPill, $"{label}, {focusWindow?.Title}");
        AutomationProperties.SetAutomationId(FocusPill, "FocusPill");
        if (focusCountdown is not null) focusCountdown.Text = FocusFacts();
    }

    private void FocusPillClicked(object sender, RoutedEventArgs args) => Show("focus");
}
