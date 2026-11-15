using System.Globalization;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Windows.System;

namespace Nomi;

// The captions of the call: what the user said or wrote, and Nomi's answers and remarks.
public sealed partial class MainWindow
{
    private StackPanel? feedPanel;
    private ScrollViewer? feedScroll;
    private readonly Dictionary<CallLine, TextBlock> lineViews = [];

    private void ForgetCallView()
    {
        callGrid = null;
        previewImage = null;
        previewWaiting = null;
        feedPanel = null;
        feedScroll = null;
        callClock = callSeen = callActivityText = callMessage = null;
        lineViews.Clear();
    }

    private Border CaptionsView()
    {
        var grid = new Grid { RowSpacing = 12 };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var head = new StackPanel { Spacing = 10 };
        var top = new Grid();
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        top.Children.Add(CardTitle(T("callCaptions")));
        var model = Text(vision.Model, 12, "NomiInk3");
        model.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(model, 1);
        top.Children.Add(model);
        head.Children.Add(top);
        head.Children.Add(ModeSelector("Call", true));
        grid.Children.Add(head);
        feedPanel = new StackPanel { Spacing = 14 };
        feedScroll = new ScrollViewer { Content = feedPanel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollMode = ScrollMode.Disabled };
        AutomationProperties.SetName(feedScroll, T("callCaptions"));
        AutomationProperties.SetAutomationId(feedScroll, "CallCaptions");
        foreach (var line in call!.Lines) feedPanel.Children.Add(LineView(line));
        feedScroll.Loaded += (_, _) => ScrollEnd();
        Grid.SetRow(feedScroll, 1);
        grid.Children.Add(feedScroll);
        var composer = CallComposer();
        Grid.SetRow(composer, 2);
        grid.Children.Add(composer);
        return Glass(grid, 16);
    }

