using System.Globalization;
using System.Net.Http;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
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
    private bool choosingWindow;
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
        var header = new StackPanel { Spacing = 4 };
        header.Children.Add(Heading(T("focus")));
        header.Children.Add(Text(T("focusIntro"), 13.5, "NomiInk2"));
        FocusBody.Children.Add(header);

        var privacy = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        privacy.Children.Add(new FontIcon { Glyph = "\uE72E", FontSize = 14, Foreground = Palette("NomiSage"), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 0, 0) });
        var privacyText = Text(T("focusPrivacy"), 12.5, "NomiInk2");
        privacyText.MaxWidth = 760;
        privacy.Children.Add(privacyText);
        var privacyCard = new Border { Background = Palette("NomiSageSoft"), CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 10, 14, 10), Child = privacy };
        AutomationProperties.SetAutomationId(privacyCard, "FocusPrivacy");
        FocusBody.Children.Add(privacyCard);

        FocusBody.Children.Add(VisionCard());
        if (focusState == "idle") FocusBody.Children.Add(ChooseCard());
        else FocusBody.Children.Add(SessionCard());
        if (insight is not null) FocusBody.Children.Add(InsightCard(insight));
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
        AutomationProperties.SetAutomationId(card, "VisionCard");
        return card;
    }

    private Border ChooseCard()
    {
        var stack = new StackPanel { Spacing = 12 };
        stack.Children.Add(Eyebrow(T("focusWindow")));
        if (focusWindow is not null)
        {
            var chosen = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            chosen.Children.Add(new FontIcon { Glyph = "\uE737", FontSize = 15, Foreground = Palette("NomiAccent") });
            var title = Text(focusWindow.Title, 14);
            title.FontWeight = FontWeights.SemiBold;
            title.MaxLines = 1;
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            chosen.Children.Add(title);
            chosen.Children.Add(Text(focusWindow.Process, 12, "NomiInk3"));
            stack.Children.Add(chosen);
        }
        else stack.Children.Add(Text(T("focusChooseHelp"), 13, "NomiInk2"));

        var tasks = board.Tasks.Where(task => task.Column is Columns.Todo or Columns.Doing).OrderBy(task => task.Column == Columns.Doing ? 0 : 1).ThenBy(task => task.Order).ToArray();
        var link = new ComboBox { MinWidth = 280, PlaceholderText = T("focusLinkNone") };
        link.Items.Add(T("focusLinkNone"));
        foreach (var task in tasks) link.Items.Add(task.Title);
        var linked = Array.FindIndex(tasks, task => task.Id == (focusTaskId ?? board.Active?.Id));
        link.SelectedIndex = linked + 1;
        focusTaskId = linked >= 0 ? tasks[linked].Id : null;
        AutomationProperties.SetName(link, T("focusLink"));
        AutomationProperties.SetAutomationId(link, "FocusLink");
        link.SelectionChanged += (_, _) => focusTaskId = link.SelectedIndex <= 0 ? null : tasks[link.SelectedIndex - 1].Id;
        stack.Children.Add(Field(T("focusLink"), link));

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var choose = Chip(focusWindow is null ? T("focusChoose") : T("focusChange"), "NomiChip", "\uE7C4", "FocusChoose");
        choose.Click += (_, _) => ListWindows();
        buttons.Children.Add(choose);
        var start = Primary(T("focusStart"), "FocusStart");
        start.IsEnabled = focusWindow is not null && vision.HasModel && visionPreparation is null;
        start.Click += async (_, _) => await StartFocus();
        buttons.Children.Add(start);
        stack.Children.Add(buttons);
        if (!vision.HasModel) stack.Children.Add(Text(T("focusNeedsVision"), 12, "NomiInk3"));

        if (choosingWindow)
        {
            var list = new StackPanel { Spacing = 4 };
            if (focusChoices.Count == 0) list.Children.Add(Text(T("focusNoWindows"), 12.5, "NomiInk3"));
            for (var index = 0; index < focusChoices.Count; index++)
            {
                var choice = focusChoices[index];
                var grid = new Grid { ColumnSpacing = 10 };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var title = Text(choice.Title, 13);
                title.MaxLines = 1;
                title.TextTrimming = TextTrimming.CharacterEllipsis;
                grid.Children.Add(title);
                var process = Text(choice.Process, 11.5, "NomiInk3");
                process.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(process, 1);
                grid.Children.Add(process);
                var item = new Button { Content = grid, Style = (Style)Application.Current.Resources["NomiExample"] };
                AutomationProperties.SetName(item, $"{choice.Title}, {choice.Process}");
                AutomationProperties.SetAutomationId(item, $"FocusWindow_{index}");
                item.Click += (_, _) =>
                {
                    focusWindow = choice;
                    choosingWindow = false;
                    focusMessage = "";
                    BuildFocus();
                };
                list.Children.Add(item);
            }
            var scroller = new ScrollViewer { Content = list, MaxHeight = 280, HorizontalScrollMode = ScrollMode.Disabled };
            stack.Children.Add(scroller);
        }
        var card = Glass(stack, 18, true);
        AutomationProperties.SetAutomationId(card, "FocusSetup");
        return card;
    }

    private void ListWindows()
    {
        focusChoices = WindowCapture.List(WindowNative.GetWindowHandle(this));
        choosingWindow = true;
        BuildFocus();
        if (FocusBody.FindName("FocusWindow_0") is Control first) first.Focus(FocusState.Programmatic);
    }

    private Border SessionCard()
    {
        var stack = new StackPanel { Spacing = 12 };
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
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var pause = Chip(focusState == "paused" ? T("resume") : T("pauseTimer"), "NomiChip", focusState == "paused" ? "\uE768" : "\uE769", "FocusPause");
        pause.Click += (_, _) => PauseFocus(focusState != "paused");
        buttons.Children.Add(pause);
        var now = Chip(T("focusNow"), "NomiChip", "\uE72C", "FocusAnalyze");
        now.IsEnabled = !focusBusy && focusState == "watching";
        now.Click += (_, _) => { lastPrint = null; focusWake.Release(); };
        buttons.Children.Add(now);
        var stop = Primary(T("focusStop"), "FocusStop");
        stop.Click += (_, _) => { StopFocus(); Refresh(); };
        buttons.Children.Add(stop);
        stack.Children.Add(buttons);
        var card = Glass(stack, 18, true);
        AutomationProperties.SetAutomationId(card, "FocusSession");
        return card;
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
            AutomationProperties.SetAutomationId(border, "InsightCheck");
            stack.Children.Add(border);
        }
        var card = Glass(stack, 18);
        AutomationProperties.SetAutomationId(card, "FocusInsight");
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
