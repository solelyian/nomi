using System.Globalization;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Nomi;

// Before the call: what to share, how to talk to Nomi, and when it may speak on its own.
public sealed partial class MainWindow
{
    private UIElement PreCallView()
    {
        if (!focusListed) LoadWindows();
        var stack = new StackPanel { Spacing = 16, MaxWidth = 920, Margin = new Thickness(0, 8, 0, 0) };
        var header = new Grid { ColumnSpacing = 12 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var titles = new StackPanel { Spacing = 6 };
        titles.Children.Add(Heading(T("callTitle"), 28));
        titles.Children.Add(Text(T("callIntro"), 14, "NomiInk2"));
        header.Children.Add(titles);
        var ready = vision.HasModel
            ? Pill($"● {vision.Model} · {T("callReadyLocal")}", "NomiSageSoft", "NomiOk")
            : Pill(T("focusNeedsVision"), "NomiAccentSoft", "NomiAccent");
        ready.VerticalAlignment = VerticalAlignment.Top;
        Grid.SetColumn(ready, 1);
        header.Children.Add(ready);
        stack.Children.Add(header);
        if (!vision.HasModel || visionPreparation is not null) stack.Children.Add(VisionCard());
        stack.Children.Add(WindowTiles());
        stack.Children.Add(CallOptions());

        var tasks = board.Tasks.Where(task => task.Column is Columns.Todo or Columns.Doing).OrderBy(task => task.Column == Columns.Doing ? 0 : 1).ThenBy(task => task.Order).ToArray();
        var link = new ComboBox { MinWidth = 260, PlaceholderText = T("focusLinkNone"), VerticalAlignment = VerticalAlignment.Center };
        link.Items.Add(T("focusLinkNone"));
        foreach (var task in tasks) link.Items.Add(task.Title);
        var linked = Array.FindIndex(tasks, task => task.Id == (focusTaskId ?? board.Active?.Id));
        link.SelectedIndex = linked + 1;
        focusTaskId = linked >= 0 ? tasks[linked].Id : null;
        AutomationProperties.SetName(link, T("focusLink"));
        AutomationProperties.SetAutomationId(link, "FocusLink");
        link.SelectionChanged += (_, _) => focusTaskId = link.SelectedIndex <= 0 ? null : tasks[link.SelectedIndex - 1].Id;
        var linkRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        var linkLabel = Text(T("focusLink"), 13, "NomiInk2");
        linkLabel.VerticalAlignment = VerticalAlignment.Center;
        linkRow.Children.Add(linkLabel);
        linkRow.Children.Add(link);
        stack.Children.Add(linkRow);

        var bottom = new Grid { ColumnSpacing = 16 };
        bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var privacy = Text(T("callPrivacy"), 12.5, "NomiInk3");
        privacy.MaxWidth = 580;
        privacy.HorizontalAlignment = HorizontalAlignment.Left;
        AutomationProperties.SetAutomationId(privacy, "FocusPrivacy");
        bottom.Children.Add(privacy);
        var start = Primary(T("callStart"), "FocusStart");
        start.Padding = new Thickness(22, 12, 22, 12);
        start.FontSize = 15;
        start.IsEnabled = focusWindow is not null && vision.HasModel && visionPreparation is null;
        start.VerticalAlignment = VerticalAlignment.Center;
        start.Click += async (_, _) => await StartFocus();
        Grid.SetColumn(start, 1);
        bottom.Children.Add(start);
        stack.Children.Add(bottom);
        if (focusWindow is null) stack.Children.Add(Text(T("focusChooseHelp"), 12.5, "NomiInk3"));
        if (focusMessage.Length > 0)
        {
            var message = Text(focusMessage, 12.5, "NomiAccent");
            AutomationProperties.SetAutomationId(message, "FocusMessage");
            AutomationProperties.SetLiveSetting(message, Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Polite);
            stack.Children.Add(message);
        }
        return stack;
    }

    private Border CallOptions()
    {
        var grid = new Grid { ColumnSpacing = 28, RowSpacing = 18 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var mic = Option(T("callMicOption"), T("callMicNote"), callMic && speech.HasModel, speech.HasModel, "CallMicOption", on => callMic = on);
        if (!speech.HasModel)
        {
            if (speechPreparation is not null)
            {
                var status = speechProgress.Stage == "model-downloading"
                    ? $"{speech.Model} · {Readable.Fraction(speechProgress.Fraction, language)}" : $"{speech.Model} · {Label(speechProgress.Stage)}";
                var line = Text(status, 12.5, "NomiInk2");
                AutomationProperties.SetLiveSetting(line, Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Polite);
                mic.Children.Add(line);
                var stop = Chip(T("stop"), "NomiGhost", null, "CallSpeechStop");
                stop.HorizontalAlignment = HorizontalAlignment.Left;
                stop.Click += (_, _) => speechPreparation?.Cancel();
                mic.Children.Add(stop);
            }
            else
            {
                if (speechProgress.Stage is not ("vision-idle" or "local-model-ready" or "model-paused"))
                    mic.Children.Add(Text(Label(speechProgress.Stage), 12.5, "NomiAccent"));
                var get = Chip(string.Format(T("callSpeechDownload"), Gigabytes(speech.Bytes)), "NomiChip", "\uE896", "CallSpeechDownload");
                get.HorizontalAlignment = HorizontalAlignment.Left;
                get.Click += (_, _) => _ = PrepareSpeech(true, load: false);
                mic.Children.Add(get);
            }
        }
        grid.Children.Add(mic);
        var hasVoice = Voice.HasVoice(language);
        var speaking = Option(T("callVoiceOption"), T(hasVoice ? "callVoiceNote" : "callNoVoice"), callVoice && hasVoice, hasVoice, "CallVoiceOption", on => callVoice = on);
        Grid.SetColumn(speaking, 1);
        grid.Children.Add(speaking);
        var mode = new StackPanel { Spacing = 6 };
        var title = Text(T("callModeTitle"), 14);
        title.FontWeight = FontWeights.SemiBold;
        mode.Children.Add(title);
        mode.Children.Add(Text(T("callModeNote"), 12.5, "NomiInk3"));
        mode.Children.Add(ModeSelector("Focus", false));
        Grid.SetRow(mode, 1);
        Grid.SetColumnSpan(mode, 2);
        grid.Children.Add(mode);
        var card = Glass(grid, 18);
        Mark(card, "CallOptions", T("callOptions"));
        return card;
    }

    private StackPanel Option(string title, string note, bool on, bool enabled, string automation, Action<bool> set)
    {
        var stack = new StackPanel { Spacing = 6 };
        var row = new Grid { ColumnSpacing = 12 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var texts = new StackPanel { Spacing = 2 };
        var name = Text(title, 14);
        name.FontWeight = FontWeights.SemiBold;
        texts.Children.Add(name);
        texts.Children.Add(Text(note, 12.5, "NomiInk3"));
        row.Children.Add(texts);
        var toggle = new ToggleSwitch { IsOn = on, IsEnabled = enabled, OnContent = "", OffContent = "", MinWidth = 0, VerticalAlignment = VerticalAlignment.Top };
        AutomationProperties.SetName(toggle, title);
        AutomationProperties.SetAutomationId(toggle, automation);
        toggle.Toggled += (_, _) => set(toggle.IsOn);
        Grid.SetColumn(toggle, 1);
        row.Children.Add(toggle);
        stack.Children.Add(row);
        return stack;
    }

    private Border ModeSelector(string prefix, bool brief)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        foreach (var (mode, key) in new[] { (CallModes.Ask, "callModeAsk"), (CallModes.Problem, "callModeProblem"), (CallModes.Comment, "callModeComment") })
        {
            var label = T(brief ? $"{key}Short" : key);
            var toggle = new ToggleButton { Content = label, Style = (Style)Application.Current.Resources["NomiSegment"], IsChecked = callMode == mode };
            AutomationProperties.SetName(toggle, T(key));
            AutomationProperties.SetAutomationId(toggle, $"{prefix}Mode_{mode}");
            toggle.Click += (_, _) =>
            {
                callMode = mode;
                if (call is not null) call.Mode = mode;
                BuildFocus();
            };
            row.Children.Add(toggle);
        }
        var border = new Border { Background = Palette("NomiSurface2"), CornerRadius = new CornerRadius(9), Padding = new Thickness(3), HorizontalAlignment = HorizontalAlignment.Left, Child = row };
        AutomationProperties.SetName(border, T("callModeTitle"));
        return border;
    }
}