    private StackPanel CallComposer()
    {
        var stack = new StackPanel { Spacing = 8 };
        var row = new Grid { ColumnSpacing = 6 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var box = new TextBox { PlaceholderText = T("callAsk"), TextWrapping = TextWrapping.Wrap, FontFamily = readingFont, FontSize = 14, MaxLength = 600 };
        AutomationProperties.SetName(box, T("callAsk"));
        AutomationProperties.SetAutomationId(box, "CallAsk");
        void Send()
        {
            var text = box.Text;
            box.Text = "";
            _ = Ask(text, "typed");
        }
        box.KeyDown += (_, args) =>
        {
            if (args.Key != VirtualKey.Enter) return;
            args.Handled = true;
            Send();
        };
        row.Children.Add(box);
        var mic = Chip("", "NomiGhost", callMic ? "\uE720" : "\uEC54", "CallAskMic");
        AutomationProperties.SetName(mic, $"{T("callMic")}, {T(callMic ? "callOn" : "callOff")}");
        mic.Click += async (_, _) => await ToggleMic();
        Grid.SetColumn(mic, 1);
        row.Children.Add(mic);
        var send = Chip("", "NomiPrimary", "\uE724", "CallSend");
        AutomationProperties.SetName(send, T("callSend"));
        send.Click += (_, _) => Send();
        Grid.SetColumn(send, 2);
        row.Children.Add(send);
        stack.Children.Add(row);
        stack.Children.Add(Text(T("callHint"), 11.5, "NomiInk3"));
        return stack;
    }

    private Border Avatar(bool nomi) => new()
    {
        Width = 28,
        Height = 28,
        CornerRadius = new CornerRadius(14),
        VerticalAlignment = VerticalAlignment.Top,
        Background = Palette(nomi ? "NomiAccent" : "NomiSurface2"),
        Child = new TextBlock
        {
            Text = nomi ? "N" : T("callYouInitial"),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            FontFamily = readingFont,
            Foreground = Palette(nomi ? "NomiAccentInk" : "NomiInk2"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        }
    };

    private Grid LineView(CallLine line)
    {
        var row = new Grid { ColumnSpacing = 10 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var avatar = Avatar(line.Who == "nomi");
        AutomationProperties.SetAccessibilityView(avatar, AccessibilityView.Raw);
        row.Children.Add(avatar);
        var body = new StackPanel { Spacing = 4 };
        body.Children.Add(Text(LineMeta(line), 11.5, "NomiInk3"));
        var text = Text(line.Text.Length > 0 ? line.Text : T("callLookingAt"), 14);
        text.IsTextSelectionEnabled = true;
        body.Children.Add(text);
        lineViews[line] = text;
        if (line.Verified) body.Children.Add(Text($"✓ {T("callVerified")}", 12, "NomiSage"));
        if (line.Point is { Fixed: false } point && line.Source == "self") body.Children.Add(PointActions(point));
        Grid.SetColumn(body, 1);
        row.Children.Add(body);
        return row;
    }

    private string LineMeta(CallLine line)
    {
        var culture = CultureInfo.CurrentCulture;
        var parts = new List<string> { line.Who == "nomi" ? "Nomi" : T("callYou"), line.At.ToString("HH:mm:ss", culture) };
        if (line.Source == "voice") parts.Add(T("callByVoice"));
        else if (line.Source == "typed") parts.Add(T("callTyped"));
        if (line.Who == "nomi" && line.Seen is { } seen) parts.Add(string.Format(T("callImageOf"), seen.ToString("HH:mm:ss", culture)));
        if (line.Source == "self") parts.Add(T("callBySelf"));
        return string.Join(" · ", parts);
    }

    private StackPanel PointActions(CallPoint point)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var create = Chip(T("callCreate"), "NomiChip", "\uE710", null);
        create.Click += (_, _) =>
        {
            var created = board.Add(Short($"{T("verifyPrefix")} {IssueText(point.Issue)}", 120), Columns.Todo, contextId, "vision");
            created.Estimate = 10;
            created.Note = IssueText(point.Issue);
            board.Save();
            create.IsEnabled = false;
            ShowToast(string.Format(CultureInfo.CurrentCulture, T("taskAdded"), created.Title), T("open"), () => OpenSheet(created.Id));
            BuildRail();
        };
        row.Children.Add(create);
        if (focusTaskId is not null && board.Find(focusTaskId) is { } linked)
        {
            var note = Chip(T("callAddNote"), "NomiChip", "\uE70B", null);
            note.Click += (_, _) =>
            {
                linked.Note = linked.Note.Length > 0 ? $"{linked.Note}\n{IssueText(point.Issue)}" : IssueText(point.Issue);
                board.Save();
                note.IsEnabled = false;
                ShowToast(T("callNoted"), null, null);
            };
            row.Children.Add(note);
        }
        var ignore = Chip(T("callIgnore"), "NomiGhost", null, null);
        ignore.Click += (_, _) => row.Visibility = Visibility.Collapsed;
        row.Children.Add(ignore);
        return row;
    }

    private void ScrollEnd()
    {
        if (feedScroll is null) return;
        feedScroll.UpdateLayout();
        feedScroll.ChangeView(null, feedScroll.ScrollableHeight, null, true);
    }

    private void CallAdd(CallLine line)
    {
        if (feedPanel is not null)
        {
            feedPanel.Children.Add(LineView(line));
            ScrollEnd();
            if (!line.Live) AnnounceLine(line);
        }
        UpdateMini();
    }

    private void CallUpdateLine(CallLine line)
    {
        if (lineViews.TryGetValue(line, out var text))
        {
            text.Text = line.Text.Length > 0 ? line.Text : T("callLookingAt");
            ScrollEnd();
            if (!line.Live) AnnounceLine(line);
        }
        UpdateMini();
    }

    private void AnnounceLine(CallLine line)
    {
        if (!lineViews.TryGetValue(line, out var text)) return;
        AutomationProperties.SetLiveSetting(text, AutomationLiveSetting.Polite);
        FrameworkElementAutomationPeer.CreatePeerForElement(text)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    private void CallRefresh()
    {
        if (callSeen is not null) callSeen.Text = SeenLabel();
        if (callMessage is not null)
        {
            callMessage.Text = focusMessage;
            callMessage.Visibility = focusMessage.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        if (callActivityText is not null) callActivityText.Text = ActivityLabel();
        UpdateMini();
    }

    private void CallActivity(string state)
    {
        callActivity = state;
        if (callActivityText is not null) callActivityText.Text = ActivityLabel();
        UpdateMini();
    }

    private string ActivityLabel()
    {
        if (focusState == "paused") return T("focusPausedLong");
        return callActivity switch
        {
            "preparing" => T("callPreparing"),
            "hearing" => T("callHearing"),
            "transcribing" => T("callTranscribing"),
            "looking" => T("callLooking"),
            "thinking" => T("callThinking"),
            "speaking" => T("callSpeaking"),
            _ => T(callMic ? "callListening" : "callWatchingOnly")
        };
    }
}
