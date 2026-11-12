using System.Globalization;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;

namespace Nomi;

// During the call: the live preview, what Nomi is doing, and the call controls.
public sealed partial class MainWindow
{
    private Grid? callGrid;
    private Image? previewImage;
    private FrameworkElement? previewWaiting;
    private TextBlock? callClock;
    private TextBlock? callSeen;
    private TextBlock? callActivityText;
    private TextBlock? callMessage;
    private bool focusSized;

    private double CallHeight() => Math.Max(560, FocusPanel.ActualHeight - 14);

    private Grid CallView()
    {
        var grid = new Grid { ColumnSpacing = 16, Height = CallHeight() };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(340) });
        if (!focusSized)
        {
            focusSized = true;
            FocusPanel.SizeChanged += (_, _) => { if (callGrid is not null) callGrid.Height = CallHeight(); };
        }
        callGrid = grid;
        var stage = new Grid { RowSpacing = 12 };
        stage.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        stage.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        stage.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        stage.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        UIElement[] parts = [CallHeader(), PreviewView(), FrameLine(), CallControls()];
        for (var index = 0; index < parts.Length; index++)
        {
            Grid.SetRow((FrameworkElement)parts[index], index);
            stage.Children.Add(parts[index]);
        }
        grid.Children.Add(stage);
        var side = CaptionsView();
        Grid.SetColumn(side, 1);
        grid.Children.Add(side);
        return grid;
    }

    private Grid CallHeader()
    {
        var row = new Grid { ColumnSpacing = 12 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var live = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        var dot = new Ellipse { Width = 9, Height = 9, Fill = Palette(focusState == "paused" ? "NomiInk3" : "NomiAccent"), VerticalAlignment = VerticalAlignment.Center };
        if (motion && focusState == "watching")
        {
            var breathe = new Storyboard { RepeatBehavior = RepeatBehavior.Forever, AutoReverse = true };
            breathe.Children.Add(Animate(dot, "Opacity", 1, 0.35, 1100));
            breathe.Begin();
        }
        live.Children.Add(dot);
        var state = Text(T(focusState == "paused" ? "focusPaused" : "callLive"), 14);
        state.FontWeight = FontWeights.SemiBold;
        live.Children.Add(state);
        callClock = Text(Clock(call!.Duration(Now).TotalSeconds), 14, "NomiInk2");
        callClock.FontFamily = (FontFamily)Application.Current.Resources["NomiMono"];
        live.Children.Add(callClock);
        row.Children.Add(live);
        var sharing = Text(string.Format(T("callSharing"), call.Window), 13, "NomiInk3");
        sharing.MaxLines = 1;
        sharing.TextTrimming = TextTrimming.CharacterEllipsis;
        sharing.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(sharing, 1);
        row.Children.Add(sharing);
        var local = Pill(T("callLocal"), "NomiSageSoft", "NomiOk");
        local.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(local, 2);
        row.Children.Add(local);
        return row;
    }

    private Border PreviewView()
    {
        var host = new Grid();
        previewImage = new Image { Stretch = Stretch.Uniform, Source = callPreview, Opacity = focusState == "paused" ? 0.3 : 1 };
        AutomationProperties.SetName(previewImage, string.Format(T("callPreviewName"), call?.Window));
        AutomationProperties.SetAutomationId(previewImage, "CallPreview");
        host.Children.Add(previewImage);
        previewWaiting = null;
        if (focusState == "paused") host.Children.Add(Centered(T("focusPausedLong"), "\uE769"));
        else if (callPreview is null)
        {
            var waiting = Centered(T("callFirstImage"), "\uE7B3");
            previewWaiting = waiting;
            host.Children.Add(waiting);
        }
        if (call?.Points.LastOrDefault(point => !point.Fixed && point.Window == call.Window) is { } open)
        {
            host.Children.Add(new Border
            {
                Background = Palette("NomiAccent"),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 4, 10, 5),
                Margin = new Thickness(12),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Child = new TextBlock { Text = string.Format(T("callLookHere"), open.Issue.Row), FontSize = 12, FontFamily = readingFont, Foreground = Palette("NomiAccentInk") }
            });
        }
        return new Border
        {
            Background = Palette("NomiSurface2"),
            BorderBrush = Palette("NomiGlassEdge"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(8),
            Child = host
        };
    }

    private StackPanel Centered(string text, string glyph)
    {
        var stack = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(new FontIcon { Glyph = glyph, FontSize = 22, Foreground = Palette("NomiInk3") });
        var label = Text(text, 13, "NomiInk3");
        label.TextAlignment = TextAlignment.Center;
        stack.Children.Add(label);
        return stack;
    }

    private Grid FrameLine()
    {
        var row = new Grid { ColumnSpacing = 12, RowSpacing = 4 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var activity = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        activity.Children.Add(new FontIcon { Glyph = callMic ? "\uE720" : "\uE7B3", FontSize = 13, Foreground = Palette("NomiAccent") });
        callActivityText = Text(ActivityLabel(), 13, "NomiInk2");
        AutomationProperties.SetAutomationId(callActivityText, "CallActivity");
        AutomationProperties.SetLiveSetting(callActivityText, Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Polite);
        activity.Children.Add(callActivityText);
        row.Children.Add(activity);
        var rate = Text(T("callRate"), 12.5, "NomiInk3");
        rate.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(rate, 1);
        row.Children.Add(rate);
        callSeen = Text(SeenLabel(), 12.5, "NomiInk3");
        callSeen.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(callSeen, 2);
        row.Children.Add(callSeen);
        callMessage = Text(focusMessage, 12.5, "NomiAccent");
        callMessage.Visibility = focusMessage.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        AutomationProperties.SetAutomationId(callMessage, "FocusMessage");
        AutomationProperties.SetLiveSetting(callMessage, Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Polite);
        Grid.SetRow(callMessage, 1);
        Grid.SetColumnSpan(callMessage, 3);
        row.Children.Add(callMessage);
        return row;
    }

    private string SeenLabel() =>
        string.Format(T("callLastSeen"), call?.LastSeen is { } seen ? seen.ToString("HH:mm:ss", CultureInfo.CurrentCulture) : "—");
}
