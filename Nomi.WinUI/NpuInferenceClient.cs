using System.Text;
using System.Text.Json;
using Betalgo.Ranul.OpenAI.ObjectModels.RequestModels;
using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.Logging.Abstractions;

namespace Nomi;

public sealed class NpuInferenceClient : NomiInferenceClient, ILocalEngine
{
    private const string Alias = "deepseek-r1-7b";
    private const int AnswerTokens = 768;
    private const int ReasoningTokens = 3072;
    private const int QuickThinkTokens = 512;
    private const int ResumeThinkTokens = 256;
    private const string ThinkOpen = "<think>";
    private const string ThinkClose = "</think>";
    private readonly SemaphoreSlim gate = new(1);
    private readonly string directory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nyne", "Nomi", "npu");
    private string MarkerPath => Path.Combine(directory, "npu-model.json");
    private IModel? model;
    private OpenAIChatClient? chat;
    private AcceleratorVariant? variant;
    private AcceleratorVariant? candidate;
    private bool disposed;

    public string Model => "DeepSeek R1 Distill 7B";
    public string Accelerator => variant is null ? "" : $"{AcceleratorSelection.Label(variant.Provider)} · ONNX Runtime";
    public bool HasModel => File.Exists(MarkerPath);
    public bool Ready => chat is not null && !disposed;
    public long DownloadBytes => candidate?.Bytes ?? 0;

