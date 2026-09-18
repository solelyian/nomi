using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Nomi;

internal static class ModelChecks
{
    internal static async Task RunAsync(string[] args)
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Nyne", "Nomi", "checks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var bytes = Encoding.UTF8.GetBytes("Test model payload: 120 + 150 = 270");
        var definition = new ModelDefinition("Test", "test.gguf", new Uri("https://example.test/model"),
            bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)));
        var path = Path.Combine(directory, definition.FileName);
        var partial = path + ".partial";
        var requests = 0;
        using var store = new ModelStore(directory, definition, new Handler((request, _) =>
        {
            requests++;
            var from = request.Headers.Range?.Ranges.Single().From ?? 0;
            var response = new HttpResponseMessage(from > 0 ? HttpStatusCode.PartialContent : HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(bytes[(int)from..])
            };
            if (from > 0) response.Content.Headers.ContentRange = new ContentRangeHeaderValue(from, bytes.Length - 1, bytes.Length);
            return Task.FromResult(response);
        }));
        try
        {
            await Error(() => store.EnsureAsync(false, null, default), "local-model-missing");
            Check(requests == 0, "No download without user action");
            await File.WriteAllBytesAsync(partial, bytes[..8]);
            await store.EnsureAsync(true, null, default);
            Check(requests == 1 && File.Exists(path) && !File.Exists(partial), "Resume and atomic promotion");
            Check((await File.ReadAllBytesAsync(path)).SequenceEqual(bytes), "Complete verified payload");
            await store.EnsureAsync(false, null, default);
            Check(requests == 1, "Offline reuse");

            await File.WriteAllBytesAsync(path, new byte[bytes.Length]);
            using var corruptStore = new ModelStore(directory, definition, new Handler((_, _) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[bytes.Length]) })));
            await Error(() => corruptStore.EnsureAsync(false, null, default), "model-corrupt");
            await Error(() => corruptStore.EnsureAsync(true, null, default), "model-corrupt");
            Check(!File.Exists(partial), "Corrupt downloads removed");

            File.Delete(path);
            await File.WriteAllBytesAsync(partial, bytes[..8]);
            using var noRange = new ModelStore(directory, definition, new Handler((_, _) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) })));
            await noRange.EnsureAsync(true, null, default);
            Check((await File.ReadAllBytesAsync(path)).SequenceEqual(bytes), "Ignored range restarts, never appends");
            File.Delete(path);

            using var invalidRange = new ModelStore(directory, definition, new Handler((_, _) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = new ByteArrayContent(bytes) })));
            await Error(() => invalidRange.EnsureAsync(true, null, default), "model-download-error");
            Check(!File.Exists(path), "Invalid range never promoted");

            using var cancelled = new CancellationTokenSource();
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var slow = new ModelStore(directory, definition, new Handler(async (_, token) =>
            {
                started.SetResult();
                await Task.Delay(Timeout.Infinite, token);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }));
            var pending = slow.EnsureAsync(true, null, cancelled.Token);
            await started.Task;
            await Error(() => slow.EnsureAsync(true, null, default), "busy");
            cancelled.Cancel();
            try { await pending; throw new InvalidOperationException("Cancellation ignored"); }
            catch (OperationCanceledException) { }
            Check(!File.Exists(path), "Cancellation never exposes partial model");
            await Error(() => slow.EnsureAsync(false, null, default), "local-model-missing");
            Console.WriteLine("Model checks passed: consent, resume, integrity, offline reuse, ranges, cancellation and concurrency.");
        }
        finally { Directory.Delete(directory, true); }

        if (args.Contains("--embedded-live")) await LiveAsync();
    }

    private static async Task LiveAsync()
    {
        var directory = Environment.GetEnvironmentVariable("NOMI_TEST_MODEL_DIR");
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(20));
        await using var client = new LocalInferenceClient(new ModelStore(directory));
        await client.PrepareAsync(true, null, deadline.Token);
        foreach (var language in new[] { "fr", "en" })
        {
            var cases = language == "fr"
                ? new[] {
                    ("understand", "Que signifie une hausse de 120 à 150 commandes ?"),
                    ("verify", "Un prix de 80 € baisse de 15 %. Vérifie le prix de 68 €."),
                    ("convert", "Convertis 2 heures en minutes."),
                    ("plan", "Prévois 60 minutes pour lire 20 minutes, rédiger 30 minutes et faire une pause."),
                    ("communicate", "Annonce simplement à un collègue que le prix final est de 68 €."),
                    ("learn", "Explique comment calculer 10 % de 200.") }
                : new[] {
                    ("understand", "Explain an increase from 120 to 150 orders."),
                    ("verify", "An 80 euro price is reduced by 15%. Check the final price of 68 euros."),
                    ("convert", "Convert 2 hours to minutes."),
                    ("plan", "Plan 60 minutes with 20 minutes reading, 30 minutes writing and a break."),
                    ("communicate", "Tell a colleague clearly that the final price is 68 euros."),
                    ("learn", "Explain how to calculate 10% of 200.") };
            foreach (var (action, prompt) in cases)
            {
                var result = await client.GenerateNomiAsync(prompt, action, language, "summary", deadline.Token);
                Check(result.Accepted && result.Text.Length > 0, $"Embedded {language}/{action}");
                Console.WriteLine($"{language}/{action}: {result.Text}");
            }
        }
        var document = DocumentText.Read("offre.pdf",
            await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "documents", "offre.pdf")), deadline.Token);
        var answer = await client.GenerateNomiAsync("Quel total HT est indiqué dans offre.pdf ?", "understand", "fr", "summary",
            deadline.Token, [document]);
        Check(answer.Text.Contains("85"), "Embedded PDF answer");
        using var cancelled = new CancellationTokenSource();
        try
        {
            await client.GenerateAsync("Count to 100.", "learn", "en", "steps", _ => cancelled.Cancel(), cancelled.Token);
            throw new InvalidOperationException("Embedded cancellation ignored");
        }
        catch (OperationCanceledException) { }
        var recovery = await client.GenerateNomiAsync("Convert 2 hours to minutes.", "convert", "en", "summary", deadline.Token);
        Check(recovery.Text.Contains("120"), "Embedded recovery after cancellation");
        Console.WriteLine("Embedded live checks passed: 12 language/action paths, PDF, cancellation and recovery.");
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static async Task Error(Func<Task> work, string code)
    {
        try { await work(); throw new InvalidOperationException($"Expected {code}"); }
        catch (InferenceException error) when (error.Message == code) { }
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => send(request, token);
    }
}
