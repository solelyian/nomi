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
    private const int QuickThinkTokens = 512;
    private const string ThinkCutoff = "Considering the limited time by the user, I have to give the solution based on the thinking directly now.";
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
                if (!formatted.EndsWith(ThinkOpen, StringComparison.Ordinal)) formatted += ThinkOpen;
                var thinkBudget = reasoning ? ReasoningTokens : QuickThinkTokens;
                if (weights.Tokenize(formatted, true, true, Encoding.UTF8).Length + thinkBudget + AnswerTokens >= ContextTokens)
                    throw new InferenceException("document-context-limit");
                var executor = new StatelessExecutor(weights, Parameters());
                var thought = new StringBuilder();
                var thinking = true;
                var thinkTokens = 0;
                var answerTokens = 0;
                var truncated = false;
                await foreach (var chunk in Stream(executor, formatted, thinkBudget + AnswerTokens, cancellationToken))
                {
                    if (thinking)
                    {
                        thought.Append(chunk);
                        var window = Math.Min(thought.Length, chunk.Length + ThinkClose.Length);
                        var tail = thought.ToString(thought.Length - window, window);
                        var close = tail.IndexOf(ThinkClose, StringComparison.Ordinal);
                        if (close < 0)
                        {
                            onReasoning?.Invoke(chunk);
                            if (++thinkTokens >= thinkBudget) break;
                            continue;
                        }
                        thinking = false;
                        var answer = tail[(close + ThinkClose.Length)..].TrimStart('\n', ' ');
                        if (answer.Length > 0) { answerTokens++; onToken(answer); }
                        continue;
                    }
                    answerTokens++;
                    onToken(chunk);
                    if (answerTokens >= AnswerTokens) { truncated = true; break; }
                }
                if (thinking)
                {
                    var resumed = $"{formatted}{thought}\n\n{ThinkCutoff}\n{ThinkClose}\n\n";
                    await foreach (var chunk in Stream(executor, resumed, AnswerTokens, cancellationToken))
                    {
                        answerTokens++;
                        onToken(chunk);
                        if (answerTokens >= AnswerTokens) { truncated = true; break; }
                    }
                }
                cancellationToken.ThrowIfCancellationRequested();
                return truncated;
            }, cancellationToken).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    private static async IAsyncEnumerable<string> Stream(StatelessExecutor executor, string prompt, int maxTokens,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var sampling = new DefaultSamplingPipeline { Temperature = 0.6f, TopP = 0.95f, TopK = 20 };
        var parameters = new InferenceParams { MaxTokens = maxTokens, SamplingPipeline = sampling };
        await foreach (var chunk in executor.InferAsync(prompt, parameters, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return chunk;
        }
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
