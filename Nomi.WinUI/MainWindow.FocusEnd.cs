using System.Globalization;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Nomi;

// After the call: what Nomi noticed, actions that wait for a click, and a summary kept only if the user saves it.
public sealed partial class MainWindow
{
    private UIElement EndView(FocusCall ended)
    {
        var stack = new StackPanel { Spacing = 16, MaxWidth = 780, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 12, 0, 0) };
        var titles = new StackPanel { Spacing = 6 };
        titles.Children.Add(Heading(T("callEnded"), 28));
        titles.Children.Add(Text(T("callEndedNote"), 14, "NomiInk2"));
        stack.Children.Add(titles);

        var stats = new Grid { ColumnSpacing = 12 };
        var minutes = Math.Max(1, (int)Math.Round(ended.Duration(Now).TotalMinutes));
        var values = new (string Value, string Label)[]
        {
            ($"{minutes} min", T("callDuration")),
            (ended.Points.Count.ToString(CultureInfo.CurrentCulture), T("callPoints")),
            (ended.Questions.ToString(CultureInfo.CurrentCulture), T("callQuestions"))
        };
        for (var index = 0; index < values.Length; index++)
        {
            stats.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var cell = new StackPanel { Spacing = 2 };
            var number = Text(values[index].Value, 24);
            number.FontWeight = FontWeights.SemiBold;
            cell.Children.Add(number);
            cell.Children.Add(Text(values[index].Label, 12.5, "NomiInk3"));
            var card = Glass(cell, 16);
            Mark(card, $"CallStat_{index}", $"{values[index].Label} : {values[index].Value}");
            Grid.SetColumn(card, index);
            stats.Children.Add(card);
        }
        stack.Children.Add(stats);

        var noticed = new StackPanel { Spacing = 12 };
        noticed.Children.Add(CardTitle(T("callNoticed")));
        if (ended.Points.Count == 0) noticed.Children.Add(Text(T("callNothing"), 13.5, "NomiInk3"));
        foreach (var point in ended.Points)
        {
            var note = $"{point.At.ToString("HH:mm:ss", CultureInfo.CurrentCulture)} · {point.Window}";
            noticed.Children.Add(EndRow(IssueText(point.Issue), note,
                Pill(T(point.Fixed ? "callFixed" : "callToCheck"), point.Fixed ? "NomiSageSoft" : "NomiAccentSoft", point.Fixed ? "NomiOk" : "NomiAccent")));
        }
        var noticedCard = Glass(noticed, 18);
        Mark(noticedCard, "CallNoticed", T("callNoticed"));
        stack.Children.Add(noticedCard);

        var actions = new StackPanel { Spacing = 12 };
        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        head.Children.Add(CardTitle(T("callActions")));
        head.Children.Add(Text($"· {T("callNothingApplied")}", 12.5, "NomiInk3"));
        actions.Children.Add(head);
        var open = ended.Points.Where(point => !point.Fixed).ToArray();
        foreach (var point in open)
        {
            var title = Short($"{T("verifyPrefix")} {IssueText(point.Issue)}", 120);
            var create = Chip(T("callCreate"), "NomiChip", "\uE710", $"CallCreate_{ended.Points.IndexOf(point)}");
            create.Click += (_, _) =>
            {
                var created = board.Add(title, Columns.Todo, contextId, "vision");
                created.Estimate = 10;
                created.Note = IssueText(point.Issue);
                board.Save();
                create.IsEnabled = false;
                ShowToast(string.Format(CultureInfo.CurrentCulture, T("taskAdded"), created.Title), T("open"), () => OpenSheet(created.Id));
                BuildRail();
            };
            actions.Children.Add(EndRow(string.Format(T("callCreateTask"), title), null, create));
        }
        var linked = focusTaskId is not null ? board.Find(focusTaskId) : null;
        if (linked is { Column: not Columns.Done })
        {
            var done = Chip(T("callMarkDone"), "NomiChip", "\uE73E", "CallMarkDone");
            done.Click += (_, _) =>
            {
                board.Move(linked, Columns.Done, Now);
                board.Save();
                done.IsEnabled = false;
                BuildRail();
                BuildCompanion();
            };
            actions.Children.Add(EndRow(string.Format(T("callMarkDoneTask"), linked.Title), null, done));
        }
        if (open.Length == 0 && linked is not { Column: not Columns.Done }) actions.Children.Add(Text(T("callNoAction"), 13.5, "NomiInk3"));
        var actionsCard = Glass(actions, 18);
        Mark(actionsCard, "CallActions", T("callActions"));
        stack.Children.Add(actionsCard);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right };
        var close = Chip(T("callClose"), "NomiChip", null, "CallClose");
        close.Click += (_, _) => { endedCall = null; focusScreen = "pre"; BuildFocus(); };
        var save = Primary(T("callSave"), "CallSave");
        save.Click += (_, _) =>
        {
            SaveSummary(ended, linked);
            endedCall = null;
            focusScreen = "pre";
            BuildFocus();
        };
        buttons.Children.Add(close);
        buttons.Children.Add(save);
        stack.Children.Add(buttons);
        return stack;
    }

    private TextBlock CardTitle(string value)
    {
        var title = Text(value, 15);
        title.FontWeight = FontWeights.SemiBold;
        AutomationProperties.SetHeadingLevel(title, Microsoft.UI.Xaml.Automation.Peers.AutomationHeadingLevel.Level2);
        return title;
    }

    private Grid EndRow(string text, string? note, FrameworkElement side)
    {
        var row = new Grid { ColumnSpacing = 12 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var texts = new StackPanel { Spacing = 2 };
        texts.Children.Add(Text(text, 14));
        if (note is not null) texts.Children.Add(Text(note, 12, "NomiInk3"));
        row.Children.Add(texts);
        side.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(side, 1);
        row.Children.Add(side);
        return row;
    }

    private void SaveSummary(FocusCall ended, WorkTask? linked)
    {
        var culture = CultureInfo.CurrentCulture;
        var minutes = Math.Max(1, (int)Math.Round(ended.Duration(Now).TotalMinutes));
        var lines = new List<string>
        {
            string.Format(culture, T("callSummaryHead"), ended.Window, ended.Started.ToString("g", culture), minutes, ended.Questions)
        };
        lines.AddRange(ended.Points.Select(point => $"• {IssueText(point.Issue)} — {T(point.Fixed ? "callFixed" : "callToCheck")}"));
        var summary = string.Join("\n", lines);
        var target = linked ?? board.Add(Short(string.Format(culture, T("callSummaryTask"), ended.Window), 120), Columns.Done, contextId, "vision");
        target.Note = target.Note.Length > 0 ? $"{target.Note}\n\n{summary}" : summary;
        board.Save();
        ShowToast(T("callSaved"), T("open"), () => OpenSheet(target.Id));
        BuildRail();
    }
}
