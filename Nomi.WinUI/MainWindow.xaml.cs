using System.Globalization;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.Storage.Pickers;

namespace Nomi;

public sealed partial class MainWindow : Window
{
    private static readonly string[] RailGlyphs = ["\uE721", "\uE73E", "\uE8AB", "\uE787", "\uE8BD", "\uE8F1"];
    private static readonly Regex NumberPattern = new(
        @"(?<![\p{L}\d])[-−+]?\d(?:[\d\u00a0\u202f ]*\d)?(?:[.,]\d+)?(?:\s?(?:%|€|\$|£))?",
        RegexOptions.Compiled);
    private static readonly Regex BulletPattern = new(@"^\s*[-•*·–]\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex StepPattern = new(
        @"^\s*(?:(?:étape|step)\s+)?(\d{1,2})\s*[.):\-–—]\s+(.+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly Dictionary<string, Catalog> catalogs = new()
    {
        ["fr"] = Catalog.Load("fr"),
        ["en"] = Catalog.Load("en")
    };

    private string language = "fr";
    private string actionId = "understand";
    private string? contextId;
    private string view = "today";
    private bool summary;
    private bool reasoning;
    private int reasoningWords;
    private readonly LocalInferenceClient inference = new();
    private CancellationTokenSource? modelPreparation;
    private ModelProgress modelProgress = new("local-model-missing");
    private readonly Dictionary<string, Draft> drafts = new();
    private CancellationTokenSource? generation;
    private CancellationTokenSource? documentReading;
    private Draft? activeDraft;
    private bool updating;
    private readonly bool motion = new Windows.UI.ViewManagement.UISettings().AnimationsEnabled;
    private readonly List<HistoryEntry> history = HistoryStore.Load();
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? splashTimer;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? copyReset;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? elapsedTimer;
    private Storyboard? pulse;
    private string shownPanel = "";
    private Catalog Current => catalogs[language];
    private NomiAction Action => Current.Actions.Single(item => item.Id == actionId);
    private WorkContext? Context => Current.Contexts.SingleOrDefault(item => item.Id == contextId);
    private Draft CurrentDraft
    {
        get
        {
            var key = $"{actionId}:{contextId}";
            if (!drafts.TryGetValue(key, out var draft)) drafts[key] = draft = new Draft();
            return draft;
        }
    }

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(DragRegion);
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1280, 820));
        var icon = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "nomi.ico");
        if (System.IO.File.Exists(icon)) AppWindow.SetIcon(icon);
        var logo = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "nomi-512.png");
        if (System.IO.File.Exists(logo))
        {
            SplashLogo.Source = new BitmapImage(new Uri(logo));
            TitleLogo.Source = new BitmapImage(new Uri(logo));
        }
        Root.ActualThemeChanged += (_, _) => { ApplyChrome(); Refresh(); };
        Closed += (_, _) =>
        {
            CancelGeneration();
            modelPreparation?.Cancel();
            StopFocus();
            visionPreparation?.Cancel();
            ticker?.Stop();
            board.Save();
            _ = inference.DisposeAsync();
            _ = vision.DisposeAsync();
        };
        var submit = new KeyboardAccelerator { Key = VirtualKey.Enter, Modifiers = VirtualKeyModifiers.Control };
        submit.Invoked += async (_, args) => { args.Handled = true; await Launch(); };
        RequestBox.KeyboardAccelerators.Add(submit);
        var escape = new KeyboardAccelerator { Key = VirtualKey.Escape };
        escape.Invoked += (_, args) => { if (generation is not null) { args.Handled = true; CancelGeneration(); } };
        RequestBox.KeyboardAccelerators.Add(escape);
        var reason = new KeyboardAccelerator { Key = VirtualKey.R, Modifiers = VirtualKeyModifiers.Control };
        reason.Invoked += (_, args) =>
        {
            args.Handled = true;
            if (generation is not null || view != "workspace") return;
            reasoning = !reasoning;
            ReasoningToggle.IsChecked = reasoning;
        };
        Root.KeyboardAccelerators.Add(reason);
        ApplyChrome();
        Refresh();
        SplashTagline.Text = T("tagline");
        Root.Loaded += (_, _) => { DismissSplash(); StartAmbient(); StartTicker(); };
    }

    private void DismissSplash()
    {
        splashTimer = DispatcherQueue.CreateTimer();
        splashTimer.Interval = TimeSpan.FromMilliseconds(motion ? 650 : 150);
        splashTimer.IsRepeating = false;
        splashTimer.Tick += (_, _) =>
        {
            splashTimer.Stop();
            void Done()
            {
                Splash.Visibility = Visibility.Collapsed;
                SplashProgress.IsIndeterminate = false;
                if (inference.HasModel && !inference.Ready && modelPreparation is null) _ = PrepareModel();
            }
            if (!motion) { Done(); return; }
            var board = new Storyboard();
            board.Children.Add(Animate(Splash, "Opacity", 1, 0, 240));
            board.Completed += (_, _) => Done();
            board.Begin();
            Reveal(Body, 14);
        };
        splashTimer.Start();
    }

    private static DoubleAnimation Animate(DependencyObject target, string property, double from, double to, int duration, int delay = 0)
    {
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = new Duration(TimeSpan.FromMilliseconds(duration)),
            BeginTime = TimeSpan.FromMilliseconds(delay),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, property);
        return animation;
    }

    private void Reveal(UIElement element, double offset = 10, int delay = 0)
    {
        if (!motion) return;
        var transform = new TranslateTransform();
        element.RenderTransform = transform;
        element.Opacity = 0;
        var board = new Storyboard();
        board.Children.Add(Animate(element, "Opacity", 0, 1, 200, delay));
        board.Children.Add(Animate(transform, "Y", offset, 0, 280, delay));
        board.Completed += (_, _) => element.Opacity = 1;
        board.Begin();
    }

    private void Pulse(bool active)
    {
        if (!active || !motion)
        {
            pulse?.Stop();
            pulse = null;
            return;
        }
        if (pulse is not null) return;
        var animation = new DoubleAnimation
        {
            From = 1,
            To = 0.4,
            Duration = new Duration(TimeSpan.FromMilliseconds(900)),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        Storyboard.SetTarget(animation, Skeleton);
        Storyboard.SetTargetProperty(animation, "Opacity");
        pulse = new Storyboard();
        pulse.Children.Add(animation);
        pulse.Begin();
    }

    private string Elapsed(double seconds)
    {
        var total = (int)Math.Round(seconds);
        return total < 60
            ? $"{total} s"
            : $"{total / 60} min {total % 60:00} s";
    }

    private string T(string key) => Current.Labels[key];

    private bool Dark => Root.ActualTheme == ElementTheme.Dark;

    private Brush Palette(string key)
    {
        var nomi = Application.Current.Resources.MergedDictionaries.Last(dictionary => dictionary.ThemeDictionaries.Count > 0);
        var theme = (ResourceDictionary)nomi.ThemeDictionaries[Dark ? "Default" : "Light"];
        return (Brush)theme[key];
    }

    private void ApplyChrome()
    {
        if (SystemBackdrop is null) Root.Background = Palette("NomiCanvas");
        var bar = AppWindow.TitleBar;
        bar.ButtonBackgroundColor = Colors.Transparent;
        bar.ButtonInactiveBackgroundColor = Colors.Transparent;
        bar.ButtonForegroundColor = ((SolidColorBrush)Palette("NomiInk2")).Color;
        bar.ButtonInactiveForegroundColor = ((SolidColorBrush)Palette("NomiInk3")).Color;
        bar.ButtonHoverBackgroundColor = ((SolidColorBrush)Palette("NomiSurface2")).Color;
        bar.ButtonHoverForegroundColor = ((SolidColorBrush)Palette("NomiInk")).Color;
        foreach (var button in new[] { LaunchButton, EmptyAction, PrepareButton, TaskButton })
        {
            var accent = ((SolidColorBrush)Palette("NomiAccent")).Color;
            var hover = Dark ? Blend(accent, 0.12, Colors.White) : Blend(accent, 0.12, Colors.Black);
            var pressed = Dark ? Blend(accent, 0.22, Colors.White) : Blend(accent, 0.22, Colors.Black);
            button.Resources["ButtonBackgroundPointerOver"] = new SolidColorBrush(hover);
            button.Resources["ButtonBackgroundPressed"] = new SolidColorBrush(pressed);
            button.Resources["ButtonBorderBrushPointerOver"] = new SolidColorBrush(hover);
            button.Resources["ButtonBorderBrushPressed"] = new SolidColorBrush(pressed);
            button.Resources["ButtonForegroundPointerOver"] = Palette("NomiAccentInk");
            button.Resources["ButtonForegroundPressed"] = Palette("NomiAccentInk");
        }
    }

    private static Windows.UI.Color Blend(Windows.UI.Color color, double amount, Windows.UI.Color towards)
    {
        byte Mix(byte a, byte b) => (byte)Math.Round(a + (b - a) * amount);
        return Windows.UI.Color.FromArgb(255, Mix(color.R, towards.R), Mix(color.G, towards.G), Mix(color.B, towards.B));
    }

    private void CancelGeneration()
    {
        if (activeDraft is not null) activeDraft.Status = "stopped";
        generation?.Cancel();
        documentReading?.Cancel();
    }

    private TextBlock Text(string value, double size = 13, string ink = "NomiInk")
    {
        return new TextBlock
        {
            Text = value,
            FontSize = size,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Palette(ink)
        };
    }

    private static void Announce(TextBlock block, string value)
    {
        block.Text = value;
        block.Visibility = value.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        FrameworkElementAutomationPeer.FromElement(block)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    private void Refresh()
    {
        updating = true;
        Root.Language = Current.Locale;
        Title = $"Nomi — {ViewTitle()}";
        Tagline.Text = T("tagline");
        FrenchToggle.IsChecked = language == "fr";
        EnglishToggle.IsChecked = language == "en";
        AutomationProperties.SetName(FrenchToggle, "Français");
        AutomationProperties.SetName(EnglishToggle, "English");
        AutomationProperties.SetName(CommandButton, T("openCommand"));
        ToolTipService.SetToolTip(CommandButton, T("openCommand"));
        CommandText.Text = T("commandBar");
        WorkTitle.Text = T("work").ToUpper(CultureInfo.CurrentCulture);
        RailNoteTitle.Text = T("railNoteTitle");
        RailNoteText.Text = T("railNoteText");
        BuildRail();
        var panels = new Dictionary<string, UIElement>
        {
            ["today"] = TodayPanel,
            ["board"] = BoardPanel,
            ["focus"] = FocusPanel,
            ["workspace"] = Workspace,
            ["spaces"] = SpacesPanel,
            ["settings"] = SettingsPanel
        };
        foreach (var (key, element) in panels) element.Visibility = key == view ? Visibility.Visible : Visibility.Collapsed;
        if (view == "spaces") BuildSpaces();
        else if (view == "settings") BuildSettings();
        else if (view == "today") BuildToday();
        else if (view == "board") BuildBoard();
        else if (view == "focus") BuildFocus();
        RefreshWorkspace();
        RefreshAside();
        BuildCompanion();
        UpdateFocusPill();
        var assistant = view == "workspace" ? Visibility.Visible : Visibility.Collapsed;
        DocumentsCard.Visibility = HistoryCard.Visibility = ExamplesCard.Visibility = assistant;
        var panel = view == "workspace" ? $"workspace:{actionId}:{contextId}" : view;
        if (shownPanel.Length > 0 && panel != shownPanel) Reveal(panels[view], 10);
        shownPanel = panel;
        PaletteHelp.Text = T("commandHelp");
        PaletteEmpty.Text = T("noResults");
        PaletteSearch.PlaceholderText = T("commandHint");
        AutomationProperties.SetName(PaletteSearch, T("search"));
        AutomationProperties.SetName(PaletteResults, T("search"));
        updating = false;
    }

    private string ViewTitle() => view switch
    {
        "today" => T("today"),
        "board" => T("board"),
        "focus" => T("focus"),
        "spaces" => T("spaces"),
        "settings" => T("preferences"),
        _ => Action.Title
    };

    private void Show(string target)
    {
        if (PaletteOverlay.Visibility == Visibility.Visible) PaletteOverlay.Visibility = Visibility.Collapsed;
        view = target;
        Refresh();
    }

    private void BuildRail()
    {
        NavList.Children.Clear();
        RailActions.Children.Clear();
        RailFooter.Children.Clear();
        var views = new[]
        {
            ("today", T("today"), "\uE8BF", "NavToday"),
            ("board", T("board"), "\uF0E2", "NavBoard"),
            ("focus", T("focus"), "\uE7B3", "NavFocus"),
            ("workspace", T("assistant"), "\uE8BD", "NavAssistant")
        };
        for (var index = 0; index < views.Length; index++)
        {
            var (id, label, glyph, automation) = views[index];
            var badge = id switch
            {
                "board" => board.Tasks.Count(task => task.Column != Columns.Done).ToString(CultureInfo.CurrentCulture),
                "focus" when focusState != "idle" => "●",
                _ => $"Ctrl {index + 1}"
            };
            var button = NavButton(label, glyph, view == id, badge, true);
            AutomationProperties.SetAutomationId(button, automation);
            AutomationProperties.SetAcceleratorKey(button, $"Ctrl+{index + 1}");
            button.Click += (_, _) => Show(id);
            NavList.Children.Add(button);
        }
        ActionsTitle.Text = T("functions").ToUpper(CultureInfo.CurrentCulture);
        for (var index = 0; index < Current.Actions.Length; index++)
        {
            var action = Current.Actions[index];
            var selected = view == "workspace" && action.Id == actionId;
            var button = NavButton(action.Title, RailGlyphs[index], selected, $"Alt {index + 1}", false);
            AutomationProperties.SetAutomationId(button, $"Action_{action.Id}");
            AutomationProperties.SetHelpText(button, action.Description);
            AutomationProperties.SetAcceleratorKey(button, $"Alt+{index + 1}");
            var id = action.Id;
            button.Click += (_, _) => Open(id, contextId);
            RailActions.Children.Add(button);
        }
        var spaces = NavButton(T("spaces"), "\uE8F1", view == "spaces", null, false);
        spaces.Click += (_, _) => Show("spaces");
        RailFooter.Children.Add(spaces);
        var settings = NavButton(T("preferences"), "\uE713", view == "settings", null, false);
        settings.Click += (_, _) => Show("settings");
        RailFooter.Children.Add(settings);
    }

    private Button NavButton(string label, string glyph, bool selected, string? badge, bool primary)
    {
        var grid = new Grid { ColumnSpacing = 11 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(new FontIcon
        {
            Glyph = glyph,
            FontSize = primary ? 16 : 14,
            Foreground = Palette(selected ? "NomiAccent" : primary ? "NomiInk2" : "NomiInk3"),
            VerticalAlignment = VerticalAlignment.Center
        });
        var text = new TextBlock
        {
            Text = label,
            FontSize = primary ? 13.5 : 12.5,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal
        };
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        if (badge is not null)
        {
            var hint = new TextBlock
            {
                Text = badge,
                FontSize = 10,
                FontFamily = (FontFamily)Application.Current.Resources["NomiMono"],
                Foreground = Palette(badge == "●" ? "NomiAccent" : "NomiInk3"),
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = selected ? 1 : 0.85
            };
            Grid.SetColumn(hint, 2);
            grid.Children.Add(hint);
        }
        var button = new Button
        {
            Content = grid,
            Style = (Style)Application.Current.Resources["NomiNav"],
            MinHeight = primary ? 38 : 32,
            Padding = primary ? new Thickness(11, 8, 11, 8) : new Thickness(11, 5, 11, 5),
            Foreground = Palette(selected ? "NomiInk" : "NomiInk2"),
            Background = selected ? Palette("NomiGlassStrong") : new SolidColorBrush(Colors.Transparent),
            BorderBrush = selected ? Palette("NomiGlassEdge") : null,
            BorderThickness = new Thickness(selected ? 1 : 0)
        };
        AutomationProperties.SetName(button, label);
        if (selected) AutomationProperties.SetItemStatus(button, T("current"));
        Springy(button, 1.02f);
        return button;
    }

    private void Open(string action, string? context)
    {
        actionId = action;
        contextId = context;
        view = "workspace";
        Refresh();
        RequestBox.Focus(FocusState.Programmatic);
    }

    private void RefreshWorkspace()
    {
        var action = Action;
        var context = Context;
        var draft = CurrentDraft;
        ActionTitle.Text = T("assistant");
        ActionDescription.Text = action.Description;
        ContextTag.Visibility = context is null ? Visibility.Collapsed : Visibility.Visible;
        ContextTagText.Text = context?.Title ?? "";

        ActionChips.Children.Clear();
        AutomationProperties.SetName(ActionChips, T("action"));
        for (var index = 0; index < Current.Actions.Length; index++)
        {
            var item = Current.Actions[index];
            var chip = new ToggleButton
            {
                Content = item.Title,
                IsChecked = item.Id == actionId,
                Style = (Style)Application.Current.Resources["NomiChipToggle"],
                CornerRadius = new CornerRadius(999)
            };
            AutomationProperties.SetName(chip, $"{T("action")} : {item.Title}");
            AutomationProperties.SetHelpText(chip, item.Description);
            AutomationProperties.SetAutomationId(chip, $"Chip_{item.Id}");
            AutomationProperties.SetAcceleratorKey(chip, $"Alt+{index + 1}");
            ToolTipService.SetToolTip(chip, $"{item.Description} · Alt {index + 1}");
            var id = item.Id;
            chip.Click += (_, _) =>
            {
                chip.IsChecked = true;
                if (id != actionId) Open(id, contextId);
            };
            ActionChips.Children.Add(chip);
        }

        ContextChip.Content = $"{context?.Title ?? T("noContext")}  ▾";
        ContextChip.Foreground = Palette(context is null ? "NomiInk3" : "NomiInk");
        AutomationProperties.SetName(ContextChip, $"{T("context")} : {context?.Title ?? T("noContext")}");
        var contextMenu = new MenuFlyout { Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft };
        var none = new ToggleMenuFlyoutItem { Text = T("noContext"), IsChecked = context is null };
        none.Click += (_, _) => Open(actionId, null);
        contextMenu.Items.Add(none);
        contextMenu.Items.Add(new MenuFlyoutSeparator());
        foreach (var item in Current.Contexts)
        {
            var entry = new ToggleMenuFlyoutItem { Text = item.Title, IsChecked = item.Id == contextId };
            var id = item.Id;
            entry.Click += (_, _) => Open(actionId, id);
            contextMenu.Items.Add(entry);
        }
        ContextChip.Flyout = contextMenu;

        FormatLabel.Text = T("nextFormat");
        StepsToggle.Content = T("steps");
        SummaryToggle.Content = T("summary");
        StepsToggle.IsChecked = !summary;
        SummaryToggle.IsChecked = summary;
        AutomationProperties.SetName(StepsToggle, $"{T("response")} : {T("steps")}");
        AutomationProperties.SetName(SummaryToggle, $"{T("response")} : {T("summary")}");
        ReasoningLabel.Text = T("reasoning");
        ReasoningToggle.IsChecked = reasoning;
        AutomationProperties.SetName(ReasoningToggle, T("reasoning"));
        ToolTipService.SetToolTip(ReasoningToggle, T("reasoningHelp"));

        RequestBox.PlaceholderText = action.Prompt;
        RequestBox.Text = draft.Prompt;
        AutomationProperties.SetName(RequestBox, T("requestLabel"));
        AutomationProperties.SetHelpText(RequestBox, T("requestHelp"));
        AttachLabel.Text = T("documentButton");
        AutomationProperties.SetName(AttachButton, T("attachDocuments"));
        ToolTipService.SetToolTip(AttachButton, T("documentHelp"));
        DocumentFormats.Text = T("documentFormats");
        ExampleButton.Content = T("loadExample");
        AutomationProperties.SetName(ExampleButton, T("loadExample"));
        StopButton.Content = T("stop");
        LaunchLabel.Text = T("send");
        AutomationProperties.SetName(LaunchButton, $"{T("send")} (Ctrl+Enter)");
        ToolTipService.SetToolTip(LaunchButton, T("requestHelp"));

        ResultTag.Text = T("responseTag");
        CopyLabel.Text = T("copy");
        CopyIcon.Glyph = "\uE8C8";
        CopyIcon.Foreground = Palette("NomiInk2");
        AutomationProperties.SetName(CopyButton, T("copy"));
        RetryLabel.Text = T("retry");
        TaskLabel.Text = T("toTasks");
        AutomationProperties.SetName(TaskButton, T("toTasks"));
        ToolTipService.SetToolTip(TaskButton, T("toTasksHelp"));
        AutomationProperties.SetName(RetryButton, T("retry"));
        EditLabel.Text = T("editRequest");
        AutomationProperties.SetName(EditButton, T("editRequest"));
        AutomationProperties.SetLiveSetting(WorkingTitle, AutomationLiveSetting.Polite);
        EmptyAction.Content = T(inference.HasModel ? "loadLocalModel" : "downloadWithSize");
        AutomationProperties.SetName(EmptyAction, (string)EmptyAction.Content);
        AutomationProperties.SetLiveSetting(StatusText, AutomationLiveSetting.Polite);
        RenderDocuments(draft);
        RenderResult(draft);
        UpdateControls();
    }

    private void UpdateControls()
    {
        var busy = generation is not null;
        LaunchButton.IsEnabled = inference.Ready && !busy;
        StopButton.Visibility = busy && activeDraft == CurrentDraft ? Visibility.Visible : Visibility.Collapsed;
        ExampleButton.IsEnabled = !busy;
        RetryButton.IsEnabled = inference.Ready && !busy;
        AttachButton.IsEnabled = !busy && documentReading is null;
        StepsToggle.IsEnabled = SummaryToggle.IsEnabled = ReasoningToggle.IsEnabled = !busy;
        RequestBox.IsReadOnly = busy && activeDraft == CurrentDraft;
        var draft = CurrentDraft;
        StatusText.Foreground = Palette(draft.Status is "stopped" or "timeout" || draft.Status.Contains("error") || draft.Status.Contains("invalid") || draft.Status == "policy-blocked"
            ? "NomiAccent" : "NomiInk2");
        var message = draft.Status switch
        {
            "idle" => "",
            "reasoning" => string.Format(CultureInfo.CurrentCulture, T("reasoningProgress"), reasoningWords),
            _ => T(draft.Status)
        };
        var working = WorkingState.Visibility == Visibility.Visible;
        Announce(StatusText, working ? "" : message);
        if (working && message.Length > 0) Announce(WorkingTitle, message);
    }

    private void RenderResult(Draft draft, bool animate = false)
    {
        var hasOutput = draft.Output.Length > 0;
        var working = !hasOutput && generation is not null && activeDraft == draft;
        var wasWorking = WorkingState.Visibility == Visibility.Visible;
        WorkingState.Visibility = working ? Visibility.Visible : Visibility.Collapsed;
        WorkingBar.Visibility = working ? Visibility.Visible : Visibility.Collapsed;
        WorkingBar.IsIndeterminate = working;
        WorkingRing.IsActive = working;
        Pulse(working);
        ResultBody.Visibility = hasOutput ? Visibility.Visible : Visibility.Collapsed;
        EmptyState.Visibility = hasOutput || working ? Visibility.Collapsed : Visibility.Visible;
        if (working)
        {
            WorkingText.Text = T("workingText");
            WorkingMeta.Text = Elapsed((DateTime.UtcNow - draft.Started).TotalSeconds);
            ResultHead.Visibility = ResultFoot.Visibility = Visibility.Collapsed;
            ResultPanel.BorderBrush = Palette("NomiGlassEdge");
            ResultPanel.Background = Palette("NomiGlass");
            if (!wasWorking) Reveal(WorkingState, 8);
            return;
        }
        ResultHead.Visibility = hasOutput ? Visibility.Visible : Visibility.Collapsed;
        ResultFoot.Visibility = hasOutput ? Visibility.Visible : Visibility.Collapsed;
        if (!hasOutput)
        {
            var firstRun = !inference.Ready;
            EmptyTitle.Text = T(firstRun ? "firstRunTitle" : "emptyTitle");
            EmptyText.Text = T(firstRun ? "firstRunText" : "emptyText");
            EmptyAction.Visibility = firstRun && modelPreparation is null ? Visibility.Visible : Visibility.Collapsed;
            ResultPanel.BorderBrush = Palette("NomiGlassEdge");
            ResultPanel.Background = Palette("NomiGlassSoft");
            return;
        }
        ResultPanel.BorderBrush = Palette("NomiGlassEdge");
        ResultPanel.Background = Palette("NomiGlass");
        var seconds = draft.Seconds > 0 ? $" · {Elapsed(draft.Seconds)}" : "";
        ResultMeta.Text = $"{Action.Title} · {T(draft.Summary ? "summary" : "steps")}{(draft.Reasoned ? $" · {T("reasoning")}" : "")} · {inference.Model}{seconds}";
        ResultBody.Children.Clear();
        var paragraphs = draft.Output.Split('\n', StringSplitOptions.TrimEntries).Where(line => line.Length > 0).ToArray();
        var lastIndex = paragraphs.Length - 1;
        for (var index = 0; index < paragraphs.Length; index++)
        {
            var line = paragraphs[index];
            var step = StepPattern.Match(line);
            if (step.Success)
            {
                var row = new Grid { ColumnSpacing = 14, Padding = new Thickness(0, 10, 0, 10) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var number = new Border
                {
                    Width = 26,
                    Height = 26,
                    CornerRadius = new CornerRadius(13),
                    Background = Palette("NomiAccentSoft"),
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, 0, 0, 0),
                    Child = new TextBlock
                    {
                        Text = step.Groups[1].Value,
                        FontSize = 12,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Palette("NomiAccent"),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };
                var body = RichText(step.Groups[2].Value, 15);
                Grid.SetColumn(body, 1);
                row.Children.Add(number);
                row.Children.Add(body);
                ResultBody.Children.Add(row);
                if (index < lastIndex && StepPattern.IsMatch(paragraphs[index + 1]))
                    ResultBody.Children.Add(new Border { Height = 1, Background = Palette("NomiLine"), Margin = new Thickness(40, 0, 0, 0) });
                continue;
            }
            var bullet = BulletPattern.Match(line);
            if (bullet.Success)
            {
                var row = new Grid { ColumnSpacing = 12, Margin = new Thickness(0, 0, 0, 6) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.Children.Add(new Border
                {
                    Width = 6,
                    Height = 6,
                    CornerRadius = new CornerRadius(3),
                    Background = Palette("NomiAccent"),
                    VerticalAlignment = VerticalAlignment.Top,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 10, 0, 0)
                });
                var body = RichText(bullet.Groups[1].Value, 15);
                Grid.SetColumn(body, 1);
                row.Children.Add(body);
                ResultBody.Children.Add(row);
                continue;
            }
            if (line.EndsWith(':') && line.Length <= 80 && !IsConclusion(line))
            {
                var heading = RichText(line.TrimEnd(':').Trim('*', '#', ' '), 15);
                heading.FontWeight = FontWeights.SemiBold;
                heading.Margin = new Thickness(0, index == 0 ? 0 : 10, 0, 4);
                ResultBody.Children.Add(heading);
                continue;
            }
            var conclusion = index == lastIndex && index > 0 && paragraphs.Length > 1
                && (StepPattern.IsMatch(paragraphs[index - 1]) || IsConclusion(line));
            if (conclusion)
            {
                var block = new StackPanel { Spacing = 4 };
                block.Children.Add(new TextBlock
                {
                    Text = T("conclusion").ToUpperInvariant(),
                    FontSize = 11,
                    CharacterSpacing = 80,
                    Foreground = Palette("NomiInk3")
                });
                block.Children.Add(RichText(StripConclusion(line), 17));
                ResultBody.Children.Add(new Border
                {
                    Child = block,
                    Margin = new Thickness(0, 16, 0, 0),
                    Padding = new Thickness(16, 12, 16, 14),
                    Background = Palette("NomiSurface2"),
                    BorderBrush = Palette("NomiAccent"),
                    BorderThickness = new Thickness(3, 0, 0, 0),
                    CornerRadius = new CornerRadius(0, 8, 8, 0)
                });
                continue;
            }
            var paragraph = RichText(line, 15);
            paragraph.Margin = new Thickness(0, 0, 0, 10);
            ResultBody.Children.Add(paragraph);
        }
        var rules = draft.Rules.Length > 0
            ? $"{T("policyPrefix")} : {string.Join(" · ", draft.Rules.Select(rule => T($"policy-{rule}")))}     "
            : "";
        ResultFootText.Text = rules + T("resultReminder");
        if (!animate) return;
        for (var index = 0; index < ResultBody.Children.Count; index++)
            Reveal(ResultBody.Children[index], 10, Math.Min(index * 40, 360));
    }

    private static bool IsConclusion(string line)
    {
        var lower = line.ToLowerInvariant();
        return lower.StartsWith("conclusion", StringComparison.Ordinal)
            || lower.StartsWith("en résumé", StringComparison.Ordinal)
            || lower.StartsWith("en resume", StringComparison.Ordinal)
            || lower.StartsWith("résultat", StringComparison.Ordinal)
            || lower.StartsWith("in summary", StringComparison.Ordinal)
            || lower.StartsWith("result", StringComparison.Ordinal)
            || lower.StartsWith("bottom line", StringComparison.Ordinal);
    }

    private static string StripConclusion(string line)
    {
        var colon = line.IndexOf(':');
        return colon is > 0 and < 24 ? line[(colon + 1)..].Trim() : line;
    }

    private TextBlock RichText(string text, double size)
    {
        var block = new TextBlock
        {
            FontSize = size,
            LineHeight = size * 1.6,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
            Foreground = Palette("NomiInk")
        };
        Typography.SetNumeralAlignment(block, FontNumeralAlignment.Tabular);
        var segments = text.Split("**");
        if (segments.Length % 2 == 0) segments = [text];
        for (var segment = 0; segment < segments.Length; segment++)
        {
            var part = segments[segment];
            var strong = segment % 2 == 1;
            var position = 0;
            foreach (Match match in NumberPattern.Matches(part))
            {
                if (match.Index > position) block.Inlines.Add(Span(part[position..match.Index], strong));
                block.Inlines.Add(new Run { Text = match.Value, FontWeight = FontWeights.SemiBold });
                position = match.Index + match.Length;
            }
            if (position < part.Length) block.Inlines.Add(Span(part[position..], strong));
        }
        return block;

        static Run Span(string value, bool strong) =>
            strong ? new Run { Text = value, FontWeight = FontWeights.SemiBold } : new Run { Text = value };
    }

    private void RenderDocuments(Draft draft)
    {
        DocumentChips.Children.Clear();
        DocumentList.Children.Clear();
        DocumentsTitle.Text = T("documentsTitle");
        DocumentNote.Text = T(draft.Documents.Count > 0 ? "documentLocalNote" : "documentsNone");
        DocumentFormats.Visibility = draft.Documents.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
        foreach (var document in draft.Documents.ToArray())
        {
            var chip = new Button { Style = (Style)Application.Current.Resources["NomiChip"] };
            var chipContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            var name = new TextBlock { Text = document.Name, MaxWidth = 180, TextTrimming = TextTrimming.CharacterEllipsis };
            chipContent.Children.Add(name);
            chipContent.Children.Add(new TextBlock { Text = "×", Foreground = Palette("NomiInk3") });
            chip.Content = chipContent;
            AutomationProperties.SetName(chip, $"{T("documentRemove")} {document.Name}");
            ToolTipService.SetToolTip(chip, $"{T("documentRemove")} · {document.Name}");
            chip.Click += (_, _) => RemoveDocument(draft, document);
            DocumentChips.Children.Add(chip);

            var row = new Grid { ColumnSpacing = 10 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var extension = System.IO.Path.GetExtension(document.Name).TrimStart('.').ToUpperInvariant();
            row.Children.Add(new Border
            {
                Width = 30,
                Height = 34,
                CornerRadius = new CornerRadius(5),
                BorderBrush = Palette("NomiLineStrong"),
                BorderThickness = new Thickness(1),
                Background = Palette("NomiSurface2"),
                VerticalAlignment = VerticalAlignment.Top,
                Child = new TextBlock
                {
                    Text = extension.Length > 4 ? extension[..4] : extension,
                    FontFamily = (FontFamily)Application.Current.Resources["NomiMono"],
                    FontSize = 8.5,
                    Foreground = Palette("NomiInk2"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            });
            var details = new StackPanel { Spacing = 2 };
            var title = Text(document.Name, 13);
            title.FontWeight = FontWeights.SemiBold;
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            title.TextWrapping = TextWrapping.NoWrap;
            details.Children.Add(title);
            var count = document.Text.Length.ToString("N0", new CultureInfo(Current.Locale));
            details.Children.Add(Text(document.Truncated ? $"{count} car. · {T("documentPartialShort")}" : $"{count} car.", 11.5, "NomiInk3"));
            var preview = new TextBlock
            {
                Text = document.Text,
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true,
                Foreground = Palette("NomiInk2")
            };
            details.Children.Add(new Expander
            {
                Header = new TextBlock { Text = T("documentPreview"), FontSize = 11.5 },
                Content = new ScrollViewer { Content = preview, MaxHeight = 200 },
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 4, 0, 0)
            });
            Grid.SetColumn(details, 1);
            row.Children.Add(details);
            var remove = new Button
            {
                Content = new FontIcon { Glyph = "\uE711", FontSize = 11 },
                Style = (Style)Application.Current.Resources["NomiGhost"],
                Padding = new Thickness(6),
                VerticalAlignment = VerticalAlignment.Top
            };
            AutomationProperties.SetName(remove, $"{T("documentRemove")} {document.Name}");
            remove.Click += (_, _) => RemoveDocument(draft, document);
            Grid.SetColumn(remove, 2);
            row.Children.Add(remove);
            DocumentList.Children.Add(row);
        }
        Announce(DocumentStatus, draft.DocumentStatus.Length > 0 ? T(draft.DocumentStatus) : "");
    }

    private void RemoveDocument(Draft draft, AttachedDocument document)
    {
        if (documentReading is not null || generation is not null) return;
        draft.Documents.Remove(document);
        draft.DocumentStatus = "";
        RenderDocuments(draft);
        AttachButton.Focus(FocusState.Programmatic);
    }

    private void RefreshAside()
    {
        EngineTitle.Text = T("engineTitle");
        EngineDetail.Text = $"{inference.Model} · llama.cpp";
        AutomationProperties.SetLiveSetting(EngineStatus, AutomationLiveSetting.Polite);
        AutomationProperties.SetLiveSetting(DocumentStatus, AutomationLiveSetting.Polite);
        AutomationProperties.SetName(EngineProgress, T("engineTitle"));
        PrepareCancel.Content = T("stop");
        UpdateEngine();

        RenderHistory(false);

        ExamplesTitle.Text = $"{T("examplesTitle")} · {Action.Title}";
        ExampleList.Children.Clear();
        var prompt = new Button
        {
            Content = Text(Action.Prompt, 12.5),
            Style = (Style)Application.Current.Resources["NomiExample"]
        };
        AutomationProperties.SetName(prompt, $"{T("loadExample")} : {Action.Prompt}");
        prompt.Click += (_, _) => UseExample();
        ExampleList.Children.Add(prompt);
        foreach (var context in Current.Contexts.Where(item => item.Action == actionId))
        {
            var card = new Button
            {
                Content = Text($"{context.Title} — {context.Description}", 12.5, "NomiInk2"),
                Style = (Style)Application.Current.Resources["NomiExample"]
            };
            AutomationProperties.SetName(card, $"{T("context")} : {context.Title}");
            var id = context.Id;
            card.Click += (_, _) => Open(actionId, id);
            ExampleList.Children.Add(card);
        }
    }

    private void UpdateEngine()
    {
        var preparing = modelPreparation is not null;
        string status;
        if (inference.Ready) status = T("engineReady");
        else if (modelProgress.Stage == "model-downloading") status = $"{T("engineDownloading")} · {modelProgress.Fraction:P0}";
        else if (preparing) status = T("engineBusy");
        else status = T("engineMissing");
        Announce(EngineStatus, status);
        EngineDot.Fill = Palette(inference.Ready ? "NomiOk" : preparing ? "NomiAccent" : "NomiInk3");
        EnginePillDot.Fill = EngineDot.Fill;
        EnginePillText.Text = inference.Ready ? $"{T("engineReady")} · Qwen3 4B" : status;
        AutomationProperties.SetName(EnginePill, $"{T("engineTitle")} : {status}");
        EngineCard.Visibility = !inference.Ready || view == "workspace" ? Visibility.Visible : Visibility.Collapsed;
        EngineProgress.Visibility = preparing ? Visibility.Visible : Visibility.Collapsed;
        EngineProgress.IsIndeterminate = modelProgress.Stage != "model-downloading";
        EngineProgress.Value = modelProgress.Fraction * 100;
        var message = inference.Ready || modelProgress.Stage is "local-model-missing" or "model-downloading" ? "" : T(modelProgress.Stage);
        Announce(EngineMessage, message);
        PrepareButton.Content = T(inference.HasModel ? "loadLocalModel" : "downloadLocalModel");
        AutomationProperties.SetName(PrepareButton, (string)PrepareButton.Content);
        PrepareButton.Visibility = inference.Ready || preparing ? Visibility.Collapsed : Visibility.Visible;
        PrepareCancel.Visibility = preparing ? Visibility.Visible : Visibility.Collapsed;
        EmptyAction.Content = T(inference.HasModel ? "loadLocalModel" : "downloadWithSize");
        if (view == "workspace" && CurrentDraft.Output.Length == 0) RenderResult(CurrentDraft);
        UpdateControls();
    }

    private async Task PrepareModel()
    {
        if (modelPreparation is not null || generation is not null) return;
        using var controller = new CancellationTokenSource();
        modelPreparation = controller;
        modelProgress = new("model-verifying");
        UpdateEngine();
        try
        {
            var progress = new Progress<ModelProgress>(value =>
            {
                if (modelPreparation != controller) return;
                modelProgress = value;
                UpdateEngine();
            });
            await inference.PrepareAsync(true, progress, controller.Token);
            modelProgress = new("local-model-ready", 1);
        }
        catch (OperationCanceledException)
        {
            modelProgress = new(controller.IsCancellationRequested ? "model-paused" : "model-download-error");
        }
        catch (InferenceException error) { modelProgress = new(error.Message); }
        catch (HttpRequestException) { modelProgress = new("model-download-error"); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            modelProgress = new("model-storage-error");
        }
        catch (Exception error)
        {
            StartupDiagnostics.Write($"Local model initialization: {error.GetType().Name} (0x{error.HResult:X8})");
            modelProgress = new("local-engine-error");
        }
        finally
        {
            modelPreparation = null;
            UpdateEngine();
            if (inference.Ready) RequestBox.Focus(FocusState.Programmatic);
        }
    }

    private void UseExample()
    {
        if (generation is not null) return;
        RequestBox.Text = Action.Prompt;
        RequestBox.Focus(FocusState.Programmatic);
        RequestBox.SelectionStart = RequestBox.Text.Length;
    }

    private async Task Launch()
    {
        if (generation is not null || view != "workspace") return;
        var draft = CurrentDraft;
        var action = Action;
        var contextKey = contextId;
        void SetStatus(string value)
        {
            draft.Status = value;
            if (draft == CurrentDraft) UpdateControls();
        }
        if (!inference.Ready) { SetStatus("local-model-missing"); return; }
        if (documentReading is not null) { SetStatus("documentReading"); return; }
        if (string.IsNullOrWhiteSpace(RequestBox.Text) && draft.Documents.Count == 0)
        {
            SetStatus("invalid-request");
            RequestBox.Focus(FocusState.Programmatic);
            return;
        }
        using var controller = new CancellationTokenSource();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(reasoning ? 15 : 5));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(controller.Token, timeout.Token);
        generation = controller;
        activeDraft = draft;
        draft.Output = "";
        draft.Rules = [];
        draft.Reasoned = reasoning;
        draft.Summary = summary;
        draft.Seconds = 0;
        draft.Started = DateTime.UtcNow;
        reasoningWords = 0;
        RenderResult(draft);
        SetStatus(reasoning ? "reasoning" : "generating");
        elapsedTimer?.Stop();
        elapsedTimer = DispatcherQueue.CreateTimer();
        elapsedTimer.Interval = TimeSpan.FromSeconds(1);
        elapsedTimer.Tick += (_, _) =>
        {
            if (draft == CurrentDraft) WorkingMeta.Text = Elapsed((DateTime.UtcNow - draft.Started).TotalSeconds);
        };
        elapsedTimer.Start();
        try
        {
            var reported = 0;
            var reasoned = new StringBuilder();
            var adapted = await inference.GenerateNomiAsync(draft.Prompt, action.Id, language,
                summary ? "summary" : "steps", linked.Token, draft.Documents.ToArray(), reasoning, chunk =>
                {
                    reasoned.Append(chunk);
                    var words = reasoned.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
                    if (words - reported < 20) return;
                    reported = words;
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        if (draft.Status != "reasoning") return;
                        reasoningWords = words;
                        if (draft == CurrentDraft) UpdateControls();
                    });
                });
            linked.Token.ThrowIfCancellationRequested();
            draft.Output = adapted.Text;
            draft.Rules = adapted.Changes;
            draft.Seconds = (DateTime.UtcNow - draft.Started).TotalSeconds;
            history.Insert(0, new HistoryEntry(action.Id, contextKey, draft.Prompt, draft.Output, draft.Rules,
                draft.Reasoned, draft.Summary, DateTimeOffset.Now, draft.Seconds));
            if (history.Count > HistoryStore.Limit) history.RemoveRange(HistoryStore.Limit, history.Count - HistoryStore.Limit);
            HistoryStore.Save(history);
            RenderHistory(true);
            SetStatus(adapted.Changes.Length > 0 ? "policy-adjusted" : "policy-applied");
        }
        catch (OperationCanceledException) { SetStatus(timeout.IsCancellationRequested ? "timeout" : "stopped"); }
        catch (InferenceException exception) { SetStatus(exception.Message); }
        catch (HttpRequestException) { SetStatus("local-engine-error"); }
        catch (IOException) { SetStatus("incomplete-response"); }
        catch (JsonException) { SetStatus("invalid-response"); }
        catch (ObjectDisposedException) when (controller.IsCancellationRequested) { SetStatus("stopped"); }
        catch (Exception error)
        {
            StartupDiagnostics.Write($"Local inference: {error.GetType().Name} (0x{error.HResult:X8})");
            SetStatus("local-engine-error");
        }
        finally
        {
            generation = null;
            activeDraft = null;
            elapsedTimer?.Stop();
            if (draft == CurrentDraft && view == "workspace")
            {
                RenderResult(draft, draft.Output.Length > 0);
                UpdateControls();
                if (draft.Output.Length > 0) CopyButton.Focus(FocusState.Programmatic);
            }
        }
    }

    private string CopyResponse(string response)
    {
        try
        {
            var package = new DataPackage();
            package.SetText(response);
            Clipboard.SetContent(package);
            return T("copied");
        }
        catch (COMException)
        {
            return T("copyError");
        }
    }

    private async Task AttachDocuments()
    {
        var draft = CurrentDraft;
        void SetStatus(string code)
        {
            draft.DocumentStatus = code;
            if (draft == CurrentDraft) Announce(DocumentStatus, code.Length > 0 ? T(code) : "");
        }
        if (generation is not null || documentReading is not null) { SetStatus("document-busy"); return; }
        using var controller = new CancellationTokenSource();
        documentReading = controller;
        UpdateControls();
        try
        {
            var picker = new FileOpenPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            foreach (var extension in DocumentText.Extensions) picker.FileTypeFilter.Add(extension);
            var selection = await picker.PickMultipleFilesAsync();
            controller.Token.ThrowIfCancellationRequested();
            if (selection.Count == 0) return;
            if (selection.Count + draft.Documents.Count > DocumentText.MaxFiles)
                throw new InferenceException("document-limit");
            SetStatus("documentReading");
            foreach (var file in selection)
            {
                controller.Token.ThrowIfCancellationRequested();
                using var stream = await file.OpenStreamForReadAsync();
                if (stream.Length > DocumentText.MaxBytes) throw new InferenceException("document-size");
                var bytes = new byte[(int)stream.Length];
                await stream.ReadExactlyAsync(bytes, controller.Token);
                var document = await Task.Run(() => DocumentText.Read(file.Name, bytes, controller.Token), controller.Token);
                controller.Token.ThrowIfCancellationRequested();
                DocumentText.Validate([.. draft.Documents, document]);
                draft.Documents.Add(document);
                if (draft == CurrentDraft) RenderDocuments(draft);
            }
            SetStatus("documentReady");
        }
        catch (OperationCanceledException) { SetStatus("documentStopped"); }
        catch (InferenceException error) { SetStatus(error.Message); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or COMException)
        { SetStatus("document-unreadable"); }
        finally
        {
            documentReading = null;
            UpdateControls();
        }
    }

    private void BuildSpaces()
    {
        SpacesBody.Children.Clear();
        var heading = Text(T("spaces"), 22);
        heading.FontWeight = FontWeights.SemiBold;
        AutomationProperties.SetHeadingLevel(heading, AutomationHeadingLevel.Level1);
        SpacesBody.Children.Add(heading);
        SpacesBody.Children.Add(Text(T("spacesIntro"), 13.5, "NomiInk2"));
        var grid = new Grid { ColumnSpacing = 12, RowSpacing = 12, Margin = new Thickness(0, 8, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var index = 0; index < Current.Contexts.Length; index++)
        {
            var context = Current.Contexts[index];
            var action = Current.Actions.Single(item => item.Id == context.Action);
            if (index % 2 == 0) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var content = new StackPanel { Spacing = 8 };
            var title = Text(context.Title, 16);
            title.FontWeight = FontWeights.SemiBold;
            content.Children.Add(title);
            content.Children.Add(Text(context.Description, 13, "NomiInk2"));
            var footer = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            var chip = new Border
            {
                Background = Palette("NomiAccentSoft"),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 2, 8, 2),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = Text(action.Title, 11.5, "NomiAccent")
            };
            footer.Children.Add(chip);
            var open = Text($"{T("openSpace")} →", 12.5, "NomiInk2");
            open.HorizontalAlignment = HorizontalAlignment.Right;
            footer.Children.Add(open);
            content.Children.Add(footer);
            var card = new Button
            {
                Content = content,
                Style = (Style)Application.Current.Resources["NomiExample"],
                Background = Palette("NomiSurface"),
                Padding = new Thickness(18, 16, 18, 16),
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            AutomationProperties.SetName(card, $"{context.Title} · {action.Title}");
            AutomationProperties.SetHelpText(card, context.Description);
            var id = context.Id;
            card.Click += (_, _) => Open(action.Id, id);
            Grid.SetRow(card, index / 2);
            Grid.SetColumn(card, index % 2);
            grid.Children.Add(card);
        }
        SpacesBody.Children.Add(grid);
        SpacesBody.Children.Add(Text(T("spacesHelp"), 12, "NomiInk3"));
    }

    private void BuildSettings()
    {
        SettingsBody.Children.Clear();
        var heading = Text(T("preferences"), 22);
        heading.FontWeight = FontWeights.SemiBold;
        AutomationProperties.SetHeadingLevel(heading, AutomationHeadingLevel.Level1);
        SettingsBody.Children.Add(heading);
        SettingsBody.Children.Add(Text(T("preferencesIntro"), 13.5, "NomiInk2"));
        AddSetting(T("language"), T("languageHelp"), ["Français", "English"], language == "fr" ? 0 : 1, value =>
        {
            CancelGeneration();
            language = value == 0 ? "fr" : "en";
            Refresh();
        });
        var themeIndex = Root.RequestedTheme switch { ElementTheme.Light => 1, ElementTheme.Dark => 2, _ => 0 };
        AddSetting(T("theme"), T("themeHelp"), [T("themeSystem"), T("themeLight"), T("themeDark")], themeIndex, value =>
        {
            Root.RequestedTheme = value switch { 1 => ElementTheme.Light, 2 => ElementTheme.Dark, _ => ElementTheme.Default };
        });
        AddSetting(T("response"), T("responseHelp"), [T("steps"), T("summary")], summary ? 1 : 0, value =>
        {
            summary = value == 1;
            StepsToggle.IsChecked = !summary;
            SummaryToggle.IsChecked = summary;
        });
        var keys = new StackPanel { Spacing = 8 };
        var keysTitle = Text(T("shortcutsTitle"), 14);
        keysTitle.FontWeight = FontWeights.SemiBold;
        AutomationProperties.SetHeadingLevel(keysTitle, AutomationHeadingLevel.Level2);
        keys.Children.Add(keysTitle);
        keys.Children.Add(Text(T("shortcutsHelp"), 12, "NomiInk3"));
        foreach (var (label, combo) in new[]
        {
            (T("send"), "Ctrl ↵"), (T("shortcutPalette"), "Ctrl K"), (T("shortcutView"), "Ctrl 1–4"), (T("shortcutAction"), "Alt 1–6"),
            (T("newTask"), "Ctrl N"), (T("reasoning"), "Ctrl R"), (T("stop"), "Esc")
        })
        {
            var row = new Grid();
            row.Children.Add(Text(label, 12.5, "NomiInk2"));
            row.Children.Add(new Border
            {
                Style = (Style)Application.Current.Resources["NomiKbd"],
                HorizontalAlignment = HorizontalAlignment.Right,
                Child = new TextBlock { Text = combo, Style = (Style)Application.Current.Resources["NomiKbdText"] }
            });
            keys.Children.Add(row);
        }
        SettingsBody.Children.Add(new Border { Style = (Style)Application.Current.Resources["NomiCard"], Padding = new Thickness(18, 14, 18, 14), Child = keys });
        SettingsBody.Children.Add(Text(T("nativePreferences"), 12, "NomiInk3"));
        SettingsBody.Children.Add(Text(T("nativeLocalNotice"), 12, "NomiInk3"));
    }

    private void AddSetting(string label, string help, string[] options, int selected, Action<int> changed)
    {
        var card = new Border { Style = (Style)Application.Current.Resources["NomiCard"], Padding = new Thickness(18, 14, 18, 14) };
        var grid = new Grid { ColumnSpacing = 16 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var text = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
        var title = Text(label, 14);
        title.FontWeight = FontWeights.SemiBold;
        text.Children.Add(title);
        text.Children.Add(Text(help, 12, "NomiInk3"));
        grid.Children.Add(text);
        var select = new ComboBox { ItemsSource = options, SelectedIndex = selected, MinWidth = 170, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(select, label);
        AutomationProperties.SetHelpText(select, help);
        select.SelectionChanged += (_, _) => { if (!updating) changed(select.SelectedIndex); };
        Grid.SetColumn(select, 1);
        grid.Children.Add(select);
        card.Child = grid;
        SettingsBody.Children.Add(card);
    }

    private static string Normalize(string value)
    {
        return string.Concat(value.Normalize(NormalizationForm.FormD)
            .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark))
            .ToLowerInvariant();
    }

    private void OpenPalette()
    {
        PaletteOverlay.Visibility = Visibility.Visible;
        PaletteSearch.Text = "";
        PaletteFilter(PaletteSearch, null!);
        PaletteSearch.Focus(FocusState.Programmatic);
        Reveal(PaletteOverlay, 0);
        Reveal(PaletteCard, -10);
    }

    private void ClosePalette()
    {
        PaletteOverlay.Visibility = Visibility.Collapsed;
        if (view == "workspace") RequestBox.Focus(FocusState.Programmatic);
    }

    private void PaletteFilter(object sender, TextChangedEventArgs args)
    {
        PaletteResults.Items.Clear();
        var query = PaletteSearch.Text.Trim();
        var direct = new List<Command>();
        if (query.Length >= 2)
        {
            direct.Add(new Command($"{T("askNomi")} « {query} »", Action.Description, T("assistant"), () =>
            {
                Open(actionId, contextId);
                RequestBox.Text = query;
                RequestBox.SelectionStart = query.Length;
            }));
            direct.Add(new Command($"{T("createTask")} « {query} »", T("createTaskHelp"), T("board"), () =>
            {
                var task = board.Add(query, Columns.Todo, contextId, "palette");
                ShowToast(string.Format(CultureInfo.CurrentCulture, T("taskAdded"), task.Title), T("open"), () => OpenSheet(task.Id));
                if (view is "board" or "today") Refresh();
                else BuildCompanion();
            }));
        }
        var commands = direct.Concat(new[]
            {
                new Command(T("today"), T("todayIntro"), T("navigation"), () => Show("today")),
                new Command(T("board"), T("boardIntro"), T("navigation"), () => Show("board")),
                new Command(focusState == "idle" ? T("focusStart") : T("focus"), T("focusIntro"), T("focus"), () => Show("focus")),
                new Command(T("assistant"), T("assistantIntro"), T("navigation"), () => Show("workspace"))
            })
            .Concat(Current.Actions.Select(action =>
                new Command(action.Title, action.Description, T("action"), () => Open(action.Id, contextId))))
            .Concat(Current.Contexts.Select(context =>
                new Command(context.Title, context.Description, T("context"), () => Open(context.Action, context.Id))))
            .Append(new Command(T("spaces"), T("spacesIntro"), T("navigation"), () => { view = "spaces"; Refresh(); }))
            .Append(new Command(T("preferences"), T("preferencesIntro"), T("navigation"), () => { view = "settings"; Refresh(); }))
            .Append(new Command(T("reasoning"), T("reasoningHelp"), T("action"), () => { reasoning = !reasoning; view = "workspace"; Refresh(); }))
            .Concat(board.Tasks.Where(task => task.Column != Columns.Done).Take(12).Select(task =>
                new Command(task.Title, ColumnName(task.Column), T("task"), () => OpenSheet(task.Id))));
        var terms = Normalize(PaletteSearch.Text).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var command in commands.Where(command =>
            terms.All(term => Normalize($"{command.Title} {command.Description} {command.Kind}").Contains(term))))
        {
            var row = new Grid { ColumnSpacing = 12, Padding = new Thickness(6, 4, 6, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var text = new StackPanel { Spacing = 1 };
            var title = Text(command.Title, 14);
            text.Children.Add(title);
            var description = Text(command.Description, 12, "NomiInk3");
            description.TextWrapping = TextWrapping.NoWrap;
            description.TextTrimming = TextTrimming.CharacterEllipsis;
            text.Children.Add(description);
            row.Children.Add(text);
            var kind = Text(command.Kind, 11, "NomiInk3");
            kind.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(kind, 1);
            row.Children.Add(kind);
            var item = new ListViewItem { Content = row, Tag = command, MinHeight = 48 };
            AutomationProperties.SetName(item, $"{command.Title}, {command.Kind}");
            PaletteResults.Items.Add(item);
        }
        PaletteEmpty.Visibility = PaletteResults.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (PaletteResults.Items.Count > 0) PaletteResults.SelectedIndex = 0;
    }

    private void Choose(object? selected)
    {
        if (selected is not ListViewItem { Tag: Command command }) return;
        ClosePalette();
        command.Run();
    }

    private void PaletteKeyDown(object sender, KeyRoutedEventArgs key)
    {
        if (key.Key == VirtualKey.Escape)
        {
            ClosePalette();
            key.Handled = true;
        }
        else if (key.Key == VirtualKey.Enter)
        {
            Choose(PaletteResults.SelectedItem);
            key.Handled = true;
        }
        else if (sender == PaletteSearch && (key.Key is VirtualKey.Down or VirtualKey.Up) && PaletteResults.Items.Count > 0)
        {
            var delta = key.Key == VirtualKey.Down ? 1 : -1;
            PaletteResults.SelectedIndex = Math.Clamp(PaletteResults.SelectedIndex + delta, 0, PaletteResults.Items.Count - 1);
            PaletteResults.ScrollIntoView(PaletteResults.SelectedItem);
            key.Handled = true;
        }
    }

    private void PaletteItemClick(object sender, ItemClickEventArgs args) => Choose(args.ClickedItem);

    private void PaletteScrimTapped(object sender, TappedRoutedEventArgs args) => ClosePalette();

    private void PaletteCardTapped(object sender, TappedRoutedEventArgs args) => args.Handled = true;

    private void OpenCommandClicked(object sender, RoutedEventArgs args) => OpenPalette();

    private void CommandInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (PaletteOverlay.Visibility == Visibility.Visible) ClosePalette();
        else OpenPalette();
    }

    private void ViewShortcut(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        var index = (int)sender.Key - (int)VirtualKey.Number1;
        string[] views = ["today", "board", "focus", "workspace"];
        if (index < 0 || index >= views.Length) return;
        args.Handled = true;
        CloseSheet();
        Show(views[index]);
    }

    private void NewTaskInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        CloseSheet();
        if (view is not ("today" or "board")) Show("board");
        FocusQuickAdd();
    }

    private void ActionShortcut(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        var index = (int)sender.Key - (int)VirtualKey.Number1;
        if (index < 0 || index >= Current.Actions.Length) return;
        args.Handled = true;
        if (PaletteOverlay.Visibility == Visibility.Visible) ClosePalette();
        Open(Current.Actions[index].Id, contextId);
    }

    private void LanguageToggled(object sender, RoutedEventArgs args)
    {
        if (updating || sender is not ToggleButton { Tag: string value }) return;
        if (value == language) { ((ToggleButton)sender).IsChecked = true; return; }
        CancelGeneration();
        language = value;
        Refresh();
    }

    private void ReasoningToggled(object sender, RoutedEventArgs args)
    {
        if (updating) return;
        reasoning = ReasoningToggle.IsChecked == true;
    }

    private void FormatToggled(object sender, RoutedEventArgs args)
    {
        if (updating) return;
        summary = sender == SummaryToggle;
        StepsToggle.IsChecked = !summary;
        SummaryToggle.IsChecked = summary;
    }

    private void RequestChanged(object sender, TextChangedEventArgs args)
    {
        if (updating) return;
        CurrentDraft.Prompt = RequestBox.Text;
    }

    private void ComposerFocused(object sender, RoutedEventArgs args) => Composer.BorderBrush = Palette("NomiAccent");

    private void ComposerUnfocused(object sender, RoutedEventArgs args) => Composer.BorderBrush = Palette("NomiGlassEdge");

    private async void AttachClicked(object sender, RoutedEventArgs args) => await AttachDocuments();

    private void ExampleClicked(object sender, RoutedEventArgs args) => UseExample();

    private void StopClicked(object sender, RoutedEventArgs args) => CancelGeneration();

    private async void LaunchClicked(object sender, RoutedEventArgs args) => await Launch();

    private async void PrepareClicked(object sender, RoutedEventArgs args) => await PrepareModel();

    private void PrepareCancelClicked(object sender, RoutedEventArgs args) => modelPreparation?.Cancel();

    private void CopyClicked(object sender, RoutedEventArgs args)
    {
        var draft = CurrentDraft;
        if (draft.Output.Length == 0) return;
        var copied = CopyResponse(draft.Output) == T("copied");
        draft.Status = copied ? "copied" : "copyError";
        UpdateControls();
        if (!copied) return;
        CopyIcon.Glyph = "\uE73E";
        CopyIcon.Foreground = Palette("NomiOk");
        CopyLabel.Text = T("copiedShort");
        copyReset?.Stop();
        copyReset = DispatcherQueue.CreateTimer();
        copyReset.Interval = TimeSpan.FromSeconds(2);
        copyReset.IsRepeating = false;
        copyReset.Tick += (_, _) =>
        {
            copyReset?.Stop();
            CopyIcon.Glyph = "\uE8C8";
            CopyIcon.Foreground = Palette("NomiInk2");
            CopyLabel.Text = T("copy");
        };
        copyReset.Start();
    }

    private async void RetryClicked(object sender, RoutedEventArgs args) => await Launch();

    private void HistoryClearClicked(object sender, RoutedEventArgs args)
    {
        history.Clear();
        HistoryStore.Save(history);
        RenderHistory(false);
        RequestBox.Focus(FocusState.Programmatic);
    }

    private void RenderHistory(bool animateFirst)
    {
        HistoryTitle.Text = T("historyTitle");
        HistoryClear.Content = T("historyClear");
        AutomationProperties.SetName(HistoryClear, $"{T("historyClear")} · {T("historyTitle")}");
        HistoryClear.Visibility = history.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        HistoryNote.Text = T(history.Count > 0 ? "historyNote" : "historyEmpty");
        HistoryList.Children.Clear();
        var culture = new CultureInfo(Current.Locale);
        foreach (var entry in history.Take(6))
        {
            var title = Current.Actions.FirstOrDefault(item => item.Id == entry.Action)?.Title ?? entry.Action;
            var label = (entry.Prompt.Length > 0 ? entry.Prompt : entry.Output).ReplaceLineEndings(" ");
            var time = entry.Time.Date == DateTimeOffset.Now.Date
                ? entry.Time.ToString("t", culture)
                : entry.Time.ToString("d MMM", culture);
            var content = new Grid { RowSpacing = 2 };
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.Children.Add(new TextBlock { Text = title, FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = Palette("NomiInk2") });
            content.Children.Add(new TextBlock
            {
                Text = time,
                FontSize = 11,
                FontFamily = (FontFamily)Application.Current.Resources["NomiMono"],
                Foreground = Palette("NomiInk3"),
                HorizontalAlignment = HorizontalAlignment.Right
            });
            var prompt = new TextBlock
            {
                Text = label,
                FontSize = 12.5,
                Foreground = Palette("NomiInk"),
                TextWrapping = TextWrapping.Wrap,
                MaxLines = 2,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetRow(prompt, 1);
            content.Children.Add(prompt);
            var button = new Button { Content = content, Style = (Style)Application.Current.Resources["NomiExample"] };
            AutomationProperties.SetName(button, $"{T("historyOpen")} · {title} · {time} · {label}");
            var item = entry;
            button.Click += (_, _) => RestoreHistory(item);
            HistoryList.Children.Add(button);
        }
        if (animateFirst && HistoryList.Children.Count > 0) Reveal(HistoryList.Children[0], -6);
    }

    private void RestoreHistory(HistoryEntry entry)
    {
        if (generation is not null || Current.Actions.All(item => item.Id != entry.Action)) return;
        actionId = entry.Action;
        contextId = Current.Contexts.Any(item => item.Id == entry.Context) ? entry.Context : null;
        view = "workspace";
        var draft = CurrentDraft;
        draft.Prompt = entry.Prompt;
        draft.Output = entry.Output;
        draft.Rules = entry.Rules;
        draft.Reasoned = entry.Reasoned;
        draft.Summary = entry.Summary;
        draft.Seconds = entry.Seconds;
        draft.Status = "history-restored";
        Refresh();
        RenderResult(draft, true);
        CopyButton.Focus(FocusState.Programmatic);
    }

    private void EditClicked(object sender, RoutedEventArgs args)
    {
        RequestBox.Focus(FocusState.Programmatic);
        RequestBox.SelectionStart = RequestBox.Text.Length;
    }

    private sealed record Command(string Title, string Description, string Kind, Action Run);

    private sealed class Draft
    {
        public string Prompt { get; set; } = "";
        public string Output { get; set; } = "";
        public string Status { get; set; } = "idle";
        public string[] Rules { get; set; } = [];
        public bool Reasoned { get; set; }
        public bool Summary { get; set; }
        public double Seconds { get; set; }
        public DateTime Started { get; set; }
        public List<AttachedDocument> Documents { get; } = [];
        public string DocumentStatus { get; set; } = "";
    }
}
