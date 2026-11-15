using NAudio.Wave;

namespace Nomi;

// Reads the default microphone at 16 kHz mono and hands finished utterances to the call.
public sealed class Microphone : IDisposable
{
    private readonly SpeechSegmenter segmenter = new();
    private readonly object sync = new();
    private WaveInEvent? wave;

    public event Action? SpeechStarted;
    public event Action<float[]>? Utterance;
    public event Action<string>? Failed;

    public bool Listening => wave is not null;

    public bool Damped
    {
        get => segmenter.Damped;
        set { lock (sync) segmenter.Damped = value; }
    }

    public static bool Available
    {
        get
        {
            try { return WaveInEvent.DeviceCount > 0; }
            catch (Exception error) when (error is DllNotFoundException or InvalidOperationException) { return false; }
        }
    }

    public bool Start()
    {
        if (wave is not null) return true;
        if (!Available) return false;
        var input = new WaveInEvent { WaveFormat = new WaveFormat(SpeechSegmenter.Rate, 16, 1), BufferMilliseconds = 60 };
        input.DataAvailable += (_, args) =>
        {
            SpeechEvent[] events;
            lock (sync) events = [.. segmenter.Feed(SpeechText.Samples(args.Buffer.AsSpan(0, args.BytesRecorded)))];
            foreach (var item in events)
            {
                if (item.Started) SpeechStarted?.Invoke();
                else if (item.Samples is { } samples) Utterance?.Invoke(samples);
            }
        };
        input.RecordingStopped += (_, args) => { if (args.Exception is not null) Failed?.Invoke("micError"); };
        try
        {
            input.StartRecording();
        }
        catch (NAudio.MmException)
        {
            input.Dispose();
            return false;
        }
        wave = input;
        return true;
    }

    public void Stop()
    {
        var input = wave;
        wave = null;
        if (input is null) return;
        try { input.StopRecording(); }
        catch (NAudio.MmException) { }
        input.Dispose();
        lock (sync) segmenter.Reset();
    }

    public void Dispose() => Stop();
}
