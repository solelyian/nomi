using System.Globalization;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Nomi;

public sealed partial class MainWindow
{
    private Border Tile(string label, string glyph, string value, bool mono, string detail, string id, UIElement? extra = null, bool warn = false)
    {
        var stack = new StackPanel { Spacing = 4 };
        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        head.Children.Add(new FontIcon { Glyph = glyph, FontSize = 12, Foreground = Palette("NomiAccent") });
        head.Children.Add(Eyebrow(label));
        stack.Children.Add(head);
        var number = Text(value, mono ? 25 : 19);
        number.FontWeight = FontWeights.Bold;
        number.TextWrapping = TextWrapping.WrapWholeWords;
        number.MaxLines = 2;
        number.TextTrimming = TextTrimming.CharacterEllipsis;
        number.Margin = new Thickness(0, 6, 0, 0);
        if (mono) number.FontFamily = (FontFamily)Application.Current.Resources["NomiMono"];
        stack.Children.Add(number);
        var text = Text(detail, 13, warn ? "NomiAccent" : "NomiInk2");
        text.TextWrapping = TextWrapping.WrapWholeWords;
        stack.Children.Add(text);
        if (extra is not null) stack.Children.Add(extra);
        var tile = Glass(stack, 16);
        Mark(tile, id, $"{label} : {value}, {detail}");
        return tile;
    }

    private UIElement Meter(double value, double maximum)
    {
        var bar = new ProgressBar { Maximum = Math.Max(1, maximum), Value = Math.Min(maximum, value), Height = 6, MinHeight = 6, Margin = new Thickness(0, 8, 0, 0), CornerRadius = new CornerRadius(3), Foreground = Palette("NomiAccent"), Background = Palette("NomiLine") };
        AutomationProperties.SetName(bar, T("metricCapacity"));
        return bar;
    }

