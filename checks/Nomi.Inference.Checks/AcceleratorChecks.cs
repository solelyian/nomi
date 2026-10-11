using Nomi;

internal static class AcceleratorChecks
{
    internal static async Task RunAsync()
    {
        var qnn = new AcceleratorVariant("deepseek-r1-distill-qwen-7b-qnn-npu:1", "NPU", "QNNExecutionProvider", 4096, 2048, 3_987_000_000);
        var intel = new AcceleratorVariant("deepseek-r1-distill-qwen-7b-openvino-npu:4", "NPU", "OpenVINOExecutionProvider", 4224, 2048, 4_478_000_000);
        var amd = new AcceleratorVariant("deepseek-r1-distill-qwen-7b-vitis-npu:2", "NPU", "VitisAIExecutionProvider", 131072, 2048, 5_991_000_000);
        var gpu = new AcceleratorVariant("deepseek-r1-distill-qwen-7b-generic-gpu:3", "GPU", "DmlExecutionProvider", 131072, 2048, 5_000_000_000);
        var cpuVariant = new AcceleratorVariant("deepseek-r1-distill-qwen-7b-generic-cpu:3", "CPU", "CPUExecutionProvider", 131072, 2048, 5_000_000_000);
        AcceleratorVariant[] all = [cpuVariant, gpu, amd, intel, qnn];
        Check(AcceleratorSelection.Pick(all, Set("QNNExecutionProvider", "CPUExecutionProvider")) == qnn, "Qualcomm NPU selected");
        Check(AcceleratorSelection.Pick(all, Set("OpenVINOExecutionProvider")) == intel, "Intel NPU selected");
        Check(AcceleratorSelection.Pick(all, Set("VitisAIExecutionProvider")) == amd, "AMD NPU selected");
        Check(AcceleratorSelection.Pick(all, Set("DmlExecutionProvider", "CPUExecutionProvider")) is null, "No NPU means no accelerated pick");
        Check(AcceleratorSelection.Pick([qnn], Set("OpenVINOExecutionProvider")) is null, "Unregistered provider never picked");
        Check(AcceleratorSelection.Label("QNNExecutionProvider") == "NPU Qualcomm"
            && AcceleratorSelection.Label("OpenVINOExecutionProvider") == "NPU Intel"
            && AcceleratorSelection.Label("VitisAIExecutionProvider") == "NPU AMD"
            && AcceleratorSelection.Label("CPUExecutionProvider") == "", "Provider labels");

        var quick = AcceleratorSelection.Budget(qnn, 3000, 512, 768);
        Check(quick == new TokenBudget(512, 768), "Quick thinking budget fits NPU context");
        var deep = AcceleratorSelection.Budget(qnn, 3000, 3072, 768);
        Check(deep is not null && deep.Think + deep.Answer <= 2048 && deep.Answer == 768, "Deep reasoning capped to NPU output limit");
        Check(AcceleratorSelection.Budget(qnn, 12_000, 512, 768) is null, "Long context rejected on 4K NPU");
        Check(AcceleratorSelection.Budget(amd, 12_000, 3072, 768) is not null, "Long context accepted on AMD NPU");

        var cpu = new FakeEngine("Qwen3", "CPU · llama.cpp", hasModel: true);
        var npu = new FakeEngine("DeepSeek", "NPU Intel", hasModel: true) { Unavailable = "no-npu" };
        var router = new AdaptiveInferenceClient(cpu, npu);
        await router.PrepareAsync(false, null, default);
        Check(router.Ready && router.Model == "Qwen3" && router.Accelerator == "CPU · llama.cpp" && router.FallbackReason == "no-npu", "CPU fallback without NPU");
        Check(npu.Disposed, "Unavailable NPU engine released");

        cpu = new FakeEngine("Qwen3", "CPU · llama.cpp", hasModel: true);
        npu = new FakeEngine("DeepSeek", "NPU Qualcomm", hasModel: false) { NeedsDownload = true };
        router = new AdaptiveInferenceClient(cpu, npu);
        await Error(() => router.PrepareAsync(false, null, default), "local-model-missing");
        Check(!router.Ready && !router.HasModel && cpu.Prepared == 0, "NPU model requires consent before download");
        npu.NeedsDownload = false;
        await router.PrepareAsync(true, null, default);
        Check(router.Ready && router.Model == "DeepSeek" && router.Accelerator == "NPU Qualcomm" && cpu.Prepared == 0, "NPU engine active after consent");

        var output = new List<string>();
        var reasoning = await router.GenerateNomiAsync("80 € moins 15 %", "verify", "fr", "steps", default, reasoning: true);
        Check(reasoning.Accepted && npu.LastReasoning && router.AnsweredBy == "DeepSeek", "NPU answer passes Nomi policy with reasoning flag");
        npu.ContextLimit = true;
        await router.GenerateAsync("long", "verify", "fr", "steps", output.Add, default);
        Check(cpu.Prepared == 1 && router.AnsweredBy == "Qwen3" && output.SequenceEqual(["Qwen3"]), "Long context routed to CPU model");

        cpu = new FakeEngine("Qwen3", "CPU · llama.cpp", hasModel: false);
        npu = new FakeEngine("DeepSeek", "NPU AMD", hasModel: true) { ContextLimit = true };
        router = new AdaptiveInferenceClient(cpu, npu);
        await router.PrepareAsync(false, null, default);
        await Error(() => router.GenerateAsync("long", "verify", "fr", "steps", _ => { }, default), "document-context-limit");
        Check(cpu.Prepared == 0, "No CPU download triggered by long context");

        cpu = new FakeEngine("Qwen3", "CPU · llama.cpp", hasModel: true);
        npu = new FakeEngine("DeepSeek", "NPU Intel", hasModel: true) { Unavailable = "load-failed" };
        router = new AdaptiveInferenceClient(cpu, npu);
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            npu.Unavailable = null;
            npu.CancelOnPrepare = true;
            await ExpectCancel(() => router.PrepareAsync(true, null, cancelled.Token));
            Check(!router.Ready && cpu.Prepared == 0, "Cancellation never falls back silently");
        }
        npu.CancelOnPrepare = false;
        npu.Unavailable = "load-failed";
        await router.PrepareAsync(true, null, default);
        Check(router.Ready && router.Model == "Qwen3" && router.FallbackReason == "load-failed", "CPU fallback when NPU load fails");
        Console.WriteLine("Accelerator selection and fallback checks passed.");
    }

    private static HashSet<string> Set(params string[] names) => new(names, StringComparer.OrdinalIgnoreCase);

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static async Task Error(Func<Task> work, string code)
    {
        try { await work(); throw new InvalidOperationException($"Expected {code}"); }
        catch (InferenceException error) when (error.Message == code) { }
    }

    private static async Task ExpectCancel(Func<Task> work)
    {
        try { await work(); throw new InvalidOperationException("Expected cancellation"); }
        catch (OperationCanceledException) { }
    }

    private sealed class FakeEngine(string model, string accelerator, bool hasModel) : ILocalEngine
    {
        private bool ready;
        public string? Unavailable { get; set; }
        public bool NeedsDownload { get; set; }
        public bool ContextLimit { get; set; }
        public bool CancelOnPrepare { get; set; }
        public bool LastReasoning { get; private set; }
        public bool Disposed { get; private set; }
        public int Prepared { get; private set; }
        public string Model => model;
        public string Accelerator => accelerator;
        public bool HasModel => hasModel;
        public bool Ready => ready;
        public long DownloadBytes => 1;

        public Task PrepareAsync(bool allowDownload, IProgress<ModelProgress>? progress, CancellationToken token)
        {
            if (CancelOnPrepare) token.ThrowIfCancellationRequested();
            if (Unavailable is not null) throw new AcceleratorUnavailableException(Unavailable);
            if (NeedsDownload && !allowDownload) throw new InferenceException("local-model-missing");
            Prepared++;
            ready = true;
            return Task.CompletedTask;
        }

        public Task<bool> GenerateAsync(string prompt, string action, string language, string format,
            Action<string> onToken, CancellationToken cancellationToken, string instruction = "",
            IReadOnlyList<AttachedDocument>? documents = null, bool reasoning = false, Action<string>? onReasoning = null)
        {
            if (ContextLimit) throw new InferenceException("document-context-limit");
            LastReasoning = reasoning;
            onToken(prompt == "long" ? model : "1. 80 × 0,85 = 68\nRésultat : 68 €");
            return Task.FromResult(false);
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
