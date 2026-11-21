using System.Text.RegularExpressions;

namespace Nomi;

// Turns microphone audio into utterances: speech starts above the noise floor and ends after a short silence.
public sealed class SpeechSegmenter
{
    public const int Rate = 16000;
    private const int Window = Rate * 30 / 1000;
    private const float MinimumLevel = 0.012f;
    private const int StartWindows = 3;
    private const int EndWindows = 24;
    private const int MinimumVoiced = 10;
    private const int MaximumSamples = Rate * 15;
    private const int PreRoll = Rate * 3 / 10;
    private readonly List<float> pending = [];
    private readonly List<float> speech = [];
    private readonly Queue<float> history = new();
    private float floor = 0.004f;
    private int loud;
    private int quiet;
    private int voiced;

    public bool Speaking { get; private set; }

    // While Nomi talks, its own voice from the speakers must not count as the user speaking.
    public bool Damped { get; set; }

    public float Threshold => Math.Max(MinimumLevel, floor * 3) * (Damped ? 3 : 1);

    public IEnumerable<SpeechEvent> Feed(ReadOnlySpan<float> samples)
    {
        var events = new List<SpeechEvent>();
        foreach (var sample in samples) pending.Add(sample);
        var offset = 0;
        while (pending.Count - offset >= Window)
        {
            double sum = 0;
            for (var index = offset; index < offset + Window; index++) sum += pending[index] * pending[index];
            var level = (float)Math.Sqrt(sum / Window);
            var chunk = pending.GetRange(offset, Window);
            offset += Window;
            if (!Speaking)
            {
                foreach (var value in chunk) history.Enqueue(value);
                while (history.Count > PreRoll) history.Dequeue();
                loud = level > Threshold ? loud + 1 : 0;
                if (loud < StartWindows)
                {
                    if (level <= Threshold) floor = Math.Clamp(floor * 0.95f + level * 0.05f, 0.001f, 0.05f);
                    continue;
                }
                Speaking = true;
                speech.Clear();
                speech.AddRange(history);
                history.Clear();
                quiet = 0;
                voiced = loud;
                events.Add(new SpeechEvent(true, null));
                continue;
            }
            speech.AddRange(chunk);
            if (level > Threshold) { voiced++; quiet = 0; }
            else quiet++;
            if (quiet < EndWindows && speech.Count < MaximumSamples) continue;
            Speaking = false;
            loud = 0;
            events.Add(new SpeechEvent(false, voiced >= MinimumVoiced ? [.. speech] : null));
            speech.Clear();
        }
        pending.RemoveRange(0, offset);
        return events;
    }

    public void Reset()
    {
        pending.Clear();
        speech.Clear();
        history.Clear();
        Speaking = false;
        loud = quiet = voiced = 0;
    }

    // Flushes speech still buffered — e.g. when listening stops before the trailing silence.
    public float[]? Finish()
    {
        var tail = Speaking && voiced >= MinimumVoiced ? speech.ToArray() : null;
        Reset();
        return tail;
    }
}

// Started is true when the user begins to speak; Samples holds a finished utterance, or null when it was only noise.
public sealed record SpeechEvent(bool Started, float[]? Samples);

public static partial class SpeechText
{
    private static readonly string[] Noise =
    [
        "sous-titres", "sous-titrage", "amara.org", "merci d'avoir regardé", "merci d’avoir regardé",
        "thanks for watching", "thank you for watching", "subtitles by", "abonnez-vous"
    ];

    [GeneratedRegex(@"\[[^\]]*\]|\([^)]*\)|\*[^*]*\*|♪")]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    // Removes what Whisper writes for silence or music, and the credits it sometimes invents.
    public static string Clean(string text)
    {
        var value = Spaces().Replace(Tags().Replace(text, " "), " ").Trim();
        if (!value.Any(char.IsLetter)) return "";
        var lower = value.ToLowerInvariant();
        return Noise.Any(lower.Contains) ? "" : value;
    }

    public static float[] Samples(ReadOnlySpan<byte> pcm16)
    {
        var output = new float[pcm16.Length / 2];
        for (var index = 0; index < output.Length; index++)
            output[index] = BitConverter.ToInt16(pcm16.Slice(index * 2, 2)) / 32768f;
        return output;
    }
}
