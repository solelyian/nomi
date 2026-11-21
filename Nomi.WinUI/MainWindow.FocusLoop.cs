using System.Globalization;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media.Imaging;
using WinRT.Interop;

namespace Nomi;

// The call itself: one image per second, an analysis when the screen settles on something new, and answers to questions.
public sealed partial class MainWindow
{
    private CancellationTokenSource? focusLoop;
    private CancellationTokenSource? watchRun;
    private CancellationTokenSource? askRun;
    private Task? lookTask;
    private ScreenFrame? callFrame;
    private DateTimeOffset callFrameAt;
    private WriteableBitmap? callPreview;
    private Microphone? callMicrophone;
    private Voice? callSpeaker;

    private static ScreenFrame Copy(ScreenFrame frame) => new(frame.Width, frame.Height, (byte[])frame.Pixels.Clone());

    private IReadOnlyList<string> OpenTitles() =>
        board.Tasks.Where(item => item.Column is Columns.Todo or Columns.Doing).OrderBy(item => item.Order).Take(9).Select(item => item.Title).ToArray();

    private async Task StartFocus()
    {
        if (focusWindow is not { } target || focusState != "idle") return;
        if (target.Handle != IntPtr.Zero && !WindowCapture.Exists(target.Handle))
        {
            focusMessage = Label("focusClosed");
            focusWindow = null;
            BuildFocus();
            return;
        }
        focusMessage = "";
        insight = null;
        tableReport = null;
        endedCall = null;
        call = new FocusCall(WindowName(target), Now, callMode);
        focusState = "watching";
        focusScreen = "call";
        focusStarted = call.Started;
        callActivity = "preparing";
        ClearThumbnails();
        if (target.Handle == IntPtr.Zero) WindowCapture.Exclude(WindowNative.GetWindowHandle(this), true);
        if (focusTaskId is not null && board.Find(focusTaskId) is { RunningSince: null } linked) board.Start(linked, Now);
        Refresh();
        if (!vision.Ready)
        {
            await PrepareVision(false);
            if (call is null) return;
            if (!vision.Ready)
            {
                var reason = Label(visionProgress.Stage);
                StopFocus();
                focusScreen = "pre";
                focusMessage = reason;
                Refresh();
                return;
            }
        }
        callSpeaker = new Voice();
        callSpeaker.SpeakingChanged += speaking => DispatcherQueue.TryEnqueue(() =>
        {
            if (callMicrophone is not null) callMicrophone.Damped = speaking;
            if (speaking) CallActivity("speaking");
            else if (callActivity == "speaking") CallActivity("listening");
        });
        if (callMic) await StartMicrophone();
        if (call is null) return;
        focusLoop = new CancellationTokenSource();
        _ = RunFocus(focusLoop.Token);
        CallActivity("listening");
        CallRemark(string.Format(CultureInfo.CurrentCulture, T("callHello"), call.Window), null, null, false);
    }

