using System.Globalization;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace Nomi;

public sealed partial class MainWindow
{
    private UIElement ActionCards(ChatEntry entry)
    {
        var stack = new StackPanel { Spacing = 8, Margin = new Thickness(0, 4, 0, 0) };
        AutomationProperties.SetName(stack, T("nomiPropose"));
        var head = Text(T("nomiPropose").ToUpper(CultureInfo.CurrentCulture), 11, "NomiInk3");
        head.FontWeight = FontWeights.SemiBold;
        head.CharacterSpacing = 60;
        stack.Children.Add(head);
        foreach (var action in entry.Actions)
        {
            var task = action.TaskId is null ? null : board.Find(action.TaskId);
            var gone = action.TaskId is not null && (task is null || task.Column == Columns.Done && action.Kind != ChatActions.Create);
            var label = string.Format(CultureInfo.CurrentCulture, T(action.Kind switch
            {
                ChatActions.Postpone => "chatActPostpone", ChatActions.Split => "chatActSplit", ChatActions.Focus => "chatActFocus", _ => "chatActCreate"
            }), task?.Title ?? action.Title);
            var card = new Grid { ColumnSpacing = 12 };
            card.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            card.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            card.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            card.Children.Add(new FontIcon
            {
                Glyph = action.Done ? "\uE73E" : action.Kind switch { ChatActions.Postpone => "\uE916", ChatActions.Split => "\uE8C6", ChatActions.Focus => "\uE7B3", _ => "\uE710" },
                FontSize = 15, Foreground = Palette(action.Done ? "NomiAccent" : "NomiInk2"), VerticalAlignment = VerticalAlignment.Center
            });
            var title = Text(label, 14);
            title.FontWeight = FontWeights.SemiBold;
            title.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(title, 1);
            card.Children.Add(title);
            var apply = Chip(action.Done ? T("chatActDone") : T("nomiApply"), action.Done ? "NomiGhost" : "NomiPrimary", null, $"NomiApply_{action.Kind}");
            AutomationProperties.SetName(apply, action.Done ? $"{label} · {T("chatActDone")}" : $"{T("nomiApply")} : {label}");
            apply.IsEnabled = !action.Done && !gone && nomiRun is null;
            if (gone && !action.Done) ToolTipService.SetToolTip(apply, T("chatActGone"));
            apply.Click += async (_, _) => await ApplyChatAction(action);
            Grid.SetColumn(apply, 2);
            card.Children.Add(apply);
            stack.Children.Add(new Border { Child = card, Padding = new Thickness(12, 10, 12, 10), CornerRadius = new CornerRadius(14), Background = Palette("NomiGlassStrong"), BorderBrush = Palette("NomiLine"), BorderThickness = new Thickness(1) });
        }
        return stack;
    }

    private UIElement MessageTools(ChatEntry entry)
    {
        var row = new Grid { ColumnSpacing = 2, Margin = new Thickness(-6, 0, 0, 0) };
        foreach (var width in new[] { GridLength.Auto, GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star) })
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = width });
        Button Tool(int column, string glyph, string name, string id)
        {
            var button = new Button { Content = new FontIcon { Glyph = glyph, FontSize = 13 }, Style = (Style)Application.Current.Resources["NomiGhost"], Width = 32, Height = 30, Padding = new Thickness(0), IsEnabled = nomiRun is null };
            AutomationProperties.SetName(button, name);
            AutomationProperties.SetAutomationId(button, id);
            ToolTipService.SetToolTip(button, name);
            Grid.SetColumn(button, column);
            row.Children.Add(button);
            return button;
        }
        Tool(0, "\uE8C8", T("nomiCopy"), "NomiCopy").Click += (_, _) =>
        {
            var package = new DataPackage();
            package.SetText(entry.Text);
            Clipboard.SetContent(package);
            ShowToast(T("copied"), null, null);
        };
        Tool(1, "\uE72C", T("nomiRegenerate"), "NomiRegenerate").Click += async (_, _) => await Regenerate(entry);
        var details = Tool(2, "\uE946", T("nomiDetails"), "NomiDetails");
        details.Flyout = new Flyout { Content = DetailsView(entry) };
        if (entry.Model.Length > 0)
        {
            var who = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            who.Children.Add(new FontIcon { Glyph = "\uE950", FontSize = 11, Foreground = Palette("NomiInk3") });
            who.Children.Add(Text($"{entry.Model} · {T("nomiOnThisPc")}", 12, "NomiInk3"));
            Grid.SetColumn(who, 3);
            row.Children.Add(who);
        }
        return row;
    }

    private UIElement DetailsView(ChatEntry entry)
    {
        var grid = new Grid { ColumnSpacing = 16, RowSpacing = 6, MaxWidth = 420 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var rows = new (string, string)[]
        {
            ("nomiDetLevel", LevelName(entry.Level) + (entry.Reason == "routeChosen" ? "" : $" · {T("nomiByAuto")}")),
            ("nomiDetModel", entry.Model),
            ("nomiDetReading", entry.Documents.Length > 0 ? string.Join(", ", entry.Documents) : T("nomiDetNoDocument")),
            ("nomiDetWhy", entry.Reason.Length > 0 ? T(entry.Reason) : "—"),
            ("nomiDetCalc", entry.Steps.Contains(Steps.Calculate) ? T("nomiDetCalcEngine") : T("nomiDetNoCalc")),
            ("nomiDetDevice", inference.Accelerator.Length > 0 ? inference.Accelerator : T("nomiOnThisPc")),
            ("nomiDetTime", string.Format(CultureInfo.CurrentCulture, T("nomiSeconds"), entry.Seconds.ToString("0.#", CultureInfo.CurrentCulture)))
        };
        for (var index = 0; index < rows.Length; index++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var key = Text(T(rows[index].Item1), 12.5, "NomiInk3");
            Grid.SetRow(key, index);
            var value = Text(rows[index].Item2, 13);
            Grid.SetRow(value, index);
            Grid.SetColumn(value, 1);
            grid.Children.Add(key);
            grid.Children.Add(value);
        }
        var stack = new StackPanel { Spacing = 10 };
        AutomationProperties.SetName(stack, T("nomiDetails"));
        stack.Children.Add(grid);
        stack.Children.Add(Text(T("nomiDetPrivate"), 12, "NomiInk3"));
        return stack;
    }
}
