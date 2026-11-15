using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Nomi;

// The windows that can be shared, with a small preview kept in memory only while choosing.
public sealed partial class MainWindow
{
    private UIElement WindowTiles()
    {
        var stack = new StackPanel { Spacing = 10 };
        var top = new Grid();
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var eyebrow = Eyebrow(T("callShareWhat"));
        eyebrow.VerticalAlignment = VerticalAlignment.Center;
        top.Children.Add(eyebrow);
        var refresh = Chip(T("focusRefresh"), "NomiGhost", "\uE72C", "FocusRefresh");
        refresh.Click += (_, _) => { LoadWindows(); BuildFocus(); };
        Grid.SetColumn(refresh, 1);
        top.Children.Add(refresh);
        stack.Children.Add(top);
        var choices = focusChoices.Take(8).Append(WholeScreen).ToArray();
        var grid = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        for (var column = 0; column < 3; column++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var index = 0; index < choices.Length; index++)
        {
            if (index % 3 == 0) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var tile = Tile(choices[index], index);
            Grid.SetRow(tile, index / 3);
            Grid.SetColumn(tile, index % 3);
            grid.Children.Add(tile);
        }
        stack.Children.Add(grid);
        if (focusChoices.Count == 0) stack.Children.Add(Text(T("focusNoWindows"), 12.5, "NomiInk3"));
        return stack;
    }

    private Button Tile(WindowChoice choice, int index)
    {
        var screen = choice.Handle == IntPtr.Zero;
        var selected = focusWindow is { } current && current.Handle == choice.Handle;
        var content = new StackPanel { Spacing = 8 };
        content.Children.Add(TileImage(choice));
        var title = Text(screen ? T("callWholeScreen") : choice.Title, 13.5);
        title.FontWeight = FontWeights.SemiBold;
        title.MaxLines = 1;
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        content.Children.Add(title);
        var sub = Text(screen ? T("callWholeScreenNote") : choice.Process, 12, "NomiInk3");
        sub.MaxLines = 1;
        sub.TextTrimming = TextTrimming.CharacterEllipsis;
        content.Children.Add(sub);
        var tile = new Button
        {
            Content = content,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Top,
            Padding = new Thickness(10, 10, 10, 12),
            CornerRadius = new CornerRadius(16),
            Background = Palette("NomiGlassStrong"),
            BorderBrush = Palette(selected ? "NomiAccent" : "NomiGlassEdge"),
            BorderThickness = new Thickness(selected ? 2 : 1)
        };
        AutomationProperties.SetName(tile, $"{title.Text}, {sub.Text}{(selected ? $", {T("callSelected")}" : "")}");
        AutomationProperties.SetAutomationId(tile, screen ? "FocusWindow_screen" : $"FocusWindow_{index}");
        tile.Click += (_, _) =>
        {
            focusWindow = choice;
            focusMessage = "";
            BuildFocus();
        };
        Springy(tile, 1.02f);
        return tile;
    }

    private FrameworkElement TileImage(WindowChoice choice)
    {
        FrameworkElement image;
        if (choice.Handle == IntPtr.Zero)
            image = new Border
            {
                Height = 96,
                CornerRadius = new CornerRadius(10),
                Background = Palette("NomiSurface"),
                Child = new FontIcon { Glyph = "\uE7F4", FontSize = 26, Foreground = Palette("NomiInk3") }
            };
        else if (thumbnails.TryGetValue(choice.Handle, out var bitmap))
            image = new Border
            {
                Height = 96,
                CornerRadius = new CornerRadius(10),
                Background = Palette("NomiSurface"),
                Child = new Image { Source = bitmap, Stretch = Stretch.UniformToFill, VerticalAlignment = VerticalAlignment.Top }
            };
        else image = Thumbnail(choice.Process);
        AutomationProperties.SetAccessibilityView(image, AccessibilityView.Raw);
        return image;
    }

