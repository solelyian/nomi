using System.Net;
using System.Security.Cryptography;
using System.Text;
using Nomi;

internal static class ResourceChecks
{
    internal static async Task RunAsync()
    {
        var count = 0;
        void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            count++;
        }

        LocalResource Item(string id, long bytes, bool present = false, bool busy = false, string stage = "local-model-missing", double fraction = 0) =>
            new(id, id, bytes, present, busy, stage, fraction);

        LocalResource[] missing = [Item("reasoning", 5_000), Item("vision", 6_000)];
        Check(LocalResources.State(missing) == LocalResources.Missing && LocalResources.MissingBytes(missing) == 11_000, "Both models missing, sizes added");
        Check(LocalResources.Fraction(missing) == 0 && LocalResources.Active(missing) is null && LocalResources.Error(missing) is null, "Nothing started, no error");

        LocalResource[] half = [Item("reasoning", 5_000, true, stage: "local-model-ready"), Item("vision", 5_000, false, true, LocalResources.Downloading, 0.5)];
        Check(LocalResources.State(half) == LocalResources.Busy && LocalResources.Active(half)!.Id == "vision", "The model being downloaded is the active one");
        Check(Math.Abs(LocalResources.Fraction(half) - 0.75) < 1e-9, "Overall progress is weighted by bytes");
        Check(LocalResources.MissingBytes(half) == 5_000, "A model already on the computer is not counted again");

        LocalResource[] verifying = [Item("reasoning", 5_000, false, true, "model-verifying", 0.9), Item("vision", 5_000)];
        Check(LocalResources.Fraction(verifying) == 0, "Verification does not count as downloaded bytes");

        LocalResource[] paused = [Item("reasoning", 5_000, true, stage: "local-model-ready"), Item("vision", 5_000, stage: LocalResources.Paused)];
        Check(LocalResources.State(paused) == LocalResources.Paused && LocalResources.Error(paused) is null, "A pause is not an error");

        LocalResource[] failed = [Item("reasoning", 5_000, stage: "model-download-error"), Item("vision", 5_000)];
        Check(LocalResources.State(failed) == "model-download-error" && LocalResources.Error(failed) == "model-download-error", "A failed download is reported");

        LocalResource[] ready = [Item("reasoning", 5_000, true, stage: "local-model-ready"), Item("vision", 5_000, true)];
        Check(LocalResources.State(ready) == LocalResources.Ready && LocalResources.Fraction(ready) == 1 && LocalResources.MissingBytes(ready) == 0, "Both models present");
        Check(LocalResources.Fraction([]) == 1 && LocalResources.State([]) == LocalResources.Ready, "No resources means nothing to download");

        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Nyne", "Nomi", "checks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var weights = Encoding.UTF8.GetBytes(new string('w', 3_000));
            var clip = Encoding.UTF8.GetBytes(new string('c', 1_000));
            ModelDefinition Definition(string file, byte[] bytes) =>
                new(file, file, new Uri($"https://example.test/{file}"), bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)));
            var requests = new List<string>();
            var handler = new Handler(request =>
            {
                requests.Add(request.RequestUri!.AbsolutePath);
                var body = request.RequestUri.AbsolutePath.EndsWith("model.gguf") ? weights : clip;
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) };
            });
            await using var vision = new VisionClient(
                new ModelStore(directory, Definition("model.gguf", weights), handler),
                new ModelStore(directory, Definition("projector.gguf", clip), handler));
            Check(!vision.HasModel && vision.MissingBytes == 4_000 && vision.Bytes == 4_000, "Vision files missing before the download");
            var steps = new List<double>();
            await vision.DownloadAsync(new Inline(value => { if (value.Stage == "model-downloading") steps.Add(value.Fraction); }), default);
            Check(vision.HasModel && vision.MissingBytes == 0 && !vision.Ready, "Downloaded and verified without loading the weights in memory");
            Check(requests.Count == 2 && steps.Count > 0 && steps.Zip(steps.Skip(1)).All(pair => pair.Second >= pair.First) && steps.All(step => step is >= 0 and <= 1),
                "One request per file, progress only moves forward");
            Check(steps.Any(step => step >= 0.74), "The weights take three quarters of the progress");
            await vision.DownloadAsync(null, default);
            Check(requests.Count == 2, "Files already verified are not downloaded again");
        }
        finally
        {
            try { Directory.Delete(directory, true); } catch (IOException) { }
        }
        Console.WriteLine($"{count} resource download checks passed.");
    }

    private sealed class Inline(Action<ModelProgress> report) : IProgress<ModelProgress>
    {
        public void Report(ModelProgress value) => report(value);
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
            Task.FromResult(send(request));
    }
}
