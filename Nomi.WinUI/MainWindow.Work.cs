using System.Globalization;
using System.Numerics;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI.Core;

namespace Nomi;

public sealed partial class MainWindow
{
    private readonly TaskBoard board = new();
    private readonly List<(TextBlock Text, ProgressBar? Bar, string TaskId)> liveTimers = [];
    private readonly List<(TextBlock Text, ProgressBar? Bar, string TaskId)> companionTimers = [];
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? ticker;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? toastTimer;
    private Action? toastAction;
    private string? boardSpace;
    private string? sheetTaskId;
    private TextBox? quickAdd;
    private string suggestionSignature = "";
    private CancellationTokenSource? splitting;
    private IReadOnlyList<WorkStep> proposedSteps = [];
    private int tick;

    private DateTimeOffset Now => DateTimeOffset.Now;

    private string ColumnName(string column) => column switch
    {
        Columns.Doing => T("colDoing"),
        Columns.Review => T("colReview"),
        Columns.Done => T("colDone"),
        _ => T("colTodo")
    };

    private string Minutes(double minutes)
    {
        var total = (int)Math.Round(Math.Max(0, minutes));
        return total < 60 ? $"{total} min" : $"{total / 60} h {total % 60:00}";
    }

    private static string Clock(double seconds)
    {
        var total = (int)Math.Max(0, seconds);
        return total >= 3600 ? $"{total / 3600}:{total / 60 % 60:00}:{total % 60:00}" : $"{total / 60:00}:{total % 60:00}";
    }

    private string SpaceName(string? space) =>
        Current.Contexts.FirstOrDefault(item => item.Id == space)?.Title ?? T("allSpaces");

    private void StartTicker()
    {
        ticker = DispatcherQueue.CreateTimer();
        ticker.Interval = TimeSpan.FromSeconds(1);
        ticker.Tick += (_, _) => Tick();
        ticker.Start();
    }

    private void Tick()
    {
        tick++;
        var now = Now;
        foreach (var (text, bar, id) in liveTimers.Concat(companionTimers))
        {
            var task = board.Find(id);
            if (task is null) continue;
            var spent = task.Spent(now);
            text.Text = Clock(spent);
            if (bar is not null && task.Estimate > 0) bar.Value = Math.Min(100, spent / 60 / task.Estimate * 100);
        }
        if (focusState != "idle") UpdateFocusPill();
        if (tick % 30 != 0) return;
        var signature = string.Join('|', board.Suggestions(now).Select(item => item.Id));
        if (signature == suggestionSignature) return;
        suggestionSignature = signature;
        if (FocusInside(CompanionBody)) return;
        BuildCompanion();
        if (view == "today" && !FocusInside(TodayBody)) BuildToday();
    }

    private bool FocusInside(DependencyObject container)
    {
        var focused = FocusManager.GetFocusedElement(Root.XamlRoot) as DependencyObject;
        while (focused is not null)
        {
            if (focused == container) return true;
            focused = VisualTreeHelper.GetParent(focused);
        }
        return false;
    }

    private void StartAmbient()
    {
        if (!motion) return;
        var halos = new (FrameworkElement Halo, double X, double Y, int Seconds)[]
        {
            (HaloWarm, 90, 60, 23), (HaloSand, -110, 80, 29), (HaloSage, 70, -90, 31)
        };
        foreach (var (halo, x, y, seconds) in halos)
        {
            var transform = new CompositeTransform();
            halo.RenderTransform = transform;
            halo.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
            var drift = new Storyboard { RepeatBehavior = RepeatBehavior.Forever, AutoReverse = true };
            foreach (var (property, to) in new[] { ("TranslateX", x), ("TranslateY", y), ("ScaleX", 1.12), ("ScaleY", 1.08) })
            {
                var animation = new DoubleAnimation
                {
                    To = to,
                    Duration = new Duration(TimeSpan.FromSeconds(seconds)),
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };
                Storyboard.SetTarget(animation, transform);
                Storyboard.SetTargetProperty(animation, property);
                drift.Children.Add(animation);
            }
            drift.Begin();
        }
    }

    private void Springy(UIElement element, float scale)
    {
        if (!motion) return;
        element.ScaleTransition = new Vector3Transition { Duration = TimeSpan.FromMilliseconds(180) };
        if (element is FrameworkElement framework)
            framework.SizeChanged += (_, _) => element.CenterPoint = new Vector3((float)framework.ActualWidth / 2, (float)framework.ActualHeight / 2, 0);
        element.PointerEntered += (_, _) => element.Scale = new Vector3(scale, scale, 1);
        element.PointerExited += (_, _) => element.Scale = Vector3.One;
        element.PointerCanceled += (_, _) => element.Scale = Vector3.One;
        element.PointerPressed += (_, _) => element.Scale = new Vector3(1 - (scale - 1), 1 - (scale - 1), 1);
        element.PointerReleased += (_, _) => element.Scale = new Vector3(scale, scale, 1);
    }

    private static void Mark(Border card, string id)
    {
        if (card.Child is not Region region)
        {
            var content = card.Child;
            card.Child = null;
            region = new Region();
            if (content is not null) region.Children.Add(content);
            card.Child = region;
        }
        AutomationProperties.SetAutomationId(region, id);
    }

    private Border Glass(UIElement child, double padding = 18, bool raised = false)
    {
        var sheen = new Region();
        sheen.Children.Add(new Border
        {
            Background = Palette("NomiSheen"),
            CornerRadius = new CornerRadius(16, 16, 0, 0),
            Height = 46,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(-padding, -padding, -padding, 0),
            IsHitTestVisible = false
        });
        sheen.Children.Add(child);
        var card = new Border
        {
            Style = (Style)Application.Current.Resources["NomiCard"],
            Padding = new Thickness(padding),
            Child = sheen
        };
        if (raised)
        {
            card.Shadow = (Shadow)Application.Current.Resources["NomiShadow"];
            card.Translation = new Vector3(0, 0, 14);
        }
        return card;
    }

    private TextBlock Heading(string value, double size = 26)
    {
        var heading = Text(value, size);
        heading.FontWeight = FontWeights.SemiBold;
        heading.CharacterSpacing = -10;
        AutomationProperties.SetHeadingLevel(heading, size >= 22 ? AutomationHeadingLevel.Level1 : AutomationHeadingLevel.Level2);
        return heading;
    }

    private TextBlock Eyebrow(string value)
    {
        var text = Text(value.ToUpper(CultureInfo.CurrentCulture), 11, "NomiInk3");
        text.CharacterSpacing = 60;
        text.FontWeight = FontWeights.SemiBold;
        return text;
    }

