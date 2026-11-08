using System.Globalization;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Nomi;

public sealed partial class MainWindow
{
    private ScrollViewer? nomiScroll;

    private UIElement NomiThread(ChatThread thread)
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var list = new StackPanel { Spacing = 22, MaxWidth = 720, Padding = new Thickness(24, 10, 24, 24), HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetName(list, thread.Title);
        foreach (var entry in thread.Items) list.Children.Add(entry.Role == "user" ? UserBubble(entry) : NomiMessage(entry));
        nomiScroll = new ScrollViewer { Content = list, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        grid.Children.Add(nomiScroll);
        DispatcherQueue.TryEnqueue(() => nomiScroll?.ChangeView(null, nomiScroll.ScrollableHeight, null, true));
        var bottom = new StackPanel { Padding = new Thickness(20, 0, 20, 4) };
        bottom.Children.Add(NomiComposer());
        var hint = Text(T("nomiHint"), 11.5, "NomiInk3");
        hint.TextAlignment = TextAlignment.Center;
        hint.Margin = new Thickness(0, 7, 0, 0);
        bottom.Children.Add(hint);
        Grid.SetRow(bottom, 1);
        grid.Children.Add(bottom);
        return grid;
    }

    private UIElement UserBubble(ChatEntry entry)
    {
        var stack = new StackPanel { Spacing = 6 };
        foreach (var name in entry.Documents)
        {
            var file = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            file.Children.Add(new FontIcon { Glyph = "\uE8A5", FontSize = 13, Foreground = Palette("NomiInk2") });
            file.Children.Add(Text(name, 13, "NomiInk2"));
            stack.Children.Add(new Border { Child = file, Background = Palette("NomiSurface2"), CornerRadius = new CornerRadius(12), Padding = new Thickness(10, 7, 10, 7) });
        }
        var body = Text(entry.Text, 15.5);
        body.IsTextSelectionEnabled = true;
        stack.Children.Add(body);
        var bubble = new Border
        {
            Child = stack, HorizontalAlignment = HorizontalAlignment.Right, MaxWidth = 560, Padding = new Thickness(15, 11, 15, 11),
            CornerRadius = new CornerRadius(20, 20, 6, 20), Background = Palette("NomiSurface"), BorderBrush = Palette("NomiLine"), BorderThickness = new Thickness(1)
        };
        AutomationProperties.SetName(bubble, $"{T("chatYou")} : {entry.Text}");
        AutomationProperties.SetAutomationId(bubble, "NomiUser");
        return bubble;
    }

    private UIElement NomiMessage(ChatEntry entry)
    {
        var live = entry == liveEntry;
        var grid = new Grid { ColumnSpacing = 14 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.Children.Add(NomiOrb(28, live));
        var body = new StackPanel { Spacing = 8 };
        Grid.SetColumn(body, 1);
        grid.Children.Add(body);
        if (entry.Steps.Length > 0) body.Children.Add(StepsView(entry, live));
        foreach (var paragraph in entry.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var text = RichText(paragraph, 15);
            text.IsTextSelectionEnabled = true;
            text.LineHeight = 24;
            body.Children.Add(text);
        }
        if (live && entry.Text.Length == 0) body.Children.Add(new ProgressBar { IsIndeterminate = true, MaxWidth = 160, HorizontalAlignment = HorizontalAlignment.Left });
        if (entry.Stopped) body.Children.Add(Text(T("nomiStopped"), 13, "NomiInk3"));
        if (!live && entry.Actions.Count > 0) body.Children.Add(ActionCards(entry));
        if (!live) body.Children.Add(MessageTools(entry));
        AutomationProperties.SetName(grid, $"Nomi : {entry.Text}");
        AutomationProperties.SetAutomationId(grid, "NomiAnswer");
        return grid;
    }

    private UIElement StepsView(ChatEntry entry, bool live)
    {
        var steps = new StackPanel { Spacing = 4, Padding = new Thickness(12, 2, 0, 2), BorderBrush = Palette("NomiLine"), BorderThickness = new Thickness(2, 0, 0, 0) };
        for (var index = 0; index < entry.Steps.Length; index++)
        {
            var done = !live || index < liveStep;
            var current = live && index == liveStep;
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            row.Children.Add(current ? new ProgressRing { Width = 12, Height = 12, IsActive = true } : new FontIcon { Glyph = done ? "\uE73E" : "\uE91F", FontSize = done ? 11 : 6, Foreground = Palette(done ? "NomiAccent" : "NomiInk3") });
            row.Children.Add(Text(T($"nomiStep_{entry.Steps[index]}"), 13.5, current ? "NomiInk" : "NomiInk2"));
            steps.Children.Add(row);
        }
        if (live) return steps;
        var expander = new Expander
        {
            Header = Text(string.Format(CultureInfo.CurrentCulture, T("nomiThought"), entry.Seconds.ToString("0.#", CultureInfo.CurrentCulture)), 13, "NomiInk3"),
            Content = steps, Padding = new Thickness(4, 4, 4, 4), MinHeight = 0, HorizontalAlignment = HorizontalAlignment.Left, Background = null, BorderThickness = new Thickness(0)
        };
        AutomationProperties.SetAutomationId(expander, "NomiSteps");
        return expander;
    }
}