    private async Task RunFocus(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            do
            {
                if (call is null) break;
                if (focusState != "watching" || focusWindow is not { } target) continue;
                ScreenFrame frame;
                try { frame = await Task.Run(() => CaptureChoice(target), token); }
                catch (OperationCanceledException) { break; }
                catch (InferenceException error)
                {
                    var message = Label(error.Message);
                    if (focusMessage != message) { focusMessage = message; CallRefresh(); }
                    continue;
                }
                catch (Exception error) when (error is not OutOfMemoryException)
                {
                    StartupDiagnostics.Write($"Focus capture: {error.GetType().Name} (0x{error.HResult:X8})");
                    focusMessage = Label("focusCaptureError");
                    CallRefresh();
                    continue;
                }
                if (call is null || focusState != "watching") { Array.Clear(frame.Pixels); continue; }
                if (focusMessage.Length > 0) { focusMessage = ""; CallRefresh(); }
                var at = DateTimeOffset.Now;
                var (print, small) = await Task.Run(() => (frame.Thumbprint(), frame.Fit(PreviewSide)), token);
                var old = callFrame;
                callFrame = frame;
                callFrameAt = at;
                if (old is not null) Array.Clear(old.Pixels);
                ShowPreview(small);
                if (small != frame) Array.Clear(small.Pixels);
                if (askRun is null && lookTask is not { IsCompleted: false } && call.ShouldWatch(print, at))
                    lookTask = Look(Copy(frame), print, at, token);
            }
            while (await timer.WaitForNextTickAsync(token));
        }
        catch (OperationCanceledException) { }
    }

    private void ShowPreview(ScreenFrame small)
    {
        if (callPreview is null || callPreview.PixelWidth != small.Width || callPreview.PixelHeight != small.Height)
        {
            callPreview = new WriteableBitmap(small.Width, small.Height);
            if (previewImage is not null) previewImage.Source = callPreview;
        }
        using (var stream = callPreview.PixelBuffer.AsStream()) stream.Write(small.Pixels, 0, small.Pixels.Length);
        callPreview.Invalidate();
        if (previewImage is not null && previewImage.Source != callPreview) previewImage.Source = callPreview;
        if (previewWaiting is not null) previewWaiting.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
    }

    private async Task Look(ScreenFrame frame, byte[] print, DateTimeOffset seen, CancellationToken token)
    {
        using var run = CancellationTokenSource.CreateLinkedTokenSource(token);
        watchRun = run;
        CallActivity("looking");
        try
        {
            var result = await vision.AnalyzeAsync(frame, language, OpenTitles(), run.Token);
            if (call is null) return;
            insight = result;
            tableReport = result.Rows.Count >= 2 ? TableCheck.Check(result.Rows, language) : null;
            var outcome = call.Watched(print, seen, result, tableReport);
            if (call.Speaks(outcome))
            {
                var spoken = new List<string>();
                foreach (var point in outcome.Found) spoken.Add(CallRemark(string.Format(T("callGap"), IssueText(point.Issue)), seen, point, true, speak: false));
                foreach (var point in outcome.Fixed) spoken.Add(CallRemark(string.Format(T("callFixedLine"), point.Issue.Row, point.Issue.Column), seen, point, true, speak: false));
                if (outcome.Found.Count == 0 && outcome.Fixed.Count == 0 && result.Task.Length > 0)
                    spoken.Add(CallRemark(result.Next.Length > 0 ? string.Format(T("callSeesNext"), result.Task, result.Next) : string.Format(T("callSees"), result.Task), seen, null, false, speak: false));
                if (spoken.Count > 0) CallSpeak(string.Join(" ", spoken), false);
            }
            CallRefresh();
            BuildCompanion();
        }
        catch (OperationCanceledException) { }
        catch (InferenceException error) { focusMessage = Label(error.Message); CallRefresh(); }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            StartupDiagnostics.Write($"Focus analysis: {error.GetType().Name} (0x{error.HResult:X8})");
        }
        finally
        {
            Array.Clear(frame.Pixels);
            if (watchRun == run) watchRun = null;
            if (callActivity == "looking") CallActivity("listening");
        }
    }

    private async Task Ask(string question, string source)
    {
        question = question.Trim();
        if (call is null || question.Length == 0) return;
        callSpeaker?.Stop();
        var recent = call.Lines.Where(line => !line.Live && line.Text.Length > 0).TakeLast(6)
            .Select(line => $"{(line.Who == "you" ? T("callYou") : "Nomi")} : {line.Text}").ToArray();
        CallAdd(call.Asked(question, source, Now));
        if (callFrame is null || focusState != "watching")
        {
            CallRemark(T(focusState == "paused" ? "callPausedAnswer" : "callNoImage"), null, null, false, answer: true);
            return;
        }
        askRun?.Cancel();
        watchRun?.Cancel();
        if (lookTask is { IsCompleted: false } pending)
        {
            try { await pending; }
            catch (OperationCanceledException) { }
        }
        if (call is null || callFrame is null) return;
        using var run = CancellationTokenSource.CreateLinkedTokenSource(focusLoop?.Token ?? CancellationToken.None);
        askRun = run;
        var frame = Copy(callFrame);
        var seen = callFrameAt;
        var line = call.Add(new CallLine("nomi", "", Now, seen, "answer") { Live = true });
        CallAdd(line);
        CallActivity("thinking");
        try
        {
            var answer = await vision.AskAsync(frame, question, language, recent,
                text => DispatcherQueue.TryEnqueue(() => { if (line.Live) { line.Text = text; CallUpdateLine(line); } }), run.Token);
            line.Text = answer.Length > 0 ? answer : T("callNoAnswer");
            line.Live = false;
            call?.Seen(seen);
            CallUpdateLine(line);
            CallRefresh();
            CallSpeak(line.Text, true);
        }
        catch (OperationCanceledException)
        {
            line.Live = false;
            if (line.Text.Length == 0) line.Text = T("callInterrupted");
            CallUpdateLine(line);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            if (error is not InferenceException) StartupDiagnostics.Write($"Focus question: {error.GetType().Name} (0x{error.HResult:X8})");
            line.Live = false;
            line.Text = error is InferenceException known ? Label(known.Message) : T("callNoAnswer");
            CallUpdateLine(line);
        }
        finally
        {
            Array.Clear(frame.Pixels);
            if (askRun == run) askRun = null;
            if (callActivity == "thinking") CallActivity("listening");
        }
    }

    private string CallRemark(string text, DateTimeOffset? seen, CallPoint? point, bool verified, bool answer = false, bool speak = true)
    {
        if (call is null) return text;
        CallAdd(call.Add(new CallLine("nomi", text, Now, seen, answer ? "answer" : "self", verified) { Point = point }));
        if (speak) CallSpeak(text, answer);
        return text;
    }

    // Nomi's own remarks stay silent in calm mode; answers to the user are always spoken when the voice is on.
    private void CallSpeak(string text, bool answer)
    {
        if (!callVoice || callSpeaker is not { } speaker || (!answer && profile.Calm)) return;
        _ = Speak();
        async Task Speak()
        {
            try { await speaker.SpeakAsync(text, language); }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                StartupDiagnostics.Write($"Focus voice: {error.GetType().Name} (0x{error.HResult:X8})");
            }
        }
    }

    private async Task StartMicrophone()
    {
        if (!speech.HasModel)
        {
            callMic = false;
            focusMessage = T("callNeedSpeech");
            CallRefresh();
            return;
        }
        if (!speech.Ready) await PrepareSpeech(false);
        if (!speech.Ready || call is null)
        {
            callMic = false;
            return;
        }
        if (callMicrophone is null)
        {
            callMicrophone = new Microphone();
            callMicrophone.SpeechStarted += () => DispatcherQueue.TryEnqueue(() =>
            {
                if (call is null || !callMic) return;
                callSpeaker?.Stop();
                CallActivity("hearing");
            });
            callMicrophone.Utterance += samples => DispatcherQueue.TryEnqueue(async () => await Transcribe(samples));
            callMicrophone.Failed += _ => DispatcherQueue.TryEnqueue(() =>
            {
                callMic = false;
                callMicrophone?.Stop();
                focusMessage = T("micError");
                if (view == "focus") BuildFocus();
            });
        }
        callMic = callMicrophone.Start();
        if (!callMic) focusMessage = T("micUnavailable");
    }

    private async Task Transcribe(float[] samples)
    {
        try
        {
            if (call is null || !callMic) return;
            CallActivity("transcribing");
            var text = await speech.TranscribeAsync(samples, language, focusLoop?.Token ?? CancellationToken.None);
            if (text.Length > 0) await Ask(text, "voice");
            else if (callActivity == "transcribing") CallActivity("listening");
        }
        catch (OperationCanceledException) { }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            if (error is not InferenceException) StartupDiagnostics.Write($"Focus speech: {error.GetType().Name} (0x{error.HResult:X8})");
            if (callActivity == "transcribing") CallActivity("listening");
        }
        finally { Array.Clear(samples); }
    }

    private async Task ToggleMic()
    {
        if (call is null) return;
        if (callMic)
        {
            callMic = false;
            callMicrophone?.Stop();
        }
        else
        {
            callMic = true;
            focusMessage = "";
            await StartMicrophone();
        }
        if (view == "focus") BuildFocus();
        UpdateMini();
    }

    private void ToggleVoice()
    {
        callVoice = !callVoice;
        if (!callVoice) callSpeaker?.Stop();
        if (view == "focus") BuildFocus();
    }

    private void PauseFocus(bool pause)
    {
        if (call is null) return;
        focusState = pause ? "paused" : "watching";
        call.Paused = pause;
        if (pause)
        {
            watchRun?.Cancel();
            if (callFrame is not null) Array.Clear(callFrame.Pixels);
            callFrame = null;
            callPreview = null;
        }
        else
        {
            focusMessage = "";
            call.Forget();
        }
        callActivity = pause ? "paused" : "listening";
        Refresh();
    }

    private void ChangeWindow(WindowChoice choice)
    {
        if (call is null) return;
        focusWindow = choice;
        call.Window = WindowName(choice);
        call.Forget();
        WindowCapture.Exclude(WindowNative.GetWindowHandle(this), choice.Handle == IntPtr.Zero);
        if (callFrame is not null) Array.Clear(callFrame.Pixels);
        callFrame = null;
        callPreview = null;
        focusMessage = "";
        Refresh();
    }

    private void EndCall()
    {
        if (call is not { } ended) return;
        ended.End(Now);
        var lasted = ended.Duration(Now);
        var mini = callMini is not null;
        StopFocus();
        endedCall = ended;
        focusScreen = "end";
        if (mini) ExpandCall();
        if (lasted >= TimeSpan.FromMinutes(10)) CheckRoutines(Triggers.FocusDone);
        Show("focus");
    }

    private void StopFocus()
    {
        focusLoop?.Cancel();
        focusLoop?.Dispose();
        focusLoop = null;
        askRun?.Cancel();
        watchRun?.Cancel();
        callMicrophone?.Dispose();
        callMicrophone = null;
        callSpeaker?.Dispose();
        callSpeaker = null;
        CloseMini();
        WindowCapture.Exclude(WindowNative.GetWindowHandle(this), false);
        if (callFrame is not null) Array.Clear(callFrame.Pixels);
        callFrame = null;
        callPreview = null;
        call = null;
        focusState = "idle";
        insight = null;
        tableReport = null;
        callActivity = "listening";
        _ = vision.UnloadAsync();
        if (!transcriptRunning) _ = speech.UnloadAsync();
    }
}
