using System.Text;
using System.Text.RegularExpressions;
using LLama;
using LLama.Common;
using LLama.Native;
using LLama.Sampling;

namespace Nomi;

public sealed record ScreenInsight(string Task, string Place, string Check, string Next, int Match, double Seconds,
    IReadOnlyList<string[]> Rows);

public sealed class VisionClient : IAsyncDisposable
{
    private const int ContextTokens = 6144;
    private const int AnswerTokens = 640;
    private const int ImageSide = 1600;
    private const int ImagePixels = 1100 * 32 * 32;
    private const string Marker = "<__media__>";
    private static readonly Regex Field = new(@"^\s*(TASK|WHERE|CHECK|NEXT|MATCH)\s*[:：]\s*(.*)$",
        RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private readonly SemaphoreSlim gate = new(1);
    private readonly ModelStore model;
    private readonly ModelStore projector;
    private LLamaWeights? weights;
    private MtmdWeights? clip;
    private bool disposed;

    public VisionClient(ModelStore? modelStore = null, ModelStore? projectorStore = null)
    {
        model = modelStore ?? new ModelStore(definition: ModelDefinition.Load("vision-model.json"));
        projector = projectorStore ?? new ModelStore(definition: ModelDefinition.Load("vision-projector.json"));
    }

    public string Model => model.Definition.Name;
    public long Bytes => model.Definition.Bytes + projector.Definition.Bytes;
    public bool HasModel => model.Exists && projector.Exists;
    public bool Ready => weights is not null && clip is not null && !disposed;

    private static int Threads => Math.Clamp(Environment.ProcessorCount - 1, 1, 8);

    private ModelParams Parameters() => new(model.ModelPath)
    {
        ContextSize = ContextTokens,
        GpuLayerCount = 0,
        Threads = Threads,
        BatchThreads = Threads,
        BatchSize = 1024,
        UBatchSize = 1024,
        UseMemorymap = true
    };

    public async Task PrepareAsync(bool allowDownload, IProgress<ModelProgress>? progress, CancellationToken token)
    {
        if (!await gate.WaitAsync(0, token)) throw new InferenceException("busy");
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (Ready) return;
            var total = (double)Bytes;
            var first = model.Definition.Bytes / total;
            await model.EnsureAsync(allowDownload, Scaled(progress, 0, first), token).ConfigureAwait(false);
            await projector.EnsureAsync(allowDownload, Scaled(progress, first, 1 - first), token).ConfigureAwait(false);
            progress?.Report(new("model-loading"));
            await Task.Run(async () =>
            {
                weights = await LLamaWeights.LoadFromFileAsync(Parameters(), token).ConfigureAwait(false);
                var parameters = MtmdContextParams.Default();
                parameters.UseGpu = false;
                parameters.NThreads = Threads;
                parameters.MediaMarker = Marker;
                clip = await MtmdWeights.LoadFromFileAsync(projector.ModelPath, weights, parameters, token).ConfigureAwait(false);
                if (!clip.SupportsVision) throw new InferenceException("vision-unsupported");
            }, token).ConfigureAwait(false);
            progress?.Report(new("local-model-ready", 1));
        }
        catch
        {
            Release();
            throw;
        }
        finally { gate.Release(); }
    }

    private static IProgress<ModelProgress>? Scaled(IProgress<ModelProgress>? progress, double offset, double share) =>
        progress is null ? null : new SyncProgress(value => progress.Report(value with { Fraction = offset + value.Fraction * share }));

    public async Task<ScreenInsight> AnalyzeAsync(ScreenFrame frame, string language, IReadOnlyList<string> tasks,
        CancellationToken token)
    {
        var started = DateTime.UtcNow;
        var text = await RunAsync(frame, SystemPrompt(language), UserPrompt(language, tasks), AnswerTokens, token).ConfigureAwait(false);
        return Parse(text, tasks.Count, (DateTime.UtcNow - started).TotalSeconds);
    }

