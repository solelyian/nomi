using System.Globalization;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Storage.Pickers;

namespace Nomi;

// Live transcription: the microphone feeds the local Whisper model, each utterance lands here timestamped. Nothing leaves the PC.
public sealed partial class MainWindow
{
    private sealed record TranscriptLine(TimeSpan At, string Text);

    private readonly List<TranscriptLine> transcriptLines = [];
    private Microphone? transcriptMic;
    private CancellationTokenSource? transcriptRun;
    private DispatcherTimer? transcriptTicker;
    private DateTimeOffset? transcriptStarted;
    private TextBlock? transcriptClock;
    private bool transcriptRunning;
    private bool transcriptBusy;
    private string transcriptMessage = "";

    private void BuildTranscript()
    {
        TranscriptBody.Children.Clear();
        var titles = new StackPanel { Spacing = 4 };
        titles.Children.Add(Eyebrow(T("transcriptEyebrow")));
        titles.Children.Add(Heading(T("transcript"), 28));
        titles.Children.Add(Text(T("transcriptIntro"), 13.5, "NomiInk2"));
        TranscriptBody.Children.Add(titles);

        var control = new StackPanel { Spacing = 10 };
        var state = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        if (transcriptRunning)
        {
            state.Children.Add(new FontIcon { Glyph = "", FontSize = 13, Foreground = Palette("NomiAccent"), VerticalAlignment = VerticalAlignment.Center });
            var status = Text(T(transcriptBusy ? "transcriptTranscribing" : "transcriptListening"), 13);
            status.VerticalAlignment = VerticalAlignment.Center;
            state.Children.Add(status);
            transcriptClock = Text(transcriptStarted is { } at ? Clock((Now - at).TotalSeconds) : "", 13, "NomiInk2");
            transcriptClock.FontFamily = (FontFamily)Application.Current.Resources["NomiMono"];
            transcriptClock.VerticalAlignment = VerticalAlignment.Center;
            state.Children.Add(transcriptClock);
        }
        else
        {
            state.Children.Add(new FontIcon { Glyph = "", FontSize = 13, Foreground = Palette("NomiInk3"), VerticalAlignment = VerticalAlignment.Center });
            var status = Text(transcriptLines.Count > 0 ? T("transcriptStopped") : T("transcriptIdle"), 13, "NomiInk2");
            status.VerticalAlignment = VerticalAlignment.Center;
            state.Children.Add(status);
        }
        control.Children.Add(state);
        if (!Microphone.Available) control.Children.Add(Text(T("micUnavailable"), 12.5, "NomiAccent"));
        else if (!speech.HasModel)
        {
            control.Children.Add(Text(T("transcriptNeedModel"), 12.5, "NomiInk2"));
            var download = Primary(T("transcriptDownload"), "TranscriptDownload");
            download.Click += async (_, _) => await TranscriptDownload();
            control.Children.Add(download);
        }
        else
        {
            var toggle = Primary(T(transcriptRunning ? "transcriptStop" : "transcriptStart"), "TranscriptToggle");
            toggle.Click += async (_, _) => await TranscriptToggle();
            control.Children.Add(toggle);
        }
        if (transcriptMessage.Length > 0) control.Children.Add(Text(transcriptMessage, 12.5, "NomiAccent"));
        control.Children.Add(Pill(T("callLocal"), "NomiSageSoft", "NomiOk"));
        var card = Glass(control, 18);
        Mark(card, "TranscriptControl", T("transcript"));
        TranscriptBody.Children.Add(card);

        var lines = new StackPanel { Spacing = 8 };
        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        head.Children.Add(Eyebrow(T("transcriptLines")));
        if (transcriptLines.Count > 0)
        {
            var count = Text(string.Format(CultureInfo.CurrentCulture, T("transcriptSegments"), transcriptLines.Count), 12, "NomiInk3");
            count.VerticalAlignment = VerticalAlignment.Center;
            head.Children.Add(count);
        }
        lines.Children.Add(head);
        if (transcriptLines.Count == 0) lines.Children.Add(Text(T("transcriptEmpty"), 13, "NomiInk3"));
        foreach (var line in transcriptLines)
        {
            var row = new Grid { ColumnSpacing = 10 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var at = Text(line.At.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture), 12.5, "NomiInk3");
            at.FontFamily = (FontFamily)Application.Current.Resources["NomiMono"];
            row.Children.Add(at);
            var body = Text(line.Text, 13.5);
            Grid.SetColumn(body, 1);
            row.Children.Add(body);
            lines.Children.Add(row);
        }
        if (transcriptLines.Count > 0)
        {
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 6, 0, 0) };
            var copy = Chip(T("transcriptCopy"), "NomiChip", null, "TranscriptCopy");
            copy.Click += (_, _) => transcriptMessage = CopyResponse(TranscriptText());
            var save = Chip(T("transcriptSave"), "NomiChip", null, "TranscriptSave");
            save.Click += async (_, _) => await TranscriptSave();
            var clear = Chip(T("transcriptClear"), "NomiGhost", null, "TranscriptClear");
            clear.Click += (_, _) => { transcriptLines.Clear(); transcriptMessage = ""; RefreshTranscript(); };
            buttons.Children.Add(copy);
            buttons.Children.Add(save);
            buttons.Children.Add(clear);
            lines.Children.Add(buttons);
        }
        var card2 = Glass(lines, 18);
        Mark(card2, "TranscriptLines", T("transcriptLines"));
        TranscriptBody.Children.Add(card2);
    }

    private string TranscriptText() =>
        string.Join("\n", transcriptLines.Select(line => $"[{line.At:hh\\:mm\\:ss}] {line.Text}"));

    private void RefreshTranscript()
    {
        if (view == "transcript") BuildTranscript();
    }

    private async Task TranscriptDownload()
    {
        transcriptMessage = T("transcriptLoading");
        RefreshTranscript();
        await PrepareSpeech(true);
        transcriptMessage = speech.HasModel ? "" : T("speech-missing");
        RefreshTranscript();
    }

    private async Task TranscriptToggle()
    {
        if (transcriptRunning) { TranscriptStop(); return; }
        transcriptMessage = "";
        if (!Microphone.Available)
        {
            transcriptMessage = T("micUnavailable");
            RefreshTranscript();
            return;
        }
        if (!speech.HasModel)
        {
            transcriptMessage = T("transcriptNeedModel");
            RefreshTranscript();
            return;
        }
        transcriptMessage = T("transcriptLoading");
        RefreshTranscript();
        try { await speech.PrepareAsync(false, null, CancellationToken.None); }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            transcriptMessage = Label(error is InferenceException known ? known.Message : "speech-missing");
            RefreshTranscript();
            return;
        }
        transcriptMessage = "";
        transcriptRun?.Cancel();
        transcriptRun = new CancellationTokenSource();
        if (transcriptMic is null)
        {
            transcriptMic = new Microphone();
            transcriptMic.SpeechStarted += () => DispatcherQueue.TryEnqueue(() => { if (transcriptRunning) RefreshTranscript(); });
            transcriptMic.Utterance += samples => DispatcherQueue.TryEnqueue(async () => await TranscriptSegment(samples));
            transcriptMic.Failed += _ => DispatcherQueue.TryEnqueue(() => { transcriptMessage = T("micError"); TranscriptStop(); });
        }
        transcriptStarted = Now;
        transcriptRunning = transcriptMic.Start();
        if (!transcriptRunning) transcriptMessage = T("micUnavailable");
        else
        {
            transcriptTicker ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            transcriptTicker.Tick -= TranscriptTick;
            transcriptTicker.Tick += TranscriptTick;
            transcriptTicker.Start();
        }
        RefreshTranscript();
    }

    private void TranscriptTick(object? sender, object args)
    {
        if (transcriptClock is not null && transcriptStarted is { } at)
            transcriptClock.Text = Clock((Now - at).TotalSeconds);
    }

    private async Task TranscriptSegment(float[] samples)
    {
        try
        {
            if (!transcriptRunning) return;
            transcriptBusy = true;
            RefreshTranscript();
            var text = await speech.TranscribeAsync(samples, language, transcriptRun?.Token ?? CancellationToken.None);
            if (text.Length > 0 && transcriptStarted is { } started) transcriptLines.Add(new(Now - started, text));
        }
        catch (OperationCanceledException) { }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            if (error is not InferenceException) StartupDiagnostics.Write($"Transcript speech: {error.GetType().Name} (0x{error.HResult:X8})");
        }
        finally
        {
            transcriptBusy = false;
            Array.Clear(samples);
        }
        RefreshTranscript();
    }

    private void TranscriptStop()
    {
        transcriptRunning = false;
        transcriptTicker?.Stop();
        transcriptMic?.Stop();
        transcriptRun?.Cancel();
        transcriptRun?.Dispose();
        transcriptRun = null;
        _ = speech.UnloadAsync();
        RefreshTranscript();
        BuildRail();
    }

    private async Task TranscriptSave()
    {
        var picker = new FileSavePicker { SuggestedFileName = $"transcription-{Now:yyyy-MM-dd-HHmm}" };
        picker.FileTypeChoices.Add("Text", [".txt"]);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
        var file = await picker.PickSaveFileAsync();
        if (file is null) return;
        await Windows.Storage.FileIO.WriteTextAsync(file, TranscriptText());
        transcriptMessage = T("transcriptSaved");
        RefreshTranscript();
    }
}
