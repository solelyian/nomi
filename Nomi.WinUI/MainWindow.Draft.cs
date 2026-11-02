using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;

namespace Nomi;

public sealed partial class MainWindow
{
    private string? draftTaskId;
    private string draftKind = DraftKinds.Checklist;
    private string draftText = "";
    private CancellationTokenSource? drafting;

    private string DraftKindName(string kind) => T(kind switch
    {
        DraftKinds.Mail => "draftMail",
        DraftKinds.Message => "draftMessage",
        DraftKinds.Outline => "draftOutline",
        DraftKinds.Plan => "draftPlan",
        _ => "draftChecklist"
    });

    private UIElement DraftSection(WorkTask task)
    {
        var stack = new StackPanel { Spacing = 8 };
        var mine = draftTaskId == task.Id;
        if (mine && (draftText.Length > 0 || drafting is not null))
        {
            var card = new StackPanel { Spacing = 8, Padding = new Thickness(12, 10, 12, 10) };
            var head = new Grid { ColumnSpacing = 8 };
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var eyebrow = Eyebrow($"{T("draftTitle")} · {DraftKindName(draftKind)}");
            eyebrow.VerticalAlignment = VerticalAlignment.Center;
            head.Children.Add(eyebrow);
            var kinds = new ComboBox { MinWidth = 150, IsEnabled = drafting is null };
            foreach (var kind in DraftKinds.All) kinds.Items.Add(new ComboBoxItem { Content = DraftKindName(kind), Tag = kind });
            kinds.SelectedIndex = Array.IndexOf(DraftKinds.All, draftKind);
            AutomationProperties.SetName(kinds, T("draftKind"));
            AutomationProperties.SetAutomationId(kinds, "SheetDraftKind");
            kinds.SelectionChanged += async (_, _) =>
            {
                if (kinds.SelectedItem is ComboBoxItem { Tag: string kind } && kind != draftKind) await WriteFirstStep(task.Id, kind);
            };
            Grid.SetColumn(kinds, 1);
            head.Children.Add(kinds);
            card.Children.Add(head);
            if (drafting is not null)
            {
                card.Children.Add(new ProgressBar { IsIndeterminate = true, Height = 3, MinHeight = 3 });
                var status = Text(T("draftWriting"), 12.5, "NomiInk2");
                AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite);
                card.Children.Add(status);
                var stop = Chip(T("stop"), "NomiGhost", "\uE71A", "SheetDraftStop");
                stop.Click += (_, _) => drafting?.Cancel();
                card.Children.Add(stop);
            }
            else
            {
                var body = Text(draftText, 13);
                body.IsTextSelectionEnabled = true;
                AutomationProperties.SetAutomationId(body, "SheetDraftText");
                AutomationProperties.SetName(body, $"{T("draftTitle")} : {draftText}");
                card.Children.Add(body);
                var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                var copy = Chip(T("draftCopy"), "NomiChip", "\uE8C8", "SheetDraftCopy");
                copy.Click += (_, _) => ShowToast(CopyResponse(draftText) == T("copied") ? T("draftCopied") : T("copyError"), null, null);
                buttons.Children.Add(copy);
                var keep = Primary(T("draftToNote"), "SheetDraftKeep");
                keep.Click += (_, _) =>
                {
                    task.Note = task.Note.Trim().Length == 0 ? draftText : $"{task.Note.TrimEnd()}{Environment.NewLine}{Environment.NewLine}{draftText}";
                    board.Save();
                    ResetDraft();
                    BuildSheet(task);
                    ShowToast(T("draftAdded"), null, null);
                };
                buttons.Children.Add(keep);
                var again = Chip(T("draftAgain"), "NomiGhost", "\uE72C", "SheetDraftAgain");
                again.Click += async (_, _) => await WriteFirstStep(task.Id, draftKind);
                buttons.Children.Add(again);
                var drop = Chip(T("dismiss"), "NomiGhost", null, "SheetDraftDrop");
                drop.Click += (_, _) => { ResetDraft(); BuildSheet(task); };
                buttons.Children.Add(drop);
                card.Children.Add(new ScrollViewer
                {
                    Content = buttons, HorizontalScrollMode = ScrollMode.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    VerticalScrollMode = ScrollMode.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
                });
            }
            stack.Children.Add(new Border { Background = Palette("NomiAccentSoft"), CornerRadius = new CornerRadius(12), Child = card });
            return stack;
        }
        var write = Chip(T("draftWrite"), "NomiChip", "\uE70F", "SheetDraft");
        write.IsEnabled = drafting is null;
        write.Click += async (_, _) => await WriteFirstStep(task.Id);
        stack.Children.Add(write);
        if (!inference.Ready && !inference.HasModel) stack.Children.Add(Text(T("draftNeedsModel"), 11.5, "NomiInk3"));
        return stack;
    }

    private void ResetDraft()
    {
        drafting?.Cancel();
        draftTaskId = null;
        draftText = "";
    }

    private async Task WriteFirstStep(string id, string? kind = null)
    {
        var task = board.Find(id);
        if (task is null || drafting is not null) return;
        if (!inference.Ready)
        {
            if (!inference.HasModel)
            {
                ShowToast(T("draftNeedsModel"), T("assistant"), () => { CloseSheet(); Show("workspace"); });
                return;
            }
            await PrepareModel();
            if (!inference.Ready) return;
        }
        draftTaskId = id;
        draftKind = kind ?? FirstStep.Kind(task.Title, task.Note);
        draftText = "";
        using var controller = new CancellationTokenSource();
        drafting = controller;
        if (sheetTaskId == id) BuildSheet(task);
        try
        {
            var space = Current.Contexts.FirstOrDefault(item => item.Id == task.Space)?.Title;
            var result = await inference.GenerateNomiAsync(FirstStep.Prompt(task, space, Now), "draft", language, "summary",
                controller.Token, instruction: $"{FirstStep.Instruction(draftKind)} {AdaptiveInstruction()}".Trim());
            draftText = result.Accepted ? result.Text.Trim() : "";
            if (draftText.Length == 0) ShowToast(T("draftEmpty"), null, null);
        }
        catch (OperationCanceledException) { draftText = ""; }
        catch (Exception error) when (error is InferenceException or HttpRequestException or IOException or System.Text.Json.JsonException)
        {
            draftText = "";
            ShowToast(T("draftEmpty"), null, null);
        }
        finally
        {
            drafting = null;
            if (draftText.Length == 0) draftTaskId = null;
            if (sheetTaskId == id && board.Find(id) is { } current) BuildSheet(current);
            if (draftText.Length > 0 && sheetTaskId == id)
                DispatcherQueue.TryEnqueue(() => ShowToast(string.Format(CultureInfo.CurrentCulture, T("draftReady"), DraftKindName(draftKind)), null, null));
        }
    }
}
