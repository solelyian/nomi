using System.Text;
using LLama;
using LLama.Common;
using LLama.Sampling;

namespace Nomi;

public sealed class LocalInferenceClient : NomiInferenceClient, IAsyncDisposable
{
    private const int ContextTokens = 16384;
    private const int AnswerTokens = 768;
    private const int ReasoningTokens = 3072;
    private const string ThinkOpen = "<think>\n";
    private const string ThinkClose = "</think>";
    private readonly SemaphoreSlim gate = new(1);
    private readonly ModelStore store;
    private LLamaWeights? weights;
    private bool disposed;
    public string Model => store.Definition.Name;
    public bool HasModel => store.Exists;
    public bool Ready => weights is not null && !disposed;

    public LocalInferenceClient(ModelStore? modelStore = null) => store = modelStore ?? new ModelStore();

    private ModelParams Parameters() => new(store.ModelPath)
    {
        ContextSize = ContextTokens,
        GpuLayerCount = 0,
        Threads = Math.Clamp(Environment.ProcessorCount - 1, 1, 8),
        BatchThreads = Math.Clamp(Environment.ProcessorCount - 1, 1, 8),
        BatchSize = 512,
        UseMemorymap = true
    };

    public async Task PrepareAsync(bool allowDownload, IProgress<ModelProgress>? progress, CancellationToken token)
    {
        if (!await gate.WaitAsync(0, token)) throw new InferenceException("busy");
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (weights is not null) return;
            await store.EnsureAsync(allowDownload, progress, token).ConfigureAwait(false);
            progress?.Report(new("model-loading"));
            weights = await LLamaWeights.LoadFromFileAsync(Parameters(), token).ConfigureAwait(false);
            progress?.Report(new("local-model-ready", 1));
        }
        finally { gate.Release(); }
    }

    public override async Task<bool> GenerateAsync(
        string prompt, string action, string language, string format,
        Action<string> onToken, CancellationToken cancellationToken, string instruction = "",
        IReadOnlyList<AttachedDocument>? documents = null, bool reasoning = false,
        Action<string>? onReasoning = null)
    {
        var (system, content) = BuildRequest(prompt, action, language, format, instruction, documents);
        if (!await gate.WaitAsync(0, cancellationToken)) throw new InferenceException("busy");
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (weights is null) throw new InferenceException("local-model-missing");
            return await Task.Run(async () =>
            {
                var template = new LLamaTemplate(weights) { AddAssistant = true };
                template.Add("system", system);
                template.Add("user", content.Replace("<|", "< |", StringComparison.Ordinal));
                var formatted = Encoding.UTF8.GetString(template.Apply());
                if (formatted.EndsWith(ThinkOpen, StringComparison.Ordinal)) formatted = formatted[..^ThinkOpen.Length];
                formatted += reasoning ? ThinkOpen : $"{ThinkOpen}\n{ThinkClose}\n\n";
                var budget = reasoning ? AnswerTokens + ReasoningTokens : AnswerTokens;
                if (weights.Tokenize(formatted, true, true, Encoding.UTF8).Length + budget >= ContextTokens)
                    throw new InferenceException("document-context-limit");
                var executor = new StatelessExecutor(weights, Parameters());
                using var sampling = new DefaultSamplingPipeline { Temperature = reasoning ? 0.6f : 0.2f };
                var parameters = new InferenceParams { MaxTokens = budget, SamplingPipeline = sampling };
                var thinking = reasoning;
                var pending = new StringBuilder();
                var answerTokens = 0;
                var truncated = false;
                await foreach (var chunk in executor.InferAsync(formatted, parameters, cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (thinking)
                    {
                        pending.Append(chunk);
                        var window = Math.Min(pending.Length, chunk.Length + ThinkClose.Length);
                        var tail = pending.ToString(pending.Length - window, window);
                        var close = tail.IndexOf(ThinkClose, StringComparison.Ordinal);
                        if (close < 0)
                        {
                            onReasoning?.Invoke(chunk);
                            continue;
                        }
                        thinking = false;
                        var answer = tail[(close + ThinkClose.Length)..].TrimStart('\n', ' ');
                        if (answer.Length > 0) onToken(answer);
                        continue;
                    }
                    answerTokens++;
                    onToken(chunk);
                    if (answerTokens >= AnswerTokens) { truncated = true; break; }
                }
                cancellationToken.ThrowIfCancellationRequested();
                return truncated || thinking;
            }, cancellationToken).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (disposed) return;
            disposed = true;
            weights?.Dispose();
            weights = null;
            store.Dispose();
        }
        finally { gate.Release(); }
    }
}