    private async Task LoadThumbnails()
    {
        var choices = focusChoices.Take(8).ToArray();
        var frames = await Task.Run(() => choices.Select(choice =>
        {
            try
            {
                var frame = WindowCapture.Capture(choice.Handle);
                var small = frame.Fit(360);
                if (small != frame) Array.Clear(frame.Pixels);
                return (choice.Handle, Frame: (ScreenFrame?)small);
            }
            catch (Exception error) when (error is not OutOfMemoryException) { return (choice.Handle, Frame: null); }
        }).ToArray());
        ClearThumbnails();
        if (focusState != "idle")
        {
            foreach (var (_, frame) in frames) if (frame is not null) Array.Clear(frame.Pixels);
            return;
        }
        foreach (var (handle, frame) in frames)
        {
            if (frame is null) continue;
            var bitmap = new WriteableBitmap(frame.Width, frame.Height);
            using (var stream = bitmap.PixelBuffer.AsStream()) stream.Write(frame.Pixels, 0, frame.Pixels.Length);
            bitmap.Invalidate();
            Array.Clear(frame.Pixels);
            thumbnails[handle] = bitmap;
        }
        if (view == "focus" && focusScreen == "pre") BuildFocus();
    }

    private void ClearThumbnails() => thumbnails.Clear();

    private Border Thumbnail(string process)
    {
        var sheet = process.Contains("excel", StringComparison.OrdinalIgnoreCase) || process.Contains("calc", StringComparison.OrdinalIgnoreCase);
        var lines = new Grid { Margin = new Thickness(10, 8, 10, 8), IsHitTestVisible = false };
        var ink = Palette(sheet ? "NomiSage" : "NomiInk3");
        for (var index = 0; index < 6; index++)
        {
            lines.Children.Add(new Border
            {
                Height = 1,
                Background = ink,
                Opacity = sheet ? 0.45 : 0.25,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, index * 13, sheet ? 0 : (index % 3) * 22, 0)
            });
        }
        return new Border { Height = 96, CornerRadius = new CornerRadius(10), Background = Palette(sheet ? "NomiSageSoft" : "NomiSurface"), Child = lines };
    }

    private Border VisionCard()
    {
        var stack = new StackPanel { Spacing = 8 };
        var row = new Grid { ColumnSpacing = 12 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var preparing = visionPreparation is not null;
        var info = new StackPanel { Spacing = 2 };
        var name = Text(T("visionTitle"), 13.5);
        name.FontWeight = FontWeights.SemiBold;
        info.Children.Add(name);
        var status = visionProgress.Stage == "model-downloading" ? $"{T("engineDownloading")} · {Readable.Fraction(visionProgress.Fraction, language)}"
            : preparing ? T("engineBusy")
            : string.Format(T("visionMissing"), Gigabytes(vision.Bytes));
        var state = Text($"{vision.Model} · {status}", 12, "NomiInk3");
        AutomationProperties.SetAutomationId(state, "VisionStatus");
        AutomationProperties.SetLiveSetting(state, AutomationLiveSetting.Polite);
        info.Children.Add(state);
        row.Children.Add(info);
        var action = preparing ? Chip(T("stop"), "NomiGhost", null, "VisionCancel") : Chip(T("visionDownload"), "NomiChip", "\uE896", "VisionDownload");
        action.VerticalAlignment = VerticalAlignment.Center;
        action.Click += async (_, _) =>
        {
            if (visionPreparation is not null) visionPreparation.Cancel();
            else await PrepareVision(true);
        };
        Grid.SetColumn(action, 1);
        row.Children.Add(action);
        stack.Children.Add(row);
        if (preparing)
        {
            var bar = new ProgressBar { IsIndeterminate = visionProgress.Stage != "model-downloading", Value = visionProgress.Fraction * 100, Height = 4, MinHeight = 4 };
            AutomationProperties.SetName(bar, T("visionTitle"));
            stack.Children.Add(bar);
        }
        else if (visionProgress.Stage is not ("vision-idle" or "local-model-ready" or "model-downloading"))
            stack.Children.Add(Text(Label(visionProgress.Stage), 12, "NomiAccent"));
        stack.Children.Add(Text(T("visionNote"), 11.5, "NomiInk3"));
        var card = Glass(stack, 16);
        Mark(card, "VisionCard", T("visionTitle"));
        return card;
    }
}
