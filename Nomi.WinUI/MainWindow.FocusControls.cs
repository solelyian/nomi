using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;

namespace Nomi;

public sealed partial class MainWindow
{
    private StackPanel CallControls()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
        var paused = focusState == "paused";
        row.Children.Add(CallButton(callMic ? "\uE720" : "\uEC54", T("callMic"), callMic, "Ctrl+M", "CallMic", () => _ = ToggleMic()));
        row.Children.Add(CallButton(callVoice ? "\uE767" : "\uE74F", T("callVoice"), callVoice, "Ctrl+Shift+V", "CallVoice", ToggleVoice));
        row.Children.Add(CallButton(paused ? "\uE768" : "\uE769", T(paused ? "callResume" : "callPause"), null, "Ctrl+Shift+P", "CallPause", () => PauseFocus(!paused)));
        var change = CallButton("\uE8AB", T("callChange"), null, null, "CallChange", null);
        change.Flyout = WindowMenu();
        row.Children.Add(change);
        row.Children.Add(CallButton("\uE8A7", T("callMini"), null, "Ctrl+Shift+F", "CallMiniOpen", OpenMini));
        row.Children.Add(CallButton("\uE778", T("callHang"), null, "Ctrl+Shift+H", "CallHang", EndCall, hang: true));
        return row;
    }

    private Button CallButton(string glyph, string label, bool? on, string? keys, string automation, Action? click, bool hang = false)
    {
        var content = new StackPanel { Spacing = 5 };
        content.Children.Add(new FontIcon { Glyph = glyph, FontSize = 18, HorizontalAlignment = HorizontalAlignment.Center });
        content.Children.Add(new TextBlock { Text = label, FontSize = 12, FontFamily = readingFont, HorizontalAlignment = HorizontalAlignment.Center });
        var background = Palette(hang ? "NomiAccent" : on == false ? "NomiSurface2" : "NomiGlassStrong");
        var ink = Palette(hang ? "NomiAccentInk" : on == false ? "NomiInk3" : "NomiInk");
        var button = new Button
        {
            Content = content,
            Padding = new Thickness(14, 10, 14, 10),
            MinWidth = 96,
            CornerRadius = new CornerRadius(14),
            BorderThickness = new Thickness(1),
            BorderBrush = Palette("NomiGlassEdge"),
            Background = background,
            Foreground = ink
        };
        if (hang)
        {
            foreach (var state in new[] { "PointerOver", "Pressed" })
            {
                button.Resources[$"ButtonBackground{state}"] = background;
                button.Resources[$"ButtonForeground{state}"] = ink;
            }
        }
        AutomationProperties.SetName(button, on is null ? label : $"{label}, {T(on.Value ? "callOn" : "callOff")}");
        AutomationProperties.SetAutomationId(button, automation);
        if (keys is not null)
        {
            AutomationProperties.SetAcceleratorKey(button, keys);
            ToolTipService.SetToolTip(button, $"{label} · {keys}");
        }
        if (click is not null) button.Click += (_, _) => click();
        Springy(button, 1.03f);
        return button;
    }

    private MenuFlyout WindowMenu()
    {
        var menu = new MenuFlyout();
        menu.Opening += (_, _) =>
        {
            menu.Items.Clear();
            focusChoices = WindowCapture.List(WindowNative.GetWindowHandle(this));
            foreach (var choice in focusChoices.Take(12).Append(WholeScreen))
            {
                var item = new MenuFlyoutItem { Text = WindowName(choice) };
                item.Click += (_, _) => ChangeWindow(choice);
                menu.Items.Add(item);
            }
        };
        menu.Items.Add(new MenuFlyoutItem { Text = T("callWholeScreen") });
        return menu;
    }
}