    private Button Chip(string label, string style = "NomiChip", string? glyph = null, string? automation = null)
    {
        object content = label;
        if (glyph is not null)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
            row.Children.Add(new FontIcon { Glyph = glyph, FontSize = 12 });
            row.Children.Add(new TextBlock { Text = label });
            content = row;
        }
        var button = new Button { Content = content, Style = (Style)Application.Current.Resources[style] };
        AutomationProperties.SetName(button, label);
        if (automation is not null) AutomationProperties.SetAutomationId(button, automation);
        Springy(button, 1.03f);
        return button;
    }

    private Button Primary(string label, string? automation = null)
    {
        var button = Chip(label, "NomiPrimary", null, automation);
        var accent = ((SolidColorBrush)Palette("NomiAccent")).Color;
        var hover = Dark ? Blend(accent, 0.12, Colors.White) : Blend(accent, 0.12, Colors.Black);
        button.Resources["ButtonBackgroundPointerOver"] = new SolidColorBrush(hover);
        button.Resources["ButtonBackgroundPressed"] = new SolidColorBrush(hover);
        button.Resources["ButtonBorderBrushPointerOver"] = new SolidColorBrush(hover);
        button.Resources["ButtonForegroundPointerOver"] = Palette("NomiAccentInk");
        button.Resources["ButtonForegroundPressed"] = Palette("NomiAccentInk");
        return button;
    }

    private Border Pill(string value, string background = "NomiSurface2", string ink = "NomiInk2")
    {
        return new Border
        {
            Background = Palette(background),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(8, 2, 8, 3),
            Child = new TextBlock { Text = value, FontSize = 11, Foreground = Palette(ink) }
        };
    }

    private TextBox QuickAdd(string automation)
    {
        var box = new TextBox
        {
            PlaceholderText = T("quickAddHint"),
            MinWidth = 260,
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            BorderBrush = Palette("NomiGlassEdge"),
            Background = Palette("NomiGlassStrong")
        };
        AutomationProperties.SetName(box, T("quickAdd"));
        AutomationProperties.SetAutomationId(box, automation);
        box.KeyDown += (_, args) =>
        {
            if (args.Key != VirtualKey.Enter || box.Text.Trim().Length == 0) return;
            args.Handled = true;
            AddTask(box.Text);
        };
        quickAdd = box;
        return box;
    }

    private void FocusQuickAdd() => quickAdd?.Focus(FocusState.Programmatic);

    private void AddTask(string title)
    {
        var task = board.Add(title, Columns.Todo, view == "board" ? boardSpace : contextId);
        Refresh();
        FocusQuickAdd();
        ShowToast(string.Format(CultureInfo.CurrentCulture, T("taskAdded"), task.Title), T("open"), () => OpenSheet(task.Id));
    }

    private void Move(WorkTask task, string column, double? order = null)
    {
        var running = task.RunningSince is not null;
        board.Move(task, column, Now);
        if (column == Columns.Doing) board.Start(task, Now);
        if (order is { } value) { task.Order = value; board.Save(); }
        if (column == Columns.Done && running)
            ShowToast(string.Format(CultureInfo.CurrentCulture, T("taskFinished"), task.Title, Minutes(task.SpentSeconds / 60)), null, null);
    }

    // Board

    private void BuildBoard()
    {
        BoardHeader.Children.Clear();
        BoardHeader.ColumnDefinitions.Clear();
        BoardHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        BoardHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var titles = new StackPanel { Spacing = 4 };
        titles.Children.Add(Heading(T("board")));
        titles.Children.Add(Text(T("boardIntro"), 13.5, "NomiInk2"));
        var filters = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 10, 0, 0) };
        foreach (var (id, label) in new[] { ((string?)null, T("allSpaces")) }.Concat(Current.Contexts.Select(item => ((string?)item.Id, item.Title))))
        {
            var toggle = new ToggleButton
            {
                Content = label,
                IsChecked = boardSpace == id,
                Style = (Style)Application.Current.Resources["NomiChipToggle"]
            };
            AutomationProperties.SetAutomationId(toggle, $"BoardFilter_{id ?? "all"}");
            toggle.Click += (_, _) => { boardSpace = id; BuildBoard(); };
            filters.Children.Add(toggle);
        }
        titles.Children.Add(filters);
        BoardHeader.Children.Add(titles);
        var add = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Bottom };
        var box = QuickAdd("BoardQuickAdd");
        add.Children.Add(box);
        var button = Primary(T("addTask"), "BoardAdd");
        button.Click += (_, _) => { if (box.Text.Trim().Length > 0) AddTask(box.Text); else box.Focus(FocusState.Programmatic); };
        add.Children.Add(button);
        Grid.SetColumn(add, 1);
        BoardHeader.Children.Add(add);

        BoardColumns.Children.Clear();
        BoardColumns.ColumnDefinitions.Clear();
        liveTimers.Clear();
        for (var index = 0; index < Columns.All.Length; index++)
        {
            BoardColumns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var lane = BuildLane(Columns.All[index]);
            Grid.SetColumn(lane, index);
            BoardColumns.Children.Add(lane);
            Reveal(lane, 12, index * 40);
        }
    }

    private Border BuildLane(string column)
    {
        var tasks = board.Column(column, boardSpace).ToArray();
        var grid = new Grid { RowSpacing = 10 };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var head = new Grid { ColumnSpacing = 8, Padding = new Thickness(4, 0, 4, 0) };
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.Children.Add(new Ellipse
        {
            Width = 8,
            Height = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Fill = Palette(column switch { Columns.Doing => "NomiAccent", Columns.Review => "NomiSand", Columns.Done => "NomiSage", _ => "NomiInk3" })
        });
        var name = Text(ColumnName(column), 13);
        name.FontWeight = FontWeights.SemiBold;
        AutomationProperties.SetHeadingLevel(name, AutomationHeadingLevel.Level2);
        Grid.SetColumn(name, 1);
        head.Children.Add(name);
        var minutes = tasks.Where(task => column != Columns.Done).Sum(task => TaskBoard.Remaining(task, Now));
        var count = Text(column == Columns.Done || minutes == 0 ? $"{tasks.Length}" : $"{tasks.Length} · {Minutes(minutes)}", 11.5, "NomiInk3");
        count.FontFamily = (FontFamily)Application.Current.Resources["NomiMono"];
        count.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(count, 2);
        head.Children.Add(count);
        grid.Children.Add(head);

        var list = new ListView
        {
            SelectionMode = ListViewSelectionMode.None,
            IsItemClickEnabled = true,
            CanDragItems = true,
            AllowDrop = true,
            Tag = column,
            Padding = new Thickness(0),
            MinHeight = 120
        };
        AutomationProperties.SetName(list, ColumnName(column));
        AutomationProperties.SetAutomationId(list, $"Lane_{column}");
        list.ItemContainerStyle = LaneItemStyle();
        foreach (var task in tasks) list.Items.Add(TaskItem(task));
        if (tasks.Length == 0)
        {
            var empty = Text(column switch
            {
                Columns.Doing => T("laneDoingEmpty"),
                Columns.Review => T("laneReviewEmpty"),
                Columns.Done => T("laneDoneEmpty"),
                _ => T("laneTodoEmpty")
            }, 12, "NomiInk3");
            empty.Margin = new Thickness(6, 8, 6, 0);
            empty.IsHitTestVisible = false;
            Grid.SetRow(empty, 1);
            grid.Children.Add(empty);
        }
        list.ItemClick += (_, args) => { if (args.ClickedItem is ListViewItem { Tag: string id }) OpenSheet(id); };
        list.DragItemsStarting += (_, args) =>
        {
            if (args.Items.FirstOrDefault() is not ListViewItem { Tag: string id }) { args.Cancel = true; return; }
            args.Data.SetText(id);
            args.Data.RequestedOperation = DataPackageOperation.Move;
        };
        list.DragOver += (_, args) =>
        {
            args.AcceptedOperation = DataPackageOperation.Move;
            args.DragUIOverride.Caption = ColumnName(column);
            list.Background = Palette("NomiDrop");
        };
        list.DragLeave += (_, _) => list.Background = null;
        list.Drop += async (_, args) =>
        {
            list.Background = null;
            if (!args.DataView.Contains(StandardDataFormats.Text)) return;
            var deferral = args.GetDeferral();
            var id = await args.DataView.GetTextAsync();
            deferral.Complete();
            var task = board.Find(id);
            if (task is null) return;
            var position = args.GetPosition(list).Y;
            var ordered = board.Column(column, boardSpace).Where(item => item != task).ToArray();
            var before = 0;
            for (var index = 0; index < list.Items.Count; index++)
            {
                if (list.ContainerFromIndex(index) is not ListViewItem container || container.Tag as string == id) continue;
                var top = container.TransformToVisual(list).TransformPoint(new Windows.Foundation.Point(0, 0)).Y;
                if (position > top + container.ActualHeight / 2) before++;
            }
            before = Math.Min(before, ordered.Length);
            var order = ordered.Length == 0 ? 1
                : before == 0 ? ordered[0].Order - 1
                : before >= ordered.Length ? ordered[^1].Order + 1
                : (ordered[before - 1].Order + ordered[before].Order) / 2;
            Move(task, column, order);
            BuildBoard();
            BuildRail();
            BuildCompanion();
            FocusTask(id);
        };
        Grid.SetRow(list, 1);
        grid.Children.Add(list);
        return new Border { Style = (Style)Application.Current.Resources["NomiLane"], Child = grid };
    }

    private static Style LaneItemStyle()
    {
        var style = new Style(typeof(ListViewItem));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
        style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0, 0, 0, 8)));
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        style.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 0d));
        style.Setters.Add(new Setter(Control.CornerRadiusProperty, new CornerRadius(13)));
        return style;
    }

    private void FocusTask(string id)
    {
        foreach (var lane in BoardColumns.Children.OfType<Border>())
        {
            if (lane.Child is not Grid grid) continue;
            foreach (var list in grid.Children.OfType<ListView>())
                foreach (var item in list.Items.OfType<ListViewItem>())
                    if (item.Tag as string == id)
                    {
                        list.ScrollIntoView(item);
                        item.Focus(FocusState.Keyboard);
                        return;
                    }
        }
    }

    private ListViewItem TaskItem(WorkTask task)
    {
        var now = Now;
        var running = task.RunningSince is not null;
        var body = new StackPanel { Spacing = 8 };
        var top = new Grid { ColumnSpacing = 8 };
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = Text(task.Title, 13.5);
        title.FontWeight = FontWeights.SemiBold;
        title.MaxLines = 3;
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        if (task.Column == Columns.Done) title.Foreground = Palette("NomiInk3");
        top.Children.Add(title);
        if (task.Origin != "manual")
        {
            var badge = Pill(task.Origin == "vision" ? T("fromScreen") : "Nomi", "NomiAccentSoft", "NomiAccent");
            badge.VerticalAlignment = VerticalAlignment.Top;
            Grid.SetColumn(badge, 1);
            top.Children.Add(badge);
        }
        body.Children.Add(top);
        var meta = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        if (task.Space is not null) meta.Children.Add(Pill(SpaceName(task.Space)));
        meta.Children.Add(Pill($"≈ {Minutes(task.Estimate)}"));
        if (task.Due is { } due)
            meta.Children.Add(Pill(due < now && task.Column != Columns.Done ? $"{T("late")} · {due:HH:mm}" : due.Date == now.Date ? $"{T("dueAt")} {due:HH:mm}" : due.ToString("d MMM", CultureInfo.CurrentCulture),
                due < now && task.Column != Columns.Done ? "NomiAccentSoft" : "NomiSurface2", due < now && task.Column != Columns.Done ? "NomiAccent" : "NomiInk2"));
        body.Children.Add(meta);
        var spent = task.Spent(now);
        var parts = new List<string>();
        if (task.Steps.Count > 0)
        {
            var steps = new ProgressBar { Maximum = task.Steps.Count, Value = task.StepsDone, Height = 3, MinHeight = 3, CornerRadius = new CornerRadius(2) };
            AutomationProperties.SetName(steps, T("steps"));
            body.Children.Add(steps);
            parts.Add($"{task.StepsDone}/{task.Steps.Count} {T("stepsShort")}");
        }
        if (running || spent >= 60)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
            if (running) row.Children.Add(new Ellipse { Width = 7, Height = 7, Fill = Palette("NomiAccent"), VerticalAlignment = VerticalAlignment.Center });
            var clock = Text(Clock(spent), 12, running ? "NomiAccent" : "NomiInk3");
            clock.FontFamily = (FontFamily)Application.Current.Resources["NomiMono"];
            row.Children.Add(clock);
            if (parts.Count > 0) row.Children.Add(Text($"· {parts[0]}", 12, "NomiInk3"));
            body.Children.Add(row);
            if (running) liveTimers.Add((clock, null, task.Id));
        }
        else if (parts.Count > 0) body.Children.Add(Text(parts[0], 12, "NomiInk3"));

        var card = new Border
        {
            Style = (Style)Application.Current.Resources["NomiTaskCard"],
            Child = body,
            BorderBrush = running ? Palette("NomiAccent") : Palette("NomiGlassEdge")
        };
        var item = new ListViewItem { Content = card, Tag = task.Id };
        AutomationProperties.SetAutomationId(item, $"Task_{task.Id}");
        AutomationProperties.SetName(item, $"{task.Title}, {ColumnName(task.Column)}, ≈ {Minutes(task.Estimate)}{(running ? $", {T("running")}" : "")}");
        AutomationProperties.SetHelpText(item, T("taskKeysHelp"));
        item.PreviewKeyDown += (_, args) =>
        {
            var alt = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu).HasFlag(CoreVirtualKeyStates.Down);
            if (!alt || args.Key is not (VirtualKey.Left or VirtualKey.Right)) return;
            args.Handled = true;
            var index = Array.IndexOf(Columns.All, task.Column) + (args.Key == VirtualKey.Right ? 1 : -1);
            if (index < 0 || index >= Columns.All.Length) return;
            Move(task, Columns.All[index]);
            BuildBoard();
            BuildRail();
            BuildCompanion();
            FocusTask(task.Id);
            ShowToast(string.Format(CultureInfo.CurrentCulture, T("taskMoved"), task.Title, ColumnName(task.Column)), null, null);
        };
        item.ContextFlyout = TaskMenu(task);
        Springy(card, 1.015f);
        return item;
    }

    private MenuFlyout TaskMenu(WorkTask task)
    {
        var menu = new MenuFlyout();
        var open = new MenuFlyoutItem { Text = T("open"), Icon = new FontIcon { Glyph = "\uE8A7" } };
        open.Click += (_, _) => OpenSheet(task.Id);
        menu.Items.Add(open);
        var timer = new MenuFlyoutItem
        {
            Text = task.RunningSince is null ? T("startTimer") : T("pauseTimer"),
            Icon = new FontIcon { Glyph = task.RunningSince is null ? "\uE768" : "\uE769" }
        };
        timer.Click += (_, _) => { ToggleTimer(task); Refresh(); };
        menu.Items.Add(timer);
        var move = new MenuFlyoutSubItem { Text = T("moveTo") };
        foreach (var column in Columns.All.Where(column => column != task.Column))
        {
            var target = new MenuFlyoutItem { Text = ColumnName(column) };
            target.Click += (_, _) => { Move(task, column); Refresh(); };
            move.Items.Add(target);
        }
        menu.Items.Add(move);
        menu.Items.Add(new MenuFlyoutSeparator());
        var delete = new MenuFlyoutItem { Text = T("delete"), Icon = new FontIcon { Glyph = "\uE74D" } };
        delete.Click += (_, _) => DeleteTask(task);
        menu.Items.Add(delete);
        return menu;
    }

    private void DeleteTask(WorkTask task)
    {
        var copy = task;
        board.Delete(task);
        CloseSheet();
        Refresh();
        ShowToast(string.Format(CultureInfo.CurrentCulture, T("taskDeleted"), copy.Title), T("undo"), () =>
        {
            board.Tasks.Add(copy);
            board.Save();
            Refresh();
        });
    }

    private void ToggleTimer(WorkTask task)
    {
        if (task.RunningSince is null) board.Start(task, Now);
        else board.Pause(task, Now);
    }

    // Today

    private void BuildToday()
    {
        TodayBody.Children.Clear();
        liveTimers.Clear();
        var now = Now;
        var header = new Grid { ColumnSpacing = 16 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var titles = new StackPanel { Spacing = 4 };
        titles.Children.Add(Eyebrow(now.ToString("dddd d MMMM", CultureInfo.GetCultureInfo(Current.Locale))));
        titles.Children.Add(Heading(now.Hour < 12 ? T("morning") : now.Hour < 18 ? T("afternoon") : T("evening"), 30));
        titles.Children.Add(Text(T("todayIntro"), 13.5, "NomiInk2"));
        header.Children.Add(titles);
        var add = QuickAdd("TodayQuickAdd");
        add.VerticalAlignment = VerticalAlignment.Bottom;
        Grid.SetColumn(add, 1);
        header.Children.Add(add);
        TodayBody.Children.Add(header);

        var plan = board.Plan(now);
        var planned = plan.Sum(task => TaskBoard.Remaining(task, now));
        var (ratio, samples) = board.Pace();
        var endOfDay = new DateTimeOffset(now.Year, now.Month, now.Day, 18, 0, 0, now.Offset);
        var available = Math.Max(0, (endOfDay - now).TotalMinutes);
        var doneToday = board.Tasks.Where(task => task.Completed is { } done && done.Date == now.Date).ToArray();
        var tracked = board.Tasks.Where(task => task.RunningSince is not null || task.Completed?.Date == now.Date).Sum(task => task.Spent(now)) / 60;
        var metrics = new Grid { ColumnSpacing = 12 };
        var tiles = new (string Label, string Value, string Detail, string Id)[]
        {
            (T("metricPlanned"), Minutes(planned * ratio), string.Format(CultureInfo.CurrentCulture, T("metricPlannedDetail"), plan.Count), "MetricPlanned"),
            (T("metricCapacity"), Minutes(available), planned * ratio > available ? T("metricOver") : T("metricFits"), "MetricCapacity"),
            (T("metricDone"), doneToday.Length.ToString(CultureInfo.CurrentCulture), string.Format(CultureInfo.CurrentCulture, T("metricTracked"), Minutes(tracked)), "MetricDone"),
            (T("metricPace"), samples >= 3 ? $"×{ratio.ToString("0.0", CultureInfo.CurrentCulture)}" : "—", samples >= 3 ? T("metricPaceDetail") : string.Format(CultureInfo.CurrentCulture, T("metricLearning"), samples, 3), "MetricPace")
        };
        for (var index = 0; index < tiles.Length; index++)
        {
            metrics.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var (label, value, detail, id) = tiles[index];
            var stack = new StackPanel { Spacing = 3 };
            stack.Children.Add(Eyebrow(label));
            var number = Text(value, 26);
            number.FontWeight = FontWeights.SemiBold;
            number.FontFamily = (FontFamily)Application.Current.Resources["NomiMono"];
            stack.Children.Add(number);
            stack.Children.Add(Text(detail, 12, index == 1 && planned * ratio > available ? "NomiAccent" : "NomiInk3"));
            if (index == 1)
            {
                var bar = new ProgressBar { Maximum = Math.Max(1, available), Value = Math.Min(available, planned * ratio), Height = 4, MinHeight = 4, Margin = new Thickness(0, 6, 0, 0) };
                AutomationProperties.SetName(bar, T("metricCapacity"));
                stack.Children.Add(bar);
            }
            var tile = Glass(stack, 16);
            Mark(tile, id);
            AutomationProperties.SetName(tile, $"{label} : {value}, {detail}");
            Grid.SetColumn(tile, index);
            metrics.Children.Add(tile);
            Reveal(tile, 10, index * 50);
        }
        TodayBody.Children.Add(metrics);

        var columns = new Grid { ColumnSpacing = 14 };
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.35, GridUnitType.Star) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var left = new StackPanel { Spacing = 14 };
        left.Children.Add(ActiveHero(now, ratio));
        left.Children.Add(PlanCard(plan, now, ratio));
        columns.Children.Add(left);
        var right = SuggestionsCard(now);
        Grid.SetColumn(right, 1);
        columns.Children.Add(right);
        TodayBody.Children.Add(columns);
        Reveal(columns, 12, 160);
    }

    private Border ActiveHero(DateTimeOffset now, double ratio)
    {
        var task = board.Active;
        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(Eyebrow(T("activeTask")));
        if (task is null)
        {
            stack.Children.Add(Text(T("noActive"), 16));
            stack.Children.Add(Text(T("noActiveHelp"), 12.5, "NomiInk3"));
            var upcoming = board.Plan(now).FirstOrDefault();
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            if (upcoming is not null)
            {
                var start = Primary(string.Format(CultureInfo.CurrentCulture, T("startNamed"), Short(upcoming.Title, 36)), "TodayStartNext");
                start.Click += (_, _) => { board.Start(upcoming, Now); Refresh(); };
                actions.Children.Add(start);
            }
            var boardButton = Chip(T("openBoard"), "NomiChip", null, "TodayOpenBoard");
            boardButton.Click += (_, _) => Show("board");
            actions.Children.Add(boardButton);
            stack.Children.Add(actions);
            return Glass(stack, 20, true);
        }
        var title = Text(task.Title, 20);
        title.FontWeight = FontWeights.SemiBold;
        stack.Children.Add(title);
        var row = new Grid { ColumnSpacing = 18 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var clock = Text(Clock(task.Spent(now)), 34, task.RunningSince is null ? "NomiInk2" : "NomiInk");
        clock.FontFamily = (FontFamily)Application.Current.Resources["NomiMono"];
        clock.FontWeight = FontWeights.Light;
        AutomationProperties.SetAutomationId(clock, "ActiveElapsed");
        row.Children.Add(clock);
        var remaining = TaskBoard.Remaining(task, now) * ratio;
        var facts = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        facts.Children.Add(Text($"{T("estimated")} {Minutes(task.Estimate)} · {T("remaining")} {Minutes(remaining)}", 12.5, "NomiInk2"));
        facts.Children.Add(Text($"{T("likelyEnd")} {now.AddMinutes(remaining):HH:mm}", 12.5, "NomiInk2"));
        Grid.SetColumn(facts, 1);
        row.Children.Add(facts);
        stack.Children.Add(row);
        var bar = new ProgressBar { Maximum = 100, Value = task.Estimate > 0 ? Math.Min(100, task.Spent(now) / 60 / task.Estimate * 100) : 0, Height = 4, MinHeight = 4 };
        AutomationProperties.SetName(bar, T("progress"));
        stack.Children.Add(bar);
        liveTimers.Add((clock, bar, task.Id));
        var next = task.Steps.FirstOrDefault(step => !step.Done);
        if (next is not null) stack.Children.Add(Text($"{T("nextStep")} : {next.Title}", 12.5, "NomiInk2"));
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var toggle = Chip(task.RunningSince is null ? T("resume") : T("pauseTimer"), "NomiChip", task.RunningSince is null ? "\uE768" : "\uE769", "ActiveToggle");
        toggle.Click += (_, _) => { ToggleTimer(task); Refresh(); };
        buttons.Children.Add(toggle);
        var review = Chip(T("toReview"), "NomiChip", "\uE73E", "ActiveReview");
        review.Click += (_, _) => { Move(task, Columns.Review); Refresh(); };
        buttons.Children.Add(review);
        var done = Primary(T("markDone"), "ActiveDone");
        done.Click += (_, _) => { Move(task, Columns.Done); Refresh(); };
        buttons.Children.Add(done);
        var open = Chip(T("open"), "NomiGhost");
        open.Click += (_, _) => OpenSheet(task.Id);
        buttons.Children.Add(open);
        stack.Children.Add(buttons);
        var hero = Glass(stack, 20, true);
        Mark(hero, "ActiveTask");
        return hero;
    }

    private static string Short(string value, int length) => value.Length > length ? value[..length].TrimEnd() + "…" : value;

    private Border PlanCard(IReadOnlyList<WorkTask> plan, DateTimeOffset now, double ratio)
    {
        var stack = new StackPanel { Spacing = 4 };
        var head = new Grid();
        head.Children.Add(Eyebrow(T("dayPlan")));
        stack.Children.Add(head);
        if (plan.Count == 0)
        {
            stack.Children.Add(Text(T("planEmpty"), 12.5, "NomiInk3"));
            return Glass(stack, 18);
        }
        var cursor = now;
        foreach (var task in plan.Take(8))
        {
            var minutes = TaskBoard.Remaining(task, now) * ratio;
            var row = new Grid { ColumnSpacing = 12, Padding = new Thickness(0, 8, 0, 8), BorderBrush = Palette("NomiLine"), BorderThickness = new Thickness(0, 0, 0, 1) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var slot = Text($"{cursor:HH:mm} – {cursor.AddMinutes(minutes):HH:mm}", 12, task.RunningSince is null ? "NomiInk3" : "NomiAccent");
            slot.FontFamily = (FontFamily)Application.Current.Resources["NomiMono"];
            slot.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(slot);
            var info = new StackPanel { Spacing = 1 };
            var name = Text(task.Title, 13.5);
            name.MaxLines = 2;
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            info.Children.Add(name);
            info.Children.Add(Text($"{ColumnName(task.Column)} · {Minutes(minutes)}{(task.Due is { } due ? $" · {T("dueAt")} {due:HH:mm}" : "")}", 11.5, "NomiInk3"));
            Grid.SetColumn(info, 1);
            row.Children.Add(info);
            var action = Chip(task.RunningSince is null ? T("start") : T("pauseTimer"), "NomiGhost", task.RunningSince is null ? "\uE768" : "\uE769");
            AutomationProperties.SetName(action, $"{(task.RunningSince is null ? T("start") : T("pauseTimer"))} : {task.Title}");
            action.Click += (_, _) => { ToggleTimer(task); Refresh(); };
            Grid.SetColumn(action, 2);
            row.Children.Add(action);
            stack.Children.Add(row);
            cursor = cursor.AddMinutes(minutes);
        }
        var card = Glass(stack, 18);
        Mark(card, "DayPlan");
        return card;
    }

    private string SuggestionText(Suggestion suggestion) => suggestion.Kind switch
    {
        "overrun" => string.Format(CultureInfo.CurrentCulture, T("sugOverrun"), suggestion.Values),
        "due" => string.Format(CultureInfo.CurrentCulture, T("sugDue"), suggestion.Values),
        "late" => string.Format(CultureInfo.CurrentCulture, T("sugLate"), suggestion.Values),
        "pace" => string.Format(CultureInfo.CurrentCulture, T(double.Parse(suggestion.Values[2], CultureInfo.InvariantCulture) > 1 ? "sugPaceSlow" : "sugPaceFast"),
            Math.Abs(int.Parse(suggestion.Values[0], CultureInfo.InvariantCulture)), suggestion.Values[1]),
        "overload" => string.Format(CultureInfo.CurrentCulture, T("sugOverload"), suggestion.Values),
        "split" => string.Format(CultureInfo.CurrentCulture, T("sugSplit"), suggestion.Values),
        _ => ""
    };

    private string SuggestionAction(Suggestion suggestion) => suggestion.Kind switch
    {
        "overrun" => T("sugOverrunAction"),
        "due" => T("start"),
        "late" or "overload" => T("sugTomorrow"),
        "pace" => T("sugPaceAction"),
        "split" => T("splitWithNomi"),
        _ => T("apply")
    };

    private Border SuggestionsCard(DateTimeOffset now)
    {
        var stack = new StackPanel { Spacing = 10 };
        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        head.Children.Add(new FontIcon { Glyph = "\uE945", FontSize = 13, Foreground = Palette("NomiAccent") });
        head.Children.Add(Eyebrow(T("suggestions")));
        stack.Children.Add(head);
        var suggestions = board.Suggestions(now);
        suggestionSignature = string.Join('|', suggestions.Select(item => item.Id));
        if (suggestions.Count == 0)
        {
            stack.Children.Add(Text(T("noSuggestions"), 12.5, "NomiInk3"));
        }
        foreach (var suggestion in suggestions.Take(5))
        {
            var item = new StackPanel { Spacing = 8, Padding = new Thickness(12, 10, 12, 10) };
            item.Children.Add(Text(SuggestionText(suggestion), 13));
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            var apply = Chip(SuggestionAction(suggestion), "NomiChip", null, $"Suggestion_{suggestion.Kind}");
            apply.Click += async (_, _) =>
            {
                if (suggestion.Kind == "split")
                {
                    board.Dismissed.Add(suggestion.Id);
                    board.Save();
                    OpenSheet(suggestion.TaskId);
                    await SplitTask(suggestion.TaskId);
                    return;
                }
                board.Apply(suggestion, Now);
                ShowToast(T("suggestionApplied"), null, null);
                Refresh();
            };
            buttons.Children.Add(apply);
            var dismiss = Chip(T("dismiss"), "NomiGhost");
            AutomationProperties.SetName(dismiss, $"{T("dismiss")} : {SuggestionText(suggestion)}");
            dismiss.Click += (_, _) => { board.Dismissed.Add(suggestion.Id); board.Save(); Refresh(); };
            buttons.Children.Add(dismiss);
            item.Children.Add(buttons);
            stack.Children.Add(new Border { Background = Palette("NomiGlassSoft"), CornerRadius = new CornerRadius(12), Child = item });
        }
        stack.Children.Add(Text(T("suggestionsNote"), 11.5, "NomiInk3"));
        var card = Glass(stack, 18);
        Mark(card, "Suggestions");
        return card;
    }

    // Companion

    private void BuildCompanion()
    {
        companionTimers.Clear();
        CompanionBody.Children.Clear();
        var now = Now;
        var head = new Grid();
        head.Children.Add(Eyebrow(T("companion")));
        if (focusState != "idle")
        {
            var live = Pill(focusState == "paused" ? T("focusPaused") : T("focusLive"), "NomiAccentSoft", "NomiAccent");
            live.HorizontalAlignment = HorizontalAlignment.Right;
            head.Children.Add(live);
        }
        CompanionBody.Children.Add(head);
        var task = board.Active;
        if (task is null)
        {
            CompanionBody.Children.Add(Text(T("noActive"), 14));
            var open = Chip(T("openBoard"), "NomiChip", null, "CompanionBoard");
            open.Click += (_, _) => Show("board");
            CompanionBody.Children.Add(open);
        }
        else
        {
            var title = Text(task.Title, 14.5);
            title.FontWeight = FontWeights.SemiBold;
            CompanionBody.Children.Add(title);
            var clock = Text(Clock(task.Spent(now)), 26, task.RunningSince is null ? "NomiInk2" : "NomiInk");
            clock.FontFamily = (FontFamily)Application.Current.Resources["NomiMono"];
            clock.FontWeight = FontWeights.Light;
            AutomationProperties.SetAutomationId(clock, "CompanionElapsed");
            CompanionBody.Children.Add(clock);
            var bar = new ProgressBar { Maximum = 100, Height = 3, MinHeight = 3, Value = task.Estimate > 0 ? Math.Min(100, task.Spent(now) / 60 / task.Estimate * 100) : 0 };
            AutomationProperties.SetName(bar, T("progress"));
            CompanionBody.Children.Add(bar);
            companionTimers.Add((clock, bar, task.Id));
            var remaining = TaskBoard.Remaining(task, now) * board.Pace().Ratio;
            CompanionBody.Children.Add(Text($"{T("estimated")} {Minutes(task.Estimate)} · {T("likelyEnd")} {now.AddMinutes(remaining):HH:mm}", 12, "NomiInk3"));
            foreach (var step in task.Steps.Where(step => !step.Done).Take(3))
            {
                var check = new CheckBox { Content = step.Title, FontSize = 12.5, MinHeight = 28, Padding = new Thickness(6, 4, 0, 0) };
                check.Checked += (_, _) => { step.Done = true; board.Save(); BuildCompanion(); };
                CompanionBody.Children.Add(check);
            }
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            var toggle = Chip(task.RunningSince is null ? T("resume") : T("pauseTimer"), "NomiChip", task.RunningSince is null ? "\uE768" : "\uE769", "CompanionToggle");
            toggle.Click += (_, _) => { ToggleTimer(task); Refresh(); };
            buttons.Children.Add(toggle);
            var done = Chip(T("markDone"), "NomiChip", "\uE73E", "CompanionDone");
            done.Click += (_, _) => { Move(task, Columns.Done); Refresh(); };
            buttons.Children.Add(done);
            CompanionBody.Children.Add(buttons);
        }
        if (insight is not null)
        {
            CompanionBody.Children.Add(new Border { Height = 1, Background = Palette("NomiLine"), Margin = new Thickness(0, 4, 0, 4) });
            CompanionBody.Children.Add(Eyebrow(T("understood")));
            CompanionBody.Children.Add(Text(insight.Task, 12.5, "NomiInk2"));
            if (insight.Check.Length > 0) CompanionBody.Children.Add(Text($"{T("toVerify")} : {insight.Check}", 12.5, "NomiAccent"));
        }
    }

    // Sheet

    private void OpenSheet(string id)
    {
        var task = board.Find(id);
        if (task is null) return;
        sheetTaskId = id;
        proposedSteps = [];
        BuildSheet(task);
        SheetOverlay.Visibility = Visibility.Visible;
        if (motion)
        {
            var transform = new TranslateTransform();
            SheetCard.RenderTransform = transform;
            var story = new Storyboard();
            story.Children.Add(Animate(SheetCard, "Opacity", 0, 1, 180));
            story.Children.Add(Animate(transform, "X", 36, 0, 260));
            story.Begin();
        }
    }

    private void CloseSheet()
    {
        if (SheetOverlay.Visibility != Visibility.Visible) return;
        splitting?.Cancel();
        SheetOverlay.Visibility = Visibility.Collapsed;
        var id = sheetTaskId;
        sheetTaskId = null;
        Refresh();
        if (id is not null && view == "board") FocusTask(id);
    }

    private void SheetScrimTapped(object sender, TappedRoutedEventArgs args) => CloseSheet();

    private void SheetCardTapped(object sender, TappedRoutedEventArgs args) => args.Handled = true;

    private void SheetKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key != VirtualKey.Escape) return;
        args.Handled = true;
        CloseSheet();
    }

    private void BuildSheet(WorkTask task)
    {
        SheetBody.Children.Clear();
        var head = new Grid();
        head.Children.Add(Eyebrow($"{T("task")} · {ColumnName(task.Column)}"));
        var close = new Button { Content = new FontIcon { Glyph = "\uE711", FontSize = 12 }, Style = (Style)Application.Current.Resources["NomiGhost"], HorizontalAlignment = HorizontalAlignment.Right, Padding = new Thickness(8) };
        AutomationProperties.SetName(close, T("close"));
        AutomationProperties.SetAutomationId(close, "SheetClose");
        close.Click += (_, _) => CloseSheet();
        head.Children.Add(close);
        SheetBody.Children.Add(head);

        var title = new TextBox { Text = task.Title, FontSize = 18, TextWrapping = TextWrapping.Wrap, AcceptsReturn = false, BorderThickness = new Thickness(0, 0, 0, 1) };
        AutomationProperties.SetName(title, T("taskTitle"));
        AutomationProperties.SetAutomationId(title, "SheetTitle");
        title.LostFocus += (_, _) =>
        {
            if (title.Text.Trim().Length == 0) { title.Text = task.Title; return; }
            task.Title = title.Text.Trim();
            board.Save();
        };
        SheetBody.Children.Add(title);

        var columns = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        foreach (var column in Columns.All)
        {
            var toggle = new ToggleButton { Content = ColumnName(column), IsChecked = task.Column == column, Style = (Style)Application.Current.Resources["NomiChipToggle"] };
            AutomationProperties.SetAutomationId(toggle, $"SheetColumn_{column}");
            toggle.Click += (_, _) => { Move(task, column); BuildSheet(task); };
            columns.Children.Add(toggle);
        }
        SheetBody.Children.Add(Field(T("status"), columns));

        var numbers = new Grid { ColumnSpacing = 10 };
        numbers.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        numbers.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var estimate = new NumberBox { Value = task.Estimate, Minimum = 5, Maximum = 960, SmallChange = 5, LargeChange = 15, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        AutomationProperties.SetName(estimate, T("estimateMinutes"));
        AutomationProperties.SetAutomationId(estimate, "SheetEstimate");
        estimate.ValueChanged += (_, args) =>
        {
            if (double.IsNaN(args.NewValue)) return;
            task.Estimate = (int)Math.Clamp(args.NewValue, 5, 960);
            board.Save();
        };
        numbers.Children.Add(Field(T("estimateMinutes"), estimate));
        var space = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        space.Items.Add(T("allSpaces"));
        foreach (var context in Current.Contexts) space.Items.Add(context.Title);
        space.SelectedIndex = Array.FindIndex(Current.Contexts, context => context.Id == task.Space) + 1;
        AutomationProperties.SetName(space, T("space"));
        space.SelectionChanged += (_, _) =>
        {
            task.Space = space.SelectedIndex <= 0 ? null : Current.Contexts[space.SelectedIndex - 1].Id;
            board.Save();
        };
        var spaceField = Field(T("space"), space);
        Grid.SetColumn(spaceField, 1);
        numbers.Children.Add(spaceField);
        SheetBody.Children.Add(numbers);

        var dueRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var date = new CalendarDatePicker { Date = task.Due, PlaceholderText = T("noDue"), MinWidth = 160 };
        AutomationProperties.SetName(date, T("due"));
        var time = new TimePicker { Time = task.Due?.TimeOfDay ?? new TimeSpan(17, 0, 0), MinuteIncrement = 15, ClockIdentifier = Current.Locale.StartsWith("fr", StringComparison.Ordinal) ? "24HourClock" : "12HourClock" };
        AutomationProperties.SetName(time, T("dueTime"));
        void UpdateDue()
        {
            task.Due = date.Date is { } day ? new DateTimeOffset(day.Date.Add(time.Time), DateTimeOffset.Now.Offset) : null;
            board.Save();
        }
        date.DateChanged += (_, _) => UpdateDue();
        time.TimeChanged += (_, _) => UpdateDue();
        dueRow.Children.Add(date);
        dueRow.Children.Add(time);
        SheetBody.Children.Add(Field(T("due"), dueRow));

        var tracking = new Grid { ColumnSpacing = 10 };
        tracking.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        tracking.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var clock = Text(Clock(task.Spent(Now)), 22, task.RunningSince is null ? "NomiInk2" : "NomiAccent");
        clock.FontFamily = (FontFamily)Application.Current.Resources["NomiMono"];
        tracking.Children.Add(clock);
        liveTimers.Add((clock, null, task.Id));
        var timer = Chip(task.RunningSince is null ? T("startTimer") : T("pauseTimer"), "NomiChip", task.RunningSince is null ? "\uE768" : "\uE769", "SheetTimer");
        timer.Click += (_, _) => { ToggleTimer(task); BuildSheet(task); };
        Grid.SetColumn(timer, 1);
        tracking.Children.Add(timer);
        SheetBody.Children.Add(Field(T("tracked"), tracking));

        var steps = new StackPanel { Spacing = 2 };
        foreach (var step in task.Steps)
        {
            var row = new Grid { ColumnSpacing = 6 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var check = new CheckBox { Content = step.Title, IsChecked = step.Done, MinHeight = 30 };
            check.Click += (_, _) => { step.Done = check.IsChecked == true; board.Save(); };
            row.Children.Add(check);
            if (step.Minutes > 0)
            {
                var minutes = Text($"{step.Minutes} min", 11.5, "NomiInk3");
                minutes.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(minutes, 1);
                row.Children.Add(minutes);
            }
            var remove = new Button { Content = new FontIcon { Glyph = "\uE711", FontSize = 10 }, Style = (Style)Application.Current.Resources["NomiGhost"], Padding = new Thickness(6) };
            AutomationProperties.SetName(remove, $"{T("delete")} : {step.Title}");
            remove.Click += (_, _) => { task.Steps.Remove(step); board.Save(); BuildSheet(task); };
            Grid.SetColumn(remove, 2);
            row.Children.Add(remove);
            steps.Children.Add(row);
        }
        var addStep = new TextBox { PlaceholderText = T("addStep"), Margin = new Thickness(0, 4, 0, 0) };
        AutomationProperties.SetName(addStep, T("addStep"));
        addStep.KeyDown += (_, args) =>
        {
            if (args.Key != VirtualKey.Enter || addStep.Text.Trim().Length == 0) return;
            args.Handled = true;
            task.Steps.Add(new WorkStep { Title = addStep.Text.Trim() });
            board.Save();
            BuildSheet(task);
        };
        steps.Children.Add(addStep);
        SheetBody.Children.Add(Field(T("steps"), steps));

        var split = new StackPanel { Spacing = 8 };
        if (proposedSteps.Count > 0)
        {
            var proposal = new StackPanel { Spacing = 4, Padding = new Thickness(12, 10, 12, 10) };
            proposal.Children.Add(Eyebrow(T("proposedSteps")));
            foreach (var step in proposedSteps)
                proposal.Children.Add(Text(step.Minutes > 0 ? $"· {step.Title} — {step.Minutes} min" : $"· {step.Title}", 12.5, "NomiInk2"));
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 6, 0, 0) };
            var keep = Primary(T("addSteps"), "SheetKeepSteps");
            keep.Click += (_, _) =>
            {
                task.Steps.AddRange(proposedSteps);
                var sum = proposedSteps.Sum(step => step.Minutes);
                if (sum > 0) task.Estimate = Math.Max(5, sum);
                proposedSteps = [];
                board.Save();
                BuildSheet(task);
            };
            buttons.Children.Add(keep);
            var drop = Chip(T("dismiss"), "NomiGhost");
            drop.Click += (_, _) => { proposedSteps = []; BuildSheet(task); };
            buttons.Children.Add(drop);
            proposal.Children.Add(buttons);
            split.Children.Add(new Border { Background = Palette("NomiAccentSoft"), CornerRadius = new CornerRadius(12), Child = proposal });
        }
        else
        {
            var ai = Chip(splitting is null ? T("splitWithNomi") : T("splitting"), "NomiChip", "\uE945", "SheetSplit");
            ai.IsEnabled = splitting is null;
            ai.Click += async (_, _) => await SplitTask(task.Id);
            split.Children.Add(ai);
            if (!inference.Ready && !inference.HasModel) split.Children.Add(Text(T("splitNeedsModel"), 11.5, "NomiInk3"));
        }
        SheetBody.Children.Add(split);

        var note = new TextBox { Text = task.Note, PlaceholderText = T("noteHint"), AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 72 };
        AutomationProperties.SetName(note, T("note"));
        note.LostFocus += (_, _) => { task.Note = note.Text; board.Save(); };
        SheetBody.Children.Add(Field(T("note"), note));

        var footer = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var delete = Chip(T("delete"), "NomiGhost", "\uE74D", "SheetDelete");
        delete.Click += (_, _) => DeleteTask(task);
        footer.Children.Add(delete);
        SheetBody.Children.Add(footer);
        title.Focus(FocusState.Programmatic);
    }

    private StackPanel Field(string label, UIElement content)
    {
        var field = new StackPanel { Spacing = 6 };
        field.Children.Add(Eyebrow(label));
        field.Children.Add(content);
        return field;
    }

    private async Task SplitTask(string id)
    {
        var task = board.Find(id);
        if (task is null || splitting is not null) return;
        if (!inference.Ready)
        {
            if (!inference.HasModel)
            {
                ShowToast(T("splitNeedsModel"), T("assistant"), () => { CloseSheet(); Show("workspace"); });
                return;
            }
            await PrepareModel();
            if (!inference.Ready) return;
        }
        splitting = new CancellationTokenSource();
        if (sheetTaskId == id) BuildSheet(task);
        try
        {
            var prompt = string.Format(CultureInfo.CurrentCulture, T("splitPrompt"), task.Title, task.Estimate,
                task.Note.Length > 0 ? task.Note : "—");
            var result = await inference.GenerateNomiAsync(prompt, "plan", language, "steps", splitting.Token,
                instruction: T("splitInstruction"));
            proposedSteps = result.Accepted ? TaskBoard.ParseSteps(result.Text) : [];
            if (proposedSteps.Count == 0) ShowToast(T("splitEmpty"), null, null);
        }
        catch (OperationCanceledException)
        {
            proposedSteps = [];
        }
        catch (InferenceException)
        {
            proposedSteps = [];
            ShowToast(T("splitEmpty"), null, null);
        }
        finally
        {
            splitting.Dispose();
            splitting = null;
        }
        if (sheetTaskId == id && board.Find(id) is { } current) BuildSheet(current);
    }

    // Answer to tasks

    private void TaskClicked(object sender, RoutedEventArgs args)
    {
        var draft = CurrentDraft;
        if (draft.Output.Length == 0) return;
        var steps = TaskBoard.ParseSteps(draft.Output);
        var title = draft.Prompt.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? Action.Title;
        var task = board.Add(Short(title, 90), Columns.Todo, contextId, "answer");
        task.Steps = steps;
        var minutes = steps.Sum(step => step.Minutes);
        if (minutes > 0) task.Estimate = Math.Max(5, minutes);
        task.Note = Short(draft.Output, 1200);
        board.Save();
        BuildRail();
        BuildCompanion();
        ShowToast(string.Format(CultureInfo.CurrentCulture, T("answerToTask"), steps.Count), T("open"), () => OpenSheet(task.Id));
    }

    // Toast

    private void ShowToast(string message, string? action, Action? run)
    {
        ToastText.Text = message;
        toastAction = run;
        ToastAction.Visibility = action is null ? Visibility.Collapsed : Visibility.Visible;
        ToastAction.Content = action ?? "";
        if (action is not null) AutomationProperties.SetName(ToastAction, action);
        Toast.Visibility = Visibility.Visible;
        Announce(ToastText, message);
        if (motion) Reveal(Toast, 16);
        toastTimer ??= DispatcherQueue.CreateTimer();
        toastTimer.Stop();
        toastTimer.Interval = TimeSpan.FromSeconds(action is null ? 3.5 : 6);
        toastTimer.Tick += HideToast;
        toastTimer.Start();
    }

    private void HideToast(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args)
    {
        sender.Tick -= HideToast;
        sender.Stop();
        Toast.Visibility = Visibility.Collapsed;
        toastAction = null;
    }

    private void ToastActionClicked(object sender, RoutedEventArgs args)
    {
        var run = toastAction;
        Toast.Visibility = Visibility.Collapsed;
        toastAction = null;
        run?.Invoke();
    }
}