    private async Task<string> RunAsync(ScreenFrame frame, string system, string user, int tokens, CancellationToken token)
    {
        if (!await gate.WaitAsync(0, token)) throw new InferenceException("busy");
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (weights is null || clip is null) throw new InferenceException("vision-missing");
            var image = frame.Fit(ImageSide, ImagePixels).ToBitmap();
            return await Task.Run(async () =>
            {
                using var context = weights.CreateContext(Parameters());
                var executor = new InteractiveExecutor(context, clip);
                var embed = clip.LoadMedia(image);
                Array.Clear(image);
                executor.Embeds.Add(embed);
                var template = new LLamaTemplate(weights) { AddAssistant = true };
                template.Add("system", system);
                template.Add("user", $"{Marker}\n{user}");
                var prompt = Encoding.UTF8.GetString(template.Apply());
                using var sampling = new DefaultSamplingPipeline { Temperature = 0.1f, TopP = 0.8f, TopK = 20 };
                var parameters = new InferenceParams
                {
                    MaxTokens = tokens,
                    SamplingPipeline = sampling,
                    AntiPrompts = ["<|im_end|>"]
                };
                var output = new StringBuilder();
                try
                {
                    await foreach (var chunk in executor.InferAsync(prompt, parameters, token))
                    {
                        token.ThrowIfCancellationRequested();
                        output.Append(chunk);
                    }
                }
                finally { clip.ClearMedia(); }
                return output.ToString();
            }, token).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    private static string SystemPrompt(string language) => language == "fr"
        ? "Tu es Nomi, un outil de productivité. Tu regardes une capture de la fenêtre de travail de l'utilisateur pour comprendre la tâche en cours. Tu restes factuel, bref et professionnel. Tu ne commentes jamais la personne, seulement le travail visible. Réponds en français."
        : "You are Nomi, a productivity tool. You look at a capture of the user's work window to understand the task in progress. Stay factual, brief and professional. Never comment on the person, only on the visible work. Answer in English.";

    private static string UserPrompt(string language, IReadOnlyList<string> tasks)
    {
        var list = tasks.Count == 0
            ? (language == "fr" ? "(aucune)" : "(none)")
            : string.Join("\n", tasks.Select((task, index) => $"{index + 1}. {task.Replace('\n', ' ')}"));
        return language == "fr"
            ? $"""
              Tâches ouvertes de l'utilisateur :
              {list}

              Réponds exactement avec ces rubriques, sans autre texte :
              TASK: ce que l'utilisateur est en train de faire, en 12 mots maximum
              WHERE: l'application et le document ou la zone visibles
              CHECK: un seul point chiffré précis à vérifier (total, pourcentage, date, unité) en citant les valeurs visibles, ou AUCUN
              NEXT: la prochaine étape concrète, en 14 mots maximum
              MATCH: le numéro de la tâche ouverte correspondante, ou 0
              ROWS: si un tableau chiffré est visible, recopie exactement toutes ses lignes (16 au maximum), en-tête et total compris, une par ligne, cellules séparées par |, sans en omettre aucune, sinon AUCUN
              """
            : $"""
              User's open tasks:
              {list}

              Reply with exactly these fields and nothing else:
              TASK: what the user is doing, 12 words max
              WHERE: the visible application and document or area
              CHECK: one precise numeric point to verify (total, percentage, date, unit) quoting the visible values, or NONE
              NEXT: the next concrete step, 14 words max
              MATCH: the number of the matching open task, or 0
              ROWS: if a numeric table is visible, copy every row exactly (16 max), header and total included, one per line, cells separated by |, never skipping a row, otherwise NONE
              """;
    }

    public static ScreenInsight Parse(string text, int tasks, double seconds)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match field in Field.Matches(text.Replace("**", "", StringComparison.Ordinal)))
            fields.TryAdd(field.Groups[1].Value, Clean(field.Groups[2].Value));
        string Get(string key) => fields.TryGetValue(key, out var value) ? value : "";
        var check = Get("CHECK");
        if (Regex.IsMatch(check, @"^(aucun|aucune|none|n/?a|rien|nothing)\b", RegexOptions.IgnoreCase)) check = "";
        var match = int.TryParse(Regex.Match(Get("MATCH"), @"\d+").Value, out var number) && number >= 1 && number <= tasks ? number : 0;
        var task = Get("TASK");
        if (task.Length == 0) throw new InferenceException("vision-unclear");
        var rows = Regex.Match(text, @"^\s*ROWS\s*[:：]\s*(.*)$", RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var table = rows.Success
            ? TableCheck.ParseRows(rows.Groups[1].Value.Replace("<|im_end|>", "", StringComparison.Ordinal).Split('\n'))
            : [];
        return new ScreenInsight(task, Get("WHERE"), check, Get("NEXT"), match, seconds, table);
    }

    private static string Clean(string value)
    {
        value = value.Replace("<|im_end|>", "", StringComparison.Ordinal).Trim().Trim('"', '«', '»').Trim();
        return value.Length > 220 ? value[..220].TrimEnd() + "…" : value;
    }

    public async Task UnloadAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try { Release(); }
        finally { gate.Release(); }
    }

    private void Release()
    {
        clip?.Dispose();
        clip = null;
        weights?.Dispose();
        weights = null;
    }

    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (disposed) return;
            disposed = true;
            Release();
            model.Dispose();
            projector.Dispose();
        }
        finally { gate.Release(); }
    }

    private sealed class SyncProgress(Action<ModelProgress> report) : IProgress<ModelProgress>
    {
        public void Report(ModelProgress value) => report(value);
    }
}
