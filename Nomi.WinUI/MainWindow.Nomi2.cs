using System.Globalization;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Nomi;

public sealed partial class MainWindow
{
    private void FillHistory(StackPanel list)
    {
        list.Children.Clear();
        var items = threads.Search(historyQuery).ToList();
        if (items.Count == 0)
        {
            list.Children.Add(Text(threads.Threads.Count == 0 ? T("nomiNoConversations") : T("nomiNoMatch"), 12.5, "NomiInk3"));
            return;
        }
        string? group = null;
        foreach (var thread in items)
        {
            var when = HistoryGroup(thread.Updated);
            if (when != group)
            {
                group = when;
                var head = Text(when.ToUpper(CultureInfo.CurrentCulture), 11, "NomiInk3");
                head.FontWeight = FontWeights.SemiBold;
                head.CharacterSpacing = 60;
                head.Margin = new Thickness(10, 12, 0, 4);
                AutomationProperties.SetHeadingLevel(head, Microsoft.UI.Xaml.Automation.Peers.AutomationHeadingLevel.Level2);
                list.Children.Add(head);
            }
            list.Children.Add(HistoryItem(thread));
        }
    }

    private string HistoryGroup(DateTimeOffset time)
    {
        var days = (Now.Date - time.ToLocalTime().Date).TotalDays;
        return days < 1 ? T("today") : days < 7 ? T("nomiThisWeek") : T("nomiEarlier");
    }

    private UIElement HistoryItem(ChatThread thread)
    {
        var selected = thread == openThread;
        var grid = new Grid { CornerRadius = new CornerRadius(10), Background = selected ? Palette("NomiSurface") : null };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = new TextBlock { Text = thread.Title, FontSize = 13.5, FontFamily = readingFont, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = Palette(selected ? "NomiInk" : "NomiInk2") };
        var open = new Button { Content = title, Style = (Style)Application.Current.Resources["NomiGhost"], HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(10, 8, 10, 8) };
        AutomationProperties.SetName(open, thread.Title);
        AutomationProperties.SetAutomationId(open, "NomiThread");
        if (selected) AutomationProperties.SetItemStatus(open, T("nomiCurrent"));
        open.Click += (_, _) => OpenThread(thread);
        grid.Children.Add(open);
        var more = new Button { Content = new FontIcon { Glyph = "\uE712", FontSize = 12 }, Style = (Style)Application.Current.Resources["NomiGhost"], Width = 28, Height = 28, Padding = new Thickness(0), Opacity = selected ? 1 : 0.55 };
        AutomationProperties.SetName(more, string.Format(CultureInfo.CurrentCulture, T("nomiOptions"), thread.Title));
        var menu = new MenuFlyout();
        var rename = new MenuFlyoutItem { Text = T("nomiRename"), Icon = new FontIcon { Glyph = "\uE70F" } };
        rename.Click += (_, _) => RenameThread(thread, more);
        var delete = new MenuFlyoutItem { Text = T("nomiDelete"), Icon = new FontIcon { Glyph = "\uE74D" } };
        delete.Click += (_, _) => DeleteThread(thread);
        menu.Items.Add(rename);
        menu.Items.Add(delete);
        more.Flyout = menu;
        Grid.SetColumn(more, 1);
        grid.Children.Add(more);
        return grid;
    }

    private UIElement NomiHeader()
    {
        var bar = new Grid { Height = 40, ColumnSpacing = 8, Padding = new Thickness(4, 0, 4, 0) };
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var toggle = new Button { Content = new FontIcon { Glyph = "\uE89F", FontSize = 14 }, Style = (Style)Application.Current.Resources["NomiGhost"], Width = 34, Height = 34, Padding = new Thickness(0) };
        AutomationProperties.SetName(toggle, historyShown ? T("nomiHideHistory") : T("nomiShowHistory"));
        AutomationProperties.SetAutomationId(toggle, "NomiHistoryToggle");
        ToolTipService.SetToolTip(toggle, historyShown ? T("nomiHideHistory") : T("nomiShowHistory"));
        toggle.Click += (_, _) => { historyShown = !historyShown; BuildNomi(); };
        bar.Children.Add(toggle);
        var title = new TextBlock { Text = openThread?.Title ?? "Nomi", FontSize = 15, FontWeight = FontWeights.SemiBold, FontFamily = readingFont, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, Foreground = Palette("NomiInk") };
        AutomationProperties.SetHeadingLevel(title, Microsoft.UI.Xaml.Automation.Peers.AutomationHeadingLevel.Level1);
        Grid.SetColumn(title, 1);
        bar.Children.Add(title);
        if (openThread is not null)
        {
            var fresh = Chip(T("chatNew"), "NomiGhost", "\uE710", "NomiNewTop");
            fresh.Click += (_, _) => NewThread();
            Grid.SetColumn(fresh, 2);
            bar.Children.Add(fresh);
        }
        return bar;
    }
}
