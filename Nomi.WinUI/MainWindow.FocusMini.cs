using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;
using WinRT.Interop;

namespace Nomi;

// A small always-on-top window that keeps the call going while the shared app is in front.
public sealed partial class MainWindow
{
    private Window? callMini;
    private TextBlock? miniCaption;
    private TextBlock? miniClock;
    private Button? miniMic;
    private Button? miniPause;

    private void OpenMini()
    {
        if (call is null) return;
        if (callMini is not null) { callMini.Activate(); return; }
        var window = new Window { Title = $"Nomi — {T("callLive")}" };
        var root = new Grid { RequestedTheme = Root.ActualTheme, Background = Palette("NomiCanvas"), Padding = new Thickness(16, 14, 16, 14), RowSpacing = 10 };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var head = new Grid { ColumnSpacing = 10 };
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.Children.Add(Avatar(true));
        var names = new StackPanel { Spacing = 1 };
        miniClock = Text($"Nomi · {T("callLive")} {Clock(call.Duration(Now).TotalSeconds)}", 13);
        miniClock.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        names.Children.Add(miniClock);
        var watching = Text(string.Format(T("callWatchingWindow"), call.Window), 12, "NomiInk3");
        watching.MaxLines = 1;
        watching.TextTrimming = TextTrimming.CharacterEllipsis;
        names.Children.Add(watching);
        Grid.SetColumn(names, 1);
        head.Children.Add(names);
        root.Children.Add(head);
        miniCaption = Text(LastNomi(), 13.5, "NomiInk2");
        miniCaption.MaxLines = 3;
        miniCaption.TextTrimming = TextTrimming.CharacterEllipsis;
        AutomationProperties.SetLiveSetting(miniCaption, Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Polite);
        AutomationProperties.SetAutomationId(miniCaption, "CallMiniCaption");
        Grid.SetRow(miniCaption, 1);
        root.Children.Add(miniCaption);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        miniMic = Chip(T("callMic"), "NomiChip", "\uE720", "CallMiniMic");
        miniMic.Click += async (_, _) => await ToggleMic();
        miniPause = Chip(T("callPause"), "NomiChip", "\uE769", "CallMiniPause");
        miniPause.Click += (_, _) => PauseFocus(focusState != "paused");
        var expand = Chip(T("callExpand"), "NomiChip", "\uE740", "CallMiniExpand");
        expand.Click += (_, _) => ExpandCall();
        var hang = Chip(T("callHang"), "NomiPrimary", "\uE778", "CallMiniHang");
        hang.Click += (_, _) => EndCall();
        foreach (var button in new[] { miniMic, miniPause, expand, hang }) buttons.Children.Add(button);
        Grid.SetRow(buttons, 2);
        root.Children.Add(buttons);
        window.Content = root;
        var presenter = OverlappedPresenter.Create();
        presenter.IsAlwaysOnTop = true;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsResizable = false;
        presenter.SetBorderAndTitleBar(true, false);
        window.AppWindow.SetPresenter(presenter);
        var scale = Root.XamlRoot?.RasterizationScale ?? 1;
        window.AppWindow.Resize(new SizeInt32((int)(430 * scale), (int)(176 * scale)));
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        window.AppWindow.Move(new PointInt32(area.X + area.Width - (int)(454 * scale), area.Y + (int)(24 * scale)));
        window.Closed += (_, _) =>
        {
            if (callMini != window) return;
            callMini = null;
            miniCaption = miniClock = null;
            miniMic = miniPause = null;
        };
        callMini = window;
        if (focusWindow?.Handle == IntPtr.Zero) WindowCapture.Exclude(WindowNative.GetWindowHandle(window), true);
        UpdateMini();
        window.Activate();
        (AppWindow.Presenter as OverlappedPresenter)?.Minimize();
    }

    private void CloseMini()
    {
        var window = callMini;
        callMini = null;
        miniCaption = miniClock = null;
        miniMic = miniPause = null;
        window?.Close();
    }

    private void ExpandCall()
    {
        CloseMini();
        (AppWindow.Presenter as OverlappedPresenter)?.Restore();
        Activate();
        if (view != "focus") Show("focus");
    }

    private void UpdateMini()
    {
        if (callMini is null || call is null) return;
        if (miniCaption is not null) miniCaption.Text = LastNomi();
        if (miniClock is not null) miniClock.Text = $"Nomi · {T(focusState == "paused" ? "focusPaused" : "callLive")} {Clock(call.Duration(Now).TotalSeconds)}";
        if (miniMic is not null) AutomationProperties.SetName(miniMic, $"{T("callMic")}, {T(callMic ? "callOn" : "callOff")}");
        if (miniPause?.Content is StackPanel pause && pause.Children[^1] is TextBlock label)
        {
            label.Text = T(focusState == "paused" ? "callResume" : "callPause");
            AutomationProperties.SetName(miniPause, label.Text);
        }
        if (miniMic?.Content is StackPanel mic && mic.Children[0] is FontIcon icon) icon.Glyph = callMic ? "\uE720" : "\uEC54";
    }

    private string LastNomi() =>
        call?.Lines.LastOrDefault(line => line.Who == "nomi" && line.Text.Length > 0)?.Text ?? ActivityLabel();
}