    private UIElement Dots(int count, int on)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, Margin = new Thickness(0, 8, 0, 0) };
        for (var index = 0; index < count; index++)
            row.Children.Add(new Border { Width = 18, Height = 6, CornerRadius = new CornerRadius(3), Background = Palette(index < on ? "NomiOk" : "NomiLine") });
        AutomationProperties.SetAccessibilityView(row, Microsoft.UI.Xaml.Automation.Peers.AccessibilityView.Raw);
        return row;
    }

    private Grid TodayTiles(DateTimeOffset now, IReadOnlyList<WorkTask> plan, double planned, double available, double ratio, int samples, bool over)
    {
        var doneToday = board.Tasks.Where(task => task.Completed is { } done && done.Date == now.Date).OrderByDescending(task => task.Completed).ToArray();
        var total = doneToday.Length + plan.Count;
        var due = board.Tasks.Where(task => task.Column != Columns.Done && task.Due is { } at && at.Date == now.Date)
            .OrderBy(task => task.Due).FirstOrDefault();
        var grid = new Grid { ColumnSpacing = 12 };
        for (var column = 0; column < 4; column++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var figures = NumberMode == NumberModes.Figures;
        var tiles = new[]
        {
            Tile(T("metricPlanned"), "\uE823", plan.Count == 0 ? T("todayNothing") : Minutes(planned), figures,
                plan.Count == 0 ? T("todayFree") : over ? T("metricOver") : string.Format(CultureInfo.CurrentCulture, T("todayPlannedDetail"), Minutes(available)),
                "MetricPlanned", Meter(planned, available), over),
            Tile(T("metricDone"), "\uE73E", total == 0 ? T("todayNoneYet") : string.Format(CultureInfo.CurrentCulture, T("todayDoneOf"), doneToday.Length, total), figures,
                doneToday.Length > 0 ? string.Format(CultureInfo.CurrentCulture, T("todayLastDone"), doneToday[0].Title) : T("todayDoneEmpty"),
                "MetricDone", total > 0 ? Dots(Math.Min(total, 8), Math.Min(doneToday.Length, 8)) : null),
            Tile(T("todayDue"), "\uE787", due?.Title ?? T("todayDueNone"), false,
                due is { Due: { } at } ? string.Format(CultureInfo.CurrentCulture, T(figures ? "todayDueDetail" : "todayDueDetailWords"), At(at), At(at.AddMinutes(-TaskBoard.Remaining(due, now) * ratio))) : T("todayDueNoneDetail"),
                "MetricDue"),
            Tile(T("metricPace"), "\uE945", samples >= 3 ? Readable.Pace(ratio, language) : T("todayLearning"), false,
                samples >= 3 ? (figures ? $"×{ratio.ToString("0.0", CultureInfo.CurrentCulture)} · {T("metricPaceDetail")}" : T("metricPaceDetail")) : string.Format(CultureInfo.CurrentCulture, T("metricLearning"), samples, 3),
                "MetricPace", samples >= 3 ? null : Dots(3, samples))
        };
        for (var index = 0; index < tiles.Length; index++)
        {
            Grid.SetColumn(tiles[index], index);
            grid.Children.Add(tiles[index]);
            Reveal(tiles[index], 10, index * 50);
        }
        return grid;
    }

    private Border PlanTimeline(IReadOnlyList<WorkTask> plan, DateTimeOffset now, double ratio)
    {
        var day = profile.Day;
        var start = new DateTimeOffset(now.Date, now.Offset) + day.Start;
        var end = new DateTimeOffset(now.Date, now.Offset) + day.End;
        var events = new List<(DateTimeOffset From, DateTimeOffset To, string Title, string Kind)>();
        var lunchFrom = new DateTimeOffset(now.Date, now.Offset) + day.LunchStart;
        var lunchTo = new DateTimeOffset(now.Date, now.Offset) + day.LunchEnd;
        events.Add((lunchFrom, lunchTo, T("todayLunch"), "pause"));
        var meetings = Meetings(now).Where(item => item.End > start && item.Start < end).ToArray();
        foreach (var meeting in meetings) events.Add((meeting.Start, meeting.End, meeting.Title, "meet"));
        foreach (var task in board.Tasks.Where(task => task.Completed is { } done && done.Date == now.Date && task.Spent(now) > 0))
        {
            var finished = task.Completed!.Value;
            events.Add((finished.AddSeconds(-task.Spent(now)), finished, task.Title, "done"));
        }
        var cursor = now > start ? now : start;
        foreach (var task in plan)
        {
            var minutes = Math.Max(10, TaskBoard.Remaining(task, now) * ratio);
            var moved = true;
            while (moved)
            {
                moved = false;
                foreach (var busy in events.Where(item => item.Kind is "pause" or "meet"))
                    if (cursor < busy.To && cursor.AddMinutes(minutes) > busy.From) { cursor = busy.To; moved = true; }
            }
            if (cursor >= end) break;
            events.Add((cursor, cursor.AddMinutes(minutes) < end ? cursor.AddMinutes(minutes) : end, task.Title, "task"));
            cursor = cursor.AddMinutes(minutes);
        }

        var stack = new StackPanel { Spacing = 8 };
        var head = new Grid { ColumnSpacing = 8 };
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        title.Children.Add(new FontIcon { Glyph = "\uE8FD", FontSize = 12, Foreground = Palette("NomiAccent") });
        title.Children.Add(Eyebrow(T("dayPlan")));
        head.Children.Add(title);
        var legend = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        foreach (var (kind, label) in new[] { ("task", T("legendTask")), ("meet", T("legendMeeting")), ("pause", T("legendPause")) })
        {
            legend.Children.Add(new Border { Width = 10, Height = 10, CornerRadius = new CornerRadius(3), Background = EventBrush(kind), BorderBrush = Palette("NomiLine"), BorderThickness = new Thickness(1), Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            legend.Children.Add(Text(label, 12, "NomiInk3"));
        }
        Grid.SetColumn(legend, 1);
        head.Children.Add(legend);
        stack.Children.Add(head);

        var track = new Grid { Height = 66, Margin = new Thickness(0, 14, 0, 0) };
        var figures = NumberMode == NumberModes.Figures || figuresPeek;
        void Lay(double width)
        {
            track.Children.Clear();
            if (width <= 0) return;
            var span = (end - start).TotalMinutes;
            double X(DateTimeOffset at) => Math.Clamp((at - start).TotalMinutes / span, 0, 1) * width;
            foreach (var item in events.Where(item => item.To > start && item.From < end).OrderBy(item => item.Kind == "pause" ? 0 : 1))
            {
                var left = X(item.From);
                var size = Math.Max(6, X(item.To) - left - 3);
                var label = Text(item.Title, 12, item.Kind == "done" ? "NomiInk3" : "NomiInk");
                label.TextTrimming = TextTrimming.CharacterEllipsis;
                label.TextWrapping = TextWrapping.NoWrap;
                label.VerticalAlignment = VerticalAlignment.Center;
                if (item.Kind == "done") label.TextDecorations = Windows.UI.Text.TextDecorations.Strikethrough;
                var block = new Border
                {
                    Width = size, Height = 34, CornerRadius = new CornerRadius(10), Padding = new Thickness(7, 0, 7, 0),
                    HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(left, 6, 0, 0), Background = EventBrush(item.Kind), BorderBrush = Palette("NomiLine"), BorderThickness = new Thickness(1),
                    Opacity = item.Kind == "done" ? 0.6 : 1, Child = size >= 46 ? label : null
                };
                var spoken = $"{item.Title} · {Readable.Span(item.From, item.To, now, NumberMode, language, day)}";
                ToolTipService.SetToolTip(block, spoken);
                AutomationProperties.SetName(block, spoken);
                track.Children.Add(block);
            }
            if (now > start && now < end)
            {
                var marker = new Border { Width = 2, Height = 44, Background = Palette("NomiAccent"), CornerRadius = new CornerRadius(1), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(X(now) - 1, 1, 0, 0) };
                AutomationProperties.SetName(marker, T("todayNow"));
                track.Children.Add(marker);
                track.Children.Add(new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Background = Palette("NomiAccent"), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(X(now) - 4, -2, 0, 0) });
            }
            track.Children.Add(new Border { Height = 1, Background = Palette("NomiLine"), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 48, 0, 0) });
            (DateTimeOffset At, string Text)[] marks = figures
                ? Enumerable.Range(0, (int)(end - start).TotalHours + 1).Select(hour => (start.AddHours(hour), At(start.AddHours(hour)))).ToArray()
                : [(start, T("axisMorning")), (lunchFrom, T("axisNoon")), (lunchTo, T("axisAfternoon")), (end, T("axisEnd"))];
            for (var index = 0; index < marks.Length; index++)
            {
                var tick = Text(marks[index].Text, 11, "NomiInk3");
                var last = index == marks.Length - 1;
                tick.HorizontalAlignment = last ? HorizontalAlignment.Right : HorizontalAlignment.Left;
                tick.VerticalAlignment = VerticalAlignment.Top;
                tick.Margin = last ? new Thickness(0, 51, 0, 0) : new Thickness(Math.Max(0, X(marks[index].At) - (index == 0 ? 0 : 12)), 51, 0, 0);
                track.Children.Add(tick);
            }
            if (plan.Count == 0 && meetings.Length == 0)
            {
                var hint = Text(T("todayPlanEmpty"), 13, "NomiInk3");
                hint.HorizontalAlignment = HorizontalAlignment.Right;
                hint.TextWrapping = TextWrapping.WrapWholeWords;
                hint.MaxWidth = width * 0.45;
                hint.Margin = new Thickness(0, 4, 0, 0);
                track.Children.Add(hint);
            }
        }
        track.SizeChanged += (_, args) => { if (Math.Abs(args.NewSize.Width - args.PreviousSize.Width) > 0.5) Lay(args.NewSize.Width); };
        stack.Children.Add(track);
        var card = Glass(stack, 16);
        Mark(card, "DayPlan", T("dayPlan"));
        return card;
    }

    private UIElement DraftPeek(string draft)
    {
        var stack = new StackPanel { Spacing = 4 };
        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        head.Children.Add(new FontIcon { Glyph = "", FontSize = 12, Foreground = Palette("NomiAccent") });
        var title = Text(T("todayDraftBy"), 12, "NomiAccent");
        title.FontWeight = FontWeights.SemiBold;
        head.Children.Add(title);
        stack.Children.Add(head);
        var body = Text($"« {Short(draft.Replace('\n', ' '), 160)} »", 13, "NomiInk2");
        body.TextWrapping = TextWrapping.WrapWholeWords;
        body.MaxLines = 3;
        body.TextTrimming = TextTrimming.CharacterEllipsis;
        stack.Children.Add(body);
        var box = new Border { Child = stack, Padding = new Thickness(12, 10, 12, 10), CornerRadius = new CornerRadius(14), Background = Palette("NomiSurface"), BorderBrush = Palette("NomiLine"), BorderThickness = new Thickness(1) };
        AutomationProperties.SetName(box, $"{T("todayDraftBy")} : {draft}");
        AutomationProperties.SetAutomationId(box, "TodayDraft");
        return box;
    }

    private Brush EventBrush(string kind) => Palette(kind switch
    {
        "meet" => "NomiSandSoft",
        "pause" => "NomiGlassSoft",
        _ => "NomiAccentSoft"
    });
}
