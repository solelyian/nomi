using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

namespace Nomi;

public sealed partial class MainWindow
{
    private UIElement NoModelState()
    {
        var stack = new StackPanel { Spacing = 14, MaxWidth = 680, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(20, 0, 20, 40) };
        var hello = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14, HorizontalAlignment = HorizontalAlignment.Center };
        hello.Children.Add(NomiOrb(38));
        var title = new TextBlock { Text = T("nomiNeedsModel"), FontSize = 26, FontWeight = FontWeights.Bold, FontFamily = readingFont, TextWrapping = TextWrapping.Wrap, Foreground = Palette("NomiInk"), VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetHeadingLevel(title, Microsoft.UI.Xaml.Automation.Peers.AutomationHeadingLevel.Level2);
        hello.Children.Add(title);
        stack.Children.Add(hello);
        var intro = Text(T("nomiNeedsModelText"), 14.5, "NomiInk2");
        intro.TextAlignment = TextAlignment.Center;
        stack.Children.Add(intro);
        stack.Children.Add(HardwareCard());
        if (modelPreparation is not null)
        {
            var bar = new ProgressBar { Value = modelProgress.Fraction * 100, IsIndeterminate = modelProgress.Fraction <= 0, Maximum = 100 };
            AutomationProperties.SetName(bar, T("nomiDownloading"));
            stack.Children.Add(bar);
            stack.Children.Add(Text(T("nomiDownloading"), 12.5, "NomiInk3"));
        }
        else
        {
            var download = Chip(T("nomiDownload"), "NomiPrimary", "\uE896", "NomiDownload");
            download.HorizontalAlignment = HorizontalAlignment.Center;
            download.Click += async (_, _) =>
            {
                var task = PrepareModel();
                BuildNomi();
                await task;
                if (view == "nomi") BuildNomi();
            };
            stack.Children.Add(download);
        }
        stack.HorizontalAlignment = HorizontalAlignment.Center;
        return new ScrollViewer { Content = stack, HorizontalScrollMode = ScrollMode.Disabled, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalContentAlignment = VerticalAlignment.Center };
    }

    private async Task AttachNomiDocuments()
    {
        if (nomiRun is not null) return;
        try
        {
            var picker = new FileOpenPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            foreach (var extension in DocumentText.Extensions) picker.FileTypeFilter.Add(extension);
            var selection = await picker.PickMultipleFilesAsync();
            if (selection.Count + nomiDocuments.Count > DocumentText.MaxFiles) throw new InferenceException("document-limit");
            foreach (var file in selection)
            {
                using var stream = await file.OpenStreamForReadAsync();
                if (stream.Length > DocumentText.MaxBytes) throw new InferenceException("document-size");
                var bytes = new byte[(int)stream.Length];
                await stream.ReadExactlyAsync(bytes);
                var document = await Task.Run(() => DocumentText.Read(file.Name, bytes, CancellationToken.None));
                DocumentText.Validate([.. nomiDocuments, document]);
                nomiDocuments.Add(document);
            }
        }
        catch (InferenceException error) { ShowToast(T(error.Message), null, null); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or COMException) { ShowToast(T("document-unreadable"), null, null); }
        BuildNomi();
        DispatcherQueue.TryEnqueue(() => nomiBox?.Focus(FocusState.Programmatic));
    }

    private async Task Regenerate(ChatEntry answer)
    {
        if (openThread is null || nomiRun is not null) return;
        var index = openThread.Items.IndexOf(answer);
        var question = openThread.Items.Take(Math.Max(0, index)).LastOrDefault(item => item.Role == "user");
        if (question is null) return;
        openThread.Items.Remove(answer);
        threads.Save();
        await SendNomi(question.Text, question);
    }
}
