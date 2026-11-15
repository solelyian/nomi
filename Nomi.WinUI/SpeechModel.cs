using Whisper.net;

namespace Nomi;

// Local speech recognition with Whisper: the voice never leaves the computer.
public sealed class SpeechModel : IAsyncDisposable
{
    private readonly ModelStore store;
    private readonly SemaphoreSlim gate = new(1);
    private WhisperFactory? factory;
    private bool disposed;

    public SpeechModel(ModelStore? modelStore = null) =>
        store = modelStore ?? new ModelStore(definition: ModelDefinition.Load("speech-model.json"));

    public string Model => store.Definition.Name;
    public long Bytes => store.Definition.Bytes;
    public bool HasModel => store.Exists;
    public bool Ready => factory is not null && !disposed;

    private static int Threads => Math.Clamp(Environment.ProcessorCount / 2, 1, 4);

    public Task DownloadAsync(IProgress<ModelProgress>? progress, CancellationToken token) =>
        store.EnsureAsync(true, progress, token);

    public async Task PrepareAsync(bool allowDownload, IProgress<ModelProgress>? progress, CancellationToken token)
    {
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (factory is not null) return;
            await store.EnsureAsync(allowDownload, progress, token).ConfigureAwait(false);
            progress?.Report(new("model-loading"));
            factory = await Task.Run(() => WhisperFactory.FromPath(store.ModelPath), token).ConfigureAwait(false);
            progress?.Report(new("local-model-ready", 1));
        }
        finally { gate.Release(); }
    }

    public async Task<string> TranscribeAsync(float[] samples, string language, CancellationToken token)
    {
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (factory is null) throw new InferenceException("speech-missing");
            await using var processor = factory.CreateBuilder()
                .WithLanguage(language == "fr" ? "fr" : "en")
                .WithThreads(Threads)
                .WithNoContext()
                .WithSingleSegment()
                .Build();
            var text = new System.Text.StringBuilder();
            await foreach (var segment in processor.ProcessAsync(samples, token).ConfigureAwait(false))
                text.Append(segment.Text);
            return SpeechText.Clean(text.ToString());
        }
        finally { gate.Release(); }
    }

    public async Task UnloadAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            factory?.Dispose();
            factory = null;
        }
        finally { gate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        await UnloadAsync().ConfigureAwait(false);
        disposed = true;
        store.Dispose();
    }
}
