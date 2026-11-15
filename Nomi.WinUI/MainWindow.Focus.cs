using System.Globalization;
using System.Net.Http;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.System;
using WinRT.Interop;

namespace Nomi;

// Focus écran as a call: the user shares a window, talks or writes to Nomi, and Nomi answers about what it sees.
public sealed partial class MainWindow
{
    private const int PreviewSide = 1280;
    private readonly VisionClient vision = new();
    private readonly SpeechModel speech = new();
    private CancellationTokenSource? visionPreparation;
    private ModelProgress visionProgress = new("vision-idle");
    private CancellationTokenSource? speechPreparation;
    private ModelProgress speechProgress = new("vision-idle");
    private string focusState = "idle";
    private string focusScreen = "pre";
    private WindowChoice? focusWindow;
    private IReadOnlyList<WindowChoice> focusChoices = [];
    private bool focusListed;
    private FocusCall? call;
    private FocusCall? endedCall;
    private ScreenInsight? insight;
    private TableReport? tableReport;
    private DateTimeOffset focusStarted;
    private string focusMessage = "";
    private string? focusTaskId;
    private bool callMic = true;
    private bool callVoice = true;
    private string callMode = CallModes.Problem;
    private string callActivity = "listening";
    private bool callKeys;
    private readonly Dictionary<IntPtr, WriteableBitmap> thumbnails = [];
    private static readonly WindowChoice WholeScreen = new(IntPtr.Zero, "", "screen");

    private string Label(string code) => Current.Labels.TryGetValue(code, out var value) ? value : T("focusCaptureError");

    private string WindowName(WindowChoice choice) => choice.Handle == IntPtr.Zero ? T("callWholeScreen") : $"{choice.Process} — {choice.Title}";

    private static ScreenFrame CaptureChoice(WindowChoice choice) =>
        choice.Handle == IntPtr.Zero ? WindowCapture.CaptureScreen() : WindowCapture.Capture(choice.Handle);

    private void BuildFocus()
    {
        FocusBody.Children.Clear();
        ForgetCallView();
        SetupCallKeys();
        if (focusScreen == "call" && call is not null) FocusBody.Children.Add(CallView());
        else if (focusScreen == "end" && endedCall is not null) FocusBody.Children.Add(EndView(endedCall));
        else
        {
            focusScreen = "pre";
            FocusBody.Children.Add(PreCallView());
        }
        Reveal(FocusBody.Children[0], 10);
    }

    private void SetupCallKeys()
    {
        if (callKeys) return;
        callKeys = true;
        void Key(VirtualKey key, VirtualKeyModifiers modifiers, Action run)
        {
            var accelerator = new KeyboardAccelerator { Key = key, Modifiers = modifiers };
            accelerator.Invoked += (_, args) =>
            {
                if (call is null) return;
                args.Handled = true;
                run();
            };
            Root.KeyboardAccelerators.Add(accelerator);
        }
        var shift = VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift;
        Key(VirtualKey.M, VirtualKeyModifiers.Control, () => _ = ToggleMic());
        Key(VirtualKey.V, shift, ToggleVoice);
        Key(VirtualKey.P, shift, () => PauseFocus(focusState != "paused"));
        Key(VirtualKey.F, shift, OpenMini);
        Key(VirtualKey.H, shift, EndCall);
    }

    private async Task PrepareVision(bool download, bool load = true)
    {
        if (visionPreparation is not null) return;
        using var controller = new CancellationTokenSource();
        visionPreparation = controller;
        visionProgress = new("model-verifying");
        if (view == "focus") BuildFocus();
        visionProgress = await Prepared(controller, value => visionProgress = value, () => visionPreparation == controller,
            token => load ? vision.PrepareAsync(download, Reporter(controller, () => visionPreparation, value => visionProgress = value, () => visionProgress), token)
                : vision.DownloadAsync(Reporter(controller, () => visionPreparation, value => visionProgress = value, () => visionProgress), token), "Vision");
        visionPreparation = null;
        if (focusState == "idle" && vision.Ready) await vision.UnloadAsync();
        if (view == "focus") BuildFocus();
        ResourcesChanged();
    }

