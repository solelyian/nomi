namespace Nomi;

public interface ILocalEngine : IAsyncDisposable
{
    string Model { get; }
    string Accelerator { get; }
    bool HasModel { get; }
    bool Ready { get; }
    long DownloadBytes { get; }
    Task PrepareAsync(bool allowDownload, IProgress<ModelProgress>? progress, CancellationToken token);
    Task<bool> GenerateAsync(
        string prompt, string action, string language, string format,
        Action<string> onToken, CancellationToken cancellationToken, string instruction = "",
        IReadOnlyList<AttachedDocument>? documents = null, bool reasoning = false,
        Action<string>? onReasoning = null);
}

public sealed class AcceleratorUnavailableException(string reason, Exception? inner = null) : Exception(reason, inner);

public sealed record AcceleratorVariant(string Id, string Device, string Provider, long ContextTokens, long OutputTokens, long Bytes);

public sealed record TokenBudget(int Think, int Answer);

public static class AcceleratorSelection
{
    public static readonly string[] NpuProviders = ["QNNExecutionProvider", "OpenVINOExecutionProvider", "VitisAIExecutionProvider"];
    public const int CharactersPerToken = 3;

    public static bool IsNpuProvider(string provider) => NpuProviders.Contains(provider, StringComparer.OrdinalIgnoreCase);

    public static string Vendor(string provider) => provider.ToUpperInvariant() switch
    {
        "QNNEXECUTIONPROVIDER" => "Qualcomm",
        "OPENVINOEXECUTIONPROVIDER" => "Intel",
        "VITISAIEXECUTIONPROVIDER" => "AMD",
        _ => ""
    };

    public static string Label(string provider)
    {
        var vendor = Vendor(provider);
        return vendor.Length == 0 ? "" : $"NPU {vendor}";
    }

    public static AcceleratorVariant? Pick(IEnumerable<AcceleratorVariant> variants, IReadOnlySet<string> registered) =>
        variants
            .Where(variant => string.Equals(variant.Device, "NPU", StringComparison.OrdinalIgnoreCase)
                && IsNpuProvider(variant.Provider) && registered.Contains(variant.Provider))
            .OrderBy(variant => Array.FindIndex(NpuProviders, name => string.Equals(name, variant.Provider, StringComparison.OrdinalIgnoreCase)))
            .ThenByDescending(variant => variant.ContextTokens)
            .FirstOrDefault();

    public static TokenBudget? Budget(AcceleratorVariant variant, int promptCharacters, int thinkTokens, int answerTokens)
    {
        var output = variant.OutputTokens > 0 ? variant.OutputTokens : thinkTokens + answerTokens;
        var room = Math.Min(output, variant.ContextTokens - promptCharacters / CharactersPerToken - 64);
        if (room < answerTokens + 128) return null;
        return new((int)Math.Min(thinkTokens, room - answerTokens), answerTokens);
    }
}

public sealed class AdaptiveInferenceClient : NomiInferenceClient, IAsyncDisposable
{
    private readonly ILocalEngine cpu;
    private readonly ILocalEngine? npu;
    private ILocalEngine? active;
    private bool npuUnavailable;
    private bool npuPending;

    public AdaptiveInferenceClient(ILocalEngine cpu, ILocalEngine? npu)
    {
        this.cpu = cpu;
        this.npu = npu;
    }

    public string? FallbackReason { get; private set; }
    public string AnsweredBy { get; private set; } = "";
    private ILocalEngine Current => active
        ?? (npu is not null && !npuUnavailable && (npuPending || npu.HasModel || !cpu.HasModel) ? npu : cpu);
    public string Model => Current.Model;
    public string Accelerator => active?.Ready == true ? active.Accelerator : "";
    public bool Ready => active?.Ready == true;
    public bool HasModel => !npuPending && (npu?.HasModel == true || cpu.HasModel);
    public long DownloadBytes => Current.DownloadBytes;

    public async Task PrepareAsync(bool allowDownload, IProgress<ModelProgress>? progress, CancellationToken token)
    {
        if (Ready) return;
        if (npu is not null && !npuUnavailable)
        {
            try
            {
                await npu.PrepareAsync(allowDownload, progress, token).ConfigureAwait(false);
                active = npu;
                npuPending = false;
                return;
            }
            catch (AcceleratorUnavailableException error)
            {
                npuUnavailable = true;
                npuPending = false;
                FallbackReason = error.Message;
                await npu.DisposeAsync().ConfigureAwait(false);
            }
            catch (InferenceException error) when (error.Message == "local-model-missing")
            {
                npuPending = true;
                throw;
            }
        }
        await cpu.PrepareAsync(allowDownload, progress, token).ConfigureAwait(false);
        active = cpu;
    }

    public override async Task<bool> GenerateAsync(
        string prompt, string action, string language, string format,
        Action<string> onToken, CancellationToken cancellationToken, string instruction = "",
        IReadOnlyList<AttachedDocument>? documents = null, bool reasoning = false,
        Action<string>? onReasoning = null)
    {
        var engine = active is { Ready: true } ? active : throw new InferenceException("local-model-missing");
        try
        {
            AnsweredBy = engine.Model;
            return await engine.GenerateAsync(prompt, action, language, format, onToken, cancellationToken,
                instruction, documents, reasoning, onReasoning).ConfigureAwait(false);
        }
        catch (InferenceException error) when (error.Message == "document-context-limit" && engine != cpu && cpu.HasModel)
        {
            await cpu.PrepareAsync(false, null, cancellationToken).ConfigureAwait(false);
            AnsweredBy = cpu.Model;
            return await cpu.GenerateAsync(prompt, action, language, format, onToken, cancellationToken,
                instruction, documents, reasoning, onReasoning).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (npu is not null) await npu.DisposeAsync().ConfigureAwait(false);
        await cpu.DisposeAsync().ConfigureAwait(false);
    }
}
