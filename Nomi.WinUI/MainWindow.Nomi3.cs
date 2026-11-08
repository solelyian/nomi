using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Nomi;

public sealed partial class MainWindow
{
    private void NewThread()
    {
        if (nomiRun is not null) return;
        openThread = null;
        detailsEntry = null;
        nomiDocuments.Clear();
        BuildNomi();
        DispatcherQueue.TryEnqueue(() => nomiBox?.Focus(FocusState.Programmatic));
    }

    private void OpenThread(ChatThread thread)
    {
        if (nomiRun is not null) return;
        openThread = thread;
        detailsEntry = null;
        BuildNomi();
    }

    private async void RenameThread(ChatThread thread, FrameworkElement anchor)
    {
        var box = new TextBox { Text = thread.Title, MaxLength = 80 };
        AutomationProperties.SetName(box, T("nomiRename"));
        var dialog = new ContentDialog
        {
            XamlRoot = Root.XamlRoot, Title = T("nomiRename"), Content = box,
            PrimaryButtonText = T("nomiSave"), CloseButtonText = T("nomiCancel"), DefaultButton = ContentDialogButton.Primary
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary || box.Text.Trim().Length == 0) return;
        thread.Title = box.Text.Trim();
        threads.Save();
        BuildNomi();
    }

    private void DeleteThread(ChatThread thread)
    {
        if (nomiRun is not null) return;
        var index = threads.Threads.IndexOf(thread);
        threads.Delete(thread);
        if (openThread == thread) openThread = null;
        BuildNomi();
        ShowToast(string.Format(CultureInfo.CurrentCulture, T("nomiDeleted"), thread.Title), T("undo"), () =>
        {
            threads.Threads.Insert(Math.Clamp(index, 0, threads.Threads.Count), thread);
            threads.Save();
            if (view == "nomi") BuildNomi();
        });
    }

    private static Ellipse NomiOrb(double size, bool live = false)
    {
        var brush = new RadialGradientBrush { Center = new Windows.Foundation.Point(0.35, 0.3), GradientOrigin = new Windows.Foundation.Point(0.35, 0.3), RadiusX = 0.75, RadiusY = 0.75 };
        brush.GradientStops.Add(new GradientStop { Color = ColorHelper.FromArgb(255, 0xFF, 0xD2, 0xBF), Offset = 0 });
        brush.GradientStops.Add(new GradientStop { Color = ColorHelper.FromArgb(255, 0xE9, 0x8B, 0x66), Offset = 0.45 });
        brush.GradientStops.Add(new GradientStop { Color = ColorHelper.FromArgb(255, 0xB4, 0x48, 0x2B), Offset = 1 });
        var orb = new Ellipse { Width = size, Height = size, Fill = brush, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 0, 0) };
        AutomationProperties.SetAccessibilityView(orb, Microsoft.UI.Xaml.Automation.Peers.AccessibilityView.Raw);
        if (live) orb.Opacity = 0.85;
        return orb;
    }

    private UIElement NomiWelcome()
    {
        var stack = new StackPanel { Spacing = 0, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(20, 0, 20, 60), MaxWidth = 720 };
        var hello = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14, HorizontalAlignment = HorizontalAlignment.Center };
        hello.Children.Add(NomiOrb(38));
        var title = new TextBlock { Text = T("nomiHello"), FontSize = 34, FontWeight = FontWeights.Bold, CharacterSpacing = -25, FontFamily = readingFont, Foreground = Palette("NomiInk"), VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetHeadingLevel(title, Microsoft.UI.Xaml.Automation.Peers.AutomationHeadingLevel.Level2);
        hello.Children.Add(title);
        stack.Children.Add(hello);
        var context = ChatContextNow();
        var line = Text(chatHidden ? T("chatHidden") : context.Count == 0 ? T("chatSeesNone") : context.Count == 1 ? T("chatSeesOne")
            : string.Format(CultureInfo.CurrentCulture, T("chatSees"), context.Count), 14.5, "NomiInk2");
        line.TextAlignment = TextAlignment.Center;
        line.Margin = new Thickness(0, 6, 0, 26);
        stack.Children.Add(line);
        stack.Children.Add(NomiComposer());
        var suggestions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 16, 0, 0) };
        foreach (var (key, glyph) in new[] { ("nomiSuggest1", "\uE8BF"), ("nomiSuggest2", "\uE823"), ("nomiSuggest3", "\uE8A5"), ("nomiSuggest4", "\uE8FD") })
        {
            var text = T(key);
            var chip = Chip(text, "NomiChip", glyph, $"NomiSuggest_{key[^1]}");
            chip.FontSize = 13;
            chip.Click += async (_, _) => await SendNomi(text);
            suggestions.Children.Add(chip);
        }
        stack.Children.Add(new ScrollViewer { Content = suggestions, HorizontalScrollMode = ScrollMode.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollMode = ScrollMode.Disabled, HorizontalAlignment = HorizontalAlignment.Center });
        return stack;
    }
}
