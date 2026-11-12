using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;

namespace Nomi;

// Nomi's spoken answers, with the voices installed in Windows: no connection is used.
public sealed class Voice : IDisposable
{
    private readonly SpeechSynthesizer synthesizer = new();
    private readonly MediaPlayer player = new();
    private int turn;

    public event Action<bool>? SpeakingChanged;

    public bool Speaking { get; private set; }

    public Voice()
    {
        player.MediaEnded += (_, _) => Done();
        player.MediaFailed += (_, _) => Done();
    }

    public static bool HasVoice(string language) =>
        SpeechSynthesizer.AllVoices.Any(voice => voice.Language.StartsWith(language, StringComparison.OrdinalIgnoreCase));

    public async Task SpeakAsync(string text, string language)
    {
        Stop();
        var voice = SpeechSynthesizer.AllVoices.FirstOrDefault(item => item.Language.StartsWith(language, StringComparison.OrdinalIgnoreCase));
        if (voice is null || text.Length == 0) return;
        var mine = ++turn;
        synthesizer.Voice = voice;
        var stream = await synthesizer.SynthesizeTextToStreamAsync(text);
        if (mine != turn) { stream.Dispose(); return; }
        player.Source = MediaSource.CreateFromStream(stream, stream.ContentType);
        Speaking = true;
        SpeakingChanged?.Invoke(true);
        player.Play();
    }

    public void Stop()
    {
        turn++;
        if (!Speaking) return;
        player.Pause();
        player.Source = null;
        Done();
    }

    private void Done()
    {
        if (!Speaking) return;
        Speaking = false;
        SpeakingChanged?.Invoke(false);
    }

    public void Dispose()
    {
        Stop();
        player.Dispose();
        synthesizer.Dispose();
    }
}
