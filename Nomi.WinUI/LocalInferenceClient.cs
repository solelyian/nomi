using System.Text;
using LLama;
using LLama.Native;
using LLama.Common;
using LLama.Sampling;

namespace Nomi;

public sealed class LocalInferenceClient : NomiInferenceClient, ILocalEngine
{
    static LocalInferenceClient() => NativeLibraries.Ensure();

    private const int ContextTokens = 16384;
    private const int AnswerTokens = 768;
    private const int ReasoningTokens = 3072;
    private const string ThinkCutoff = "Considering the limited time by the user, I have to give the solution based on the thinking directly now.";
    private const string ThinkOpen = "<think>\n";
    private const string ThinkClose = "</think>";
    private readonly SemaphoreSlim gate = new(1);
    private const string GemmaEnd = "<turn|>";
    private readonly ModelStore store;
    private readonly bool gemma;
    private LLamaWeights? weights;
    private bool disposed;
    public string Model => store.Definition.Name;
    public bool HasModel => store.Exists;
    public string Accelerator => "CPU · llama.cpp";
    public long DownloadBytes => store.Definition.Bytes;
    public bool Ready => weights is not null && !disposed;

    private static readonly TimeSpan IdleRelease = TimeSpan.FromMinutes(5);
    private LLamaContext? context;
    private readonly List<LLamaToken> cached = [];
    private readonly LLamaBatch batch = new();
    private Timer? idle;

    public LocalInferenceClient(ModelStore? modelStore = null, bool gemma = false)
    {
        store = modelStore ?? new ModelStore();
        this.gemma = gemma;
    }

    private ModelParams Parameters() => new(store.ModelPath)
    {
        ContextSize = ContextTokens,
        GpuLayerCount = 0,
        Threads = Math.Clamp(Environment.ProcessorCount - 1, 1, 8),
        BatchThreads = Math.Clamp(Environment.ProcessorCount - 1, 1, 8),
        BatchSize = 512,
        LoadMode = LLama.Native.LLamaLoadMode.MemoryMap
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
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (weights is null) throw new InferenceException("local-model-missing");
            return await Task.Run(async () =>
            {
                reasoning &= !gemma;
                var formatted = gemma ? VisionClient.GemmaText(system, content) : QwenPrompt(weights, system, content, reasoning);
                var thinkBudget = reasoning ? ReasoningTokens : 0;
                if (weights.Tokenize(formatted, true, true, Encoding.UTF8).Length + thinkBudget + AnswerTokens >= ContextTokens)
                    throw new InferenceException("document-context-limit");
                var thought = new StringBuilder();
                var thinking = reasoning;
                var thinkTokens = 0;
                var answerTokens = 0;
                var truncated = false;
                var precise = action is "verify" or "convert";
                await foreach (var chunk in Stream(formatted, thinkBudget + AnswerTokens, gemma, precise, cancellationToken))
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
                    await foreach (var chunk in Stream(resumed, AnswerTokens, false, precise, cancellationToken))
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
        catch (Exception error) when (error is not (OperationCanceledException or InferenceException or CalcException or ObjectDisposedException))
        {
            throw new InferenceException("local-engine-error", error);
        }
        finally { Idle(); gate.Release(); }
    }

    private static string QwenPrompt(LLamaWeights weights, string system, string content, bool reasoning)
    {
        var template = new LLamaTemplate(weights) { AddAssistant = true };
        template.Add("system", system);
        template.Add("user", VisionClient.Plain(content));
        var formatted = Encoding.UTF8.GetString(template.Apply());
        if (!formatted.EndsWith(ThinkOpen, StringComparison.Ordinal)) formatted += ThinkOpen;
        return reasoning ? formatted : formatted + $"\n{ThinkClose}\n\n";
    }

    // One context is kept between answers, so the part of a prompt already read (instructions, tasks,
    // earlier turns, the thought before a cut-off) is not read again. It is released after a few idle minutes.
    private async IAsyncEnumerable<string> Stream(string prompt, int maxTokens, bool gemma, bool precise,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        context ??= weights!.CreateContext(Parameters());
        var tokens = context.Tokenize(prompt, special: true);
        var common = 0;
        while (common < cached.Count && common < tokens.Length - 1 && cached[common] == tokens[common]) common++;
        context.NativeHandle.MemorySequenceRemove(LLamaSeqId.Zero, common, -1);
        cached.RemoveRange(common, cached.Count - common);
        var fresh = tokens[common..].ToList();
        var (result, _, past) = await context.DecodeAsync(fresh, LLamaSeqId.Zero, batch, common).ConfigureAwait(false);
        if (result != DecodeResult.Ok) { Reset(); throw new InferenceException("local-engine-error"); }
        cached.AddRange(fresh);
        PrefixTokens = common;
        using var sampling = gemma
            ? new DefaultSamplingPipeline { Temperature = precise ? 0.2f : 1.0f, TopP = 0.95f, TopK = 64 }
            : new DefaultSamplingPipeline { Temperature = precise ? 0.2f : 0.6f, TopP = 0.95f, TopK = 20 };
        var decoder = new StreamingTokenDecoder(context);
        for (var i = 0; i < maxTokens; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var id = sampling.Sample(context.NativeHandle, batch.TokenCount - 1);
            if (id.IsEndOfGeneration(weights!.Vocab)) break;
            decoder.Add(id);
            var text = decoder.Read();
            var stop = gemma ? text.IndexOf(GemmaEnd, StringComparison.Ordinal) : -1;
            if (stop >= 0)
            {
                if (stop > 0) yield return text[..stop];
                break;
            }
            if (text.Length > 0) yield return text;
            batch.Clear();
            batch.Add(id, past++, LLamaSeqId.Zero, true);
            if (await context.DecodeAsync(batch, cancellationToken).ConfigureAwait(false) != DecodeResult.Ok) { Reset(); throw new InferenceException("local-engine-error"); }
            cached.Add(id);
        }
    }

    // Tokens of the last prompt that were already in the context, for the routing log and checks.
    public int PrefixTokens { get; private set; }

    private void Reset()
    {
        cached.Clear();
        context?.Dispose();
        context = null;
    }

    private void Idle()
    {
        if (disposed) return;
        idle ??= new Timer(_ =>
        {
            if (!gate.Wait(0)) return;
            try { if (!disposed) Reset(); }
            finally { gate.Release(); }
        });
        idle.Change(IdleRelease, Timeout.InfiniteTimeSpan);
    }

    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (disposed) return;
            disposed = true;
            idle?.Dispose();
            Reset();
            weights?.Dispose();
            weights = null;
            store.Dispose();
        }
        finally { gate.Release(); }
    }
}