    private async Task PrepareSpeech(bool download, bool load = true)
    {
        if (speechPreparation is not null) return;
        using var controller = new CancellationTokenSource();
        speechPreparation = controller;
        speechProgress = new("model-verifying");
        if (view == "focus" && focusScreen == "pre") BuildFocus();
        speechProgress = await Prepared(controller, value => speechProgress = value, () => speechPreparation == controller,
            token => load ? speech.PrepareAsync(download, Reporter(controller, () => speechPreparation, value => speechProgress = value, () => speechProgress), token)
                : speech.DownloadAsync(Reporter(controller, () => speechPreparation, value => speechProgress = value, () => speechProgress), token), "Speech");
        speechPreparation = null;
        if (focusState == "idle" && speech.Ready) await speech.UnloadAsync();
        if (view == "focus" && focusScreen != "call") BuildFocus();
        ResourcesChanged();
    }

    private IProgress<ModelProgress> Reporter(CancellationTokenSource controller, Func<CancellationTokenSource?> current,
        Action<ModelProgress> set, Func<ModelProgress> get) => new Progress<ModelProgress>(value =>
    {
        if (current() != controller) return;
        var before = get();
        var redraw = value.Stage != before.Stage || Math.Abs(value.Fraction - before.Fraction) >= 0.01;
        set(value);
        if (!redraw) return;
        if (view == "focus" && focusScreen == "pre") BuildFocus();
        ResourcesChanged();
    });

    private static async Task<ModelProgress> Prepared(CancellationTokenSource controller, Action<ModelProgress> set, Func<bool> current,
        Func<CancellationToken, Task> work, string name)
    {
        try
        {
            await work(controller.Token);
            return new("local-model-ready", 1);
        }
        catch (OperationCanceledException) { return new(controller.IsCancellationRequested ? "model-paused" : "model-download-error"); }
        catch (InferenceException error) { return new(error.Message); }
        catch (HttpRequestException) { return new("model-download-error"); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return new("model-storage-error"); }
        catch (Exception error)
        {
            StartupDiagnostics.Write($"{name} model initialization: {error.GetType().Name} (0x{error.HResult:X8})");
            return new("local-engine-error");
        }
    }

    private void LoadWindows()
    {
        focusChoices = WindowCapture.List(WindowNative.GetWindowHandle(this));
        focusListed = true;
        if (focusWindow is { Handle: var handle } && handle != IntPtr.Zero && focusChoices.All(choice => choice.Handle != handle)) focusWindow = null;
        _ = LoadThumbnails();
    }

    private void UpdateFocusPill()
    {
        if (focusState == "idle")
        {
            FocusPill.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            return;
        }
        FocusPill.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
        FocusPillDot.Fill = Palette(focusState == "paused" ? "NomiInk3" : "NomiAccent");
        var label = focusState == "paused" ? T("focusPaused") : T("focusLive");
        var elapsed = Clock((DateTimeOffset.Now - focusStarted).TotalSeconds);
        FocusPillText.Text = $"{label} · {elapsed}";
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(FocusPill, $"{label}, {call?.Window}");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(FocusPill, "FocusPill");
        if (callClock is not null) callClock.Text = elapsed;
        UpdateMini();
    }

    private void FocusPillClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs args)
    {
        if (callMini is not null) ExpandCall();
        else Show("focus");
    }

    private string IssueText(TableIssue issue)
    {
        var culture = CultureInfo.GetCultureInfo(Current.Locale);
        return string.Format(culture, T("issueLine"), issue.Row, issue.Column,
            TableCheck.Format(issue.Expected, culture), TableCheck.Format(issue.Shown, culture));
    }
}
