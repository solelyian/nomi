using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;

namespace Nomi;

public sealed record ModelDefinition(string Name, string FileName, Uri Url, long Bytes, string Sha256)
{
    public static ModelDefinition Load() => JsonSerializer.Deserialize<ModelDefinition>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "content", "local-model.json")),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new InvalidDataException("Invalid local model configuration.");
}

public sealed record ModelProgress(string Stage, double Fraction = 0);

public sealed class ModelStore : IDisposable
{
    private readonly HttpClient http;
    private readonly SemaphoreSlim gate = new(1);
    private bool verified;
    public ModelDefinition Definition { get; }
    public string ModelPath { get; }
    public bool Exists => File.Exists(ModelPath);

    public ModelStore(string? directory = null, ModelDefinition? definition = null, HttpMessageHandler? handler = null)
    {
        Definition = definition ?? ModelDefinition.Load();
        if (Path.GetFileName(Definition.FileName) != Definition.FileName
            || Definition.Url.Scheme != "https" || Definition.Bytes <= 0
            || Definition.Sha256.Length != 64)
            throw new InvalidDataException("Invalid local model configuration.");
        directory ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nyne", "Nomi", "models");
        ModelPath = Path.Combine(directory, Definition.FileName);
        http = handler is null ? new HttpClient() : new HttpClient(handler);
        http.Timeout = Timeout.InfiniteTimeSpan;
    }

    public async Task EnsureAsync(bool allowDownload, IProgress<ModelProgress>? progress, CancellationToken token)
    {
        if (!await gate.WaitAsync(0, token)) throw new InferenceException("busy");
        try
        {
            if (verified && Exists) return;
            if (Exists && await VerifyAsync(ModelPath, progress, token))
            {
                verified = true;
                return;
            }
            if (!allowDownload) throw new InferenceException(Exists ? "model-corrupt" : "local-model-missing");
            Directory.CreateDirectory(Path.GetDirectoryName(ModelPath)!);
            var partial = ModelPath + ".partial";
            if (File.Exists(partial) && new FileInfo(partial).Length > Definition.Bytes) File.Delete(partial);
            var offset = File.Exists(partial) ? new FileInfo(partial).Length : 0;
            var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(ModelPath))!);
            if (drive.AvailableFreeSpace < Definition.Bytes - offset + 100_000_000)
                throw new InferenceException("model-disk-space");
            if (offset < Definition.Bytes)
                await DownloadAsync(partial, offset, progress, token);
            if (!await VerifyAsync(partial, progress, token))
            {
                File.Delete(partial);
                throw new InferenceException("model-corrupt");
            }
            File.Move(partial, ModelPath, true);
            verified = true;
        }
        finally { gate.Release(); }
    }

    private async Task DownloadAsync(string partial, long offset, IProgress<ModelProgress>? progress, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Definition.Url);
        if (offset > 0) request.Headers.Range = new RangeHeaderValue(offset, null);
        using var connectionTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        connectionTimeout.CancelAfter(TimeSpan.FromSeconds(60));
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, connectionTimeout.Token);
        response.EnsureSuccessStatusCode();
        if (response.StatusCode == HttpStatusCode.PartialContent)
        {
            if (response.Content.Headers.ContentRange is not { } range || range.From != offset
                || range.To != Definition.Bytes - 1 || range.Length != Definition.Bytes)
                throw new InferenceException("model-download-error");
        }
        else offset = 0;
        if (response.Content.Headers.ContentLength is { } length && length != Definition.Bytes - offset)
            throw new InferenceException("model-download-error");
        await using var input = await response.Content.ReadAsStreamAsync(token);
        await using var output = new FileStream(partial, offset > 0 ? FileMode.Append : FileMode.Create,
            FileAccess.Write, FileShare.None, 131072, FileOptions.Asynchronous);
        var buffer = new byte[131072];
        var total = offset;
        var lastReport = Environment.TickCount64;
        progress?.Report(new("model-downloading", (double)total / Definition.Bytes));
        while (true)
        {
            using var idleTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            idleTimeout.CancelAfter(TimeSpan.FromSeconds(60));
            var count = await input.ReadAsync(buffer, idleTimeout.Token);
            if (count == 0) break;
            if (total + count > Definition.Bytes) throw new InferenceException("model-download-error");
            await output.WriteAsync(buffer.AsMemory(0, count), token);
            total += count;
            if (Environment.TickCount64 - lastReport >= 150)
            {
                progress?.Report(new("model-downloading", (double)total / Definition.Bytes));
                lastReport = Environment.TickCount64;
            }
        }
        if (total != Definition.Bytes) throw new InferenceException("model-download-error");
    }

    private async Task<bool> VerifyAsync(string path, IProgress<ModelProgress>? progress, CancellationToken token)
    {
        if (new FileInfo(path).Length != Definition.Bytes) return false;
        progress?.Report(new("model-verifying"));
        await using var file = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(file, token);
        return Convert.ToHexString(hash).Equals(Definition.Sha256, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose() => http.Dispose();
}