    public async Task PrepareAsync(bool allowDownload, IProgress<ModelProgress>? progress, CancellationToken token)
    {
        if (!await gate.WaitAsync(0, token)) throw new InferenceException("busy");
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (chat is not null) return;
            if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 26100))
                throw new AcceleratorUnavailableException("windows-version");
            progress?.Report(new("npu-detecting"));
            var manager = await ManagerAsync(token).ConfigureAwait(false);
            var providers = Discover(manager).Where(ep => AcceleratorSelection.IsNpuProvider(ep.Name)).ToArray();
            if (providers.Length == 0) throw new AcceleratorUnavailableException("no-npu");
            if (providers.Any(ep => !ep.IsRegistered))
            {
                if (!allowDownload && !HasModel) throw new InferenceException("local-model-missing");
                progress?.Report(new("npu-provider-downloading"));
                await Foundry(() => manager.DownloadAndRegisterEpsAsync(providers.Select(ep => ep.Name),
                    (_, percent) => progress?.Report(new("npu-provider-downloading", percent / 100)), token)).ConfigureAwait(false);
            }
            var registered = Discover(manager).Where(ep => ep.IsRegistered).Select(ep => ep.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var catalog = await Foundry(() => manager.GetCatalogAsync(token)).ConfigureAwait(false);
            var entry = await Foundry(() => catalog.GetModelAsync(Alias, token)).ConfigureAwait(false)
                ?? throw new AcceleratorUnavailableException("no-model");
            var variants = entry.Variants.Select(Describe).ToArray();
            var pick = AcceleratorSelection.Pick(variants, registered) ?? throw new AcceleratorUnavailableException("no-npu-variant");
            candidate = pick;
            var chosen = entry.Variants.First(item => item.Id == pick.Id);
            if (!await Foundry(() => chosen.IsCachedAsync(token)).ConfigureAwait(false))
            {
                if (!allowDownload) throw new InferenceException("local-model-missing");
                Directory.CreateDirectory(directory);
                var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(directory))!);
                if (drive.AvailableFreeSpace < pick.Bytes + 500_000_000) throw new InferenceException("model-disk-space");
                progress?.Report(new("model-downloading"));
                try
                {
                    await chosen.DownloadAsync(percent => progress?.Report(new("model-downloading", percent / 100.0)), token)
                        .ConfigureAwait(false);
                }
                catch (FoundryLocalException) when (!token.IsCancellationRequested)
                {
                    throw new InferenceException("model-download-error");
                }
            }
            token.ThrowIfCancellationRequested();
            progress?.Report(new("model-loading"));
            try
            {
                await chosen.LoadAsync(token).ConfigureAwait(false);
                chat = await chosen.GetChatClientAsync(token).ConfigureAwait(false);
            }
            catch (FoundryLocalException error) when (!token.IsCancellationRequested)
            {
                throw new AcceleratorUnavailableException("load-failed", error);
            }
            model = chosen;
            variant = pick;
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(MarkerPath, JsonSerializer.Serialize(pick), CancellationToken.None).ConfigureAwait(false);
            StartupDiagnostics.Write($"NPU engine: {pick.Id} on {pick.Provider}");
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
            if (chat is null || variant is null) throw new InferenceException("local-model-missing");
            var budget = AcceleratorSelection.Budget(variant, system.Length + content.Length,
                reasoning ? ReasoningTokens : QuickThinkTokens, AnswerTokens)
                ?? throw new InferenceException("document-context-limit");
            var thought = new StringBuilder();
            var (thinking, truncated) = await StreamAsync(system, content, budget.Think, budget.Think + budget.Answer,
                budget.Answer, thought, onToken, onReasoning, cancellationToken).ConfigureAwait(false);
            if (thinking)
            {
                var resumed = $"{content}\n\n{(language == "fr" ? "Notes de réflexion" : "Reasoning notes")}:\n{thought}\n\n"
                    + (language == "fr" ? "Donne maintenant la réponse finale directement, sans reprendre la réflexion."
                        : "Now give the final answer directly, without restarting the reasoning.");
                thought.Clear();
                (thinking, truncated) = await StreamAsync(system, resumed, ResumeThinkTokens, ResumeThinkTokens + budget.Answer,
                    budget.Answer, thought, onToken, onReasoning, cancellationToken).ConfigureAwait(false);
                if (thinking) truncated = true;
            }
            cancellationToken.ThrowIfCancellationRequested();
            return truncated;
        }
        catch (FoundryLocalException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InferenceException("local-engine-error");
        }
        finally { gate.Release(); }
    }

    private async Task<(bool Thinking, bool Truncated)> StreamAsync(
        string system, string content, int thinkBudget, int maxTokens, int answerBudget, StringBuilder thought,
        Action<string> onToken, Action<string>? onReasoning, CancellationToken cancellationToken)
    {
        var client = chat!;
        client.Settings.MaxTokens = maxTokens;
        client.Settings.Temperature = 0.6f;
        client.Settings.TopP = 0.95f;
        var messages = new List<ChatMessage>
        {
            new() { Role = "system", Content = system },
            new() { Role = "user", Content = content }
        };
        var thinking = true;
        var thinkTokens = 0;
        var answerTokens = 0;
        await foreach (var response in client.CompleteChatStreamingAsync(messages, cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var chunk = response.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrEmpty(chunk)) continue;
            if (thinking)
            {
                thought.Append(chunk);
                var text = thought.ToString();
                if (text.TrimStart().StartsWith(ThinkOpen, StringComparison.Ordinal))
                {
                    text = text.TrimStart()[ThinkOpen.Length..];
                    thought.Clear().Append(text);
                }
                var close = text.IndexOf(ThinkClose, StringComparison.Ordinal);
                if (close < 0)
                {
                    onReasoning?.Invoke(chunk);
                    if (++thinkTokens >= thinkBudget) return (true, false);
                    continue;
                }
                thinking = false;
                thought.Length = close;
                var answer = text[(close + ThinkClose.Length)..].TrimStart('\n', ' ');
                if (answer.Length > 0) { answerTokens++; onToken(answer); }
                continue;
            }
            onToken(chunk);
            if (++answerTokens >= answerBudget) return (false, true);
        }
        return (thinking, false);
    }

    private static AcceleratorVariant Describe(IModel item) => new(
        item.Id,
        item.Info.Runtime?.DeviceType.ToString() ?? "",
        item.Info.Runtime?.ExecutionProvider ?? "",
        item.Info.ContextLength ?? 0,
        item.Info.MaxOutputTokens ?? 0,
        (item.Info.FileSizeMb ?? 0) * 1_000_000L);

    private static EpInfo[] Discover(FoundryLocalManager manager)
    {
        try { return manager.DiscoverEps(); }
        catch (FoundryLocalException error) { throw new AcceleratorUnavailableException("runtime", error); }
    }

    private static async Task<T> Foundry<T>(Func<Task<T>> work)
    {
        try { return await work().ConfigureAwait(false); }
        catch (FoundryLocalException error) { throw new AcceleratorUnavailableException("runtime", error); }
    }

    private async Task<FoundryLocalManager> ManagerAsync(CancellationToken token)
    {
        if (FoundryLocalManager.IsInitialized) return FoundryLocalManager.Instance;
        Directory.CreateDirectory(directory);
        try
        {
            await FoundryLocalManager.CreateAsync(new Configuration
            {
                AppName = "Nomi",
                AppDataDir = directory,
                LogsDir = StartupDiagnostics.DirectoryPath,
                LogLevel = Microsoft.AI.Foundry.Local.LogLevel.Warning
            }, NullLogger.Instance, token).ConfigureAwait(false);
        }
        catch (Exception error) when (error is FoundryLocalException or DllNotFoundException or BadImageFormatException
            or EntryPointNotFoundException)
        {
            throw new AcceleratorUnavailableException("runtime", error);
        }
        return FoundryLocalManager.Instance;
    }

    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (disposed) return;
            disposed = true;
            chat = null;
            if (model is not null)
            {
                try { await model.UnloadAsync().ConfigureAwait(false); }
                catch (FoundryLocalException) { }
            }
            model = null;
        }
        finally { gate.Release(); }
    }
}
