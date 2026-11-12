using System.Diagnostics;
using Nomi;

// Runs Whisper small on a recorded sentence: --speech-live <wav 16 kHz mono 16-bit> with NOMI_TEST_MODEL_DIR.
internal static class SpeechLiveChecks
{
    internal static async Task RunAsync(string wav)
    {
        var directory = Environment.GetEnvironmentVariable("NOMI_TEST_MODEL_DIR");
        await using var speech = new SpeechModel(new ModelStore(directory, ModelDefinition.Load("speech-model.json")));
        await speech.PrepareAsync(true, null, default);
        var bytes = await File.ReadAllBytesAsync(wav);
        var samples = SpeechText.Samples(bytes.AsSpan(44));
        var segmenter = new SpeechSegmenter();
        var events = segmenter.Feed(new float[8000]).Concat(segmenter.Feed(samples)).Concat(segmenter.Feed(new float[16000])).ToArray();
        var utterance = events.FirstOrDefault(item => item.Samples is not null)?.Samples
            ?? throw new InvalidOperationException("No utterance found in the recording");
        var clock = Stopwatch.StartNew();
        var text = await speech.TranscribeAsync(utterance, "en", default);
        Console.WriteLine($"whisper ({clock.Elapsed.TotalSeconds:0.0}s, {utterance.Length / 16000.0:0.0}s of speech): {text}");
        if (!text.Contains("total", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Whisper did not understand the sentence");
        Console.WriteLine("Speech live check passed: Whisper verified, utterance found, sentence transcribed.");
    }
}
