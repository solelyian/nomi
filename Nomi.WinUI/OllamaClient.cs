using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace Nomi;

public sealed class OllamaClient : NomiInferenceClient, IDisposable
{
    private readonly HttpClient http;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    public string Model { get; }

    public OllamaClient(HttpMessageHandler? handler = null)
    {
        Model = Environment.GetEnvironmentVariable("NOMI_MODEL") ?? Config.Model;
        var endpoint = new Uri(Environment.GetEnvironmentVariable("NOMI_OLLAMA_URL") ?? "http://127.0.0.1:11434");
        if (!endpoint.IsLoopback || endpoint.Scheme != "http")
            throw new InvalidOperationException("Ollama must use a local HTTP address.");
        http = handler is null ? new HttpClient() : new HttpClient(handler);
        http.BaseAddress = endpoint;
        http.Timeout = Timeout.InfiniteTimeSpan;
    }

    public override async Task<bool> GenerateAsync(
        string prompt, string action, string language, string format,
        Action<string> onToken, CancellationToken cancellationToken, string instruction = "",
        IReadOnlyList<AttachedDocument>? documents = null)
    {
        var (system, content) = BuildRequest(prompt, action, language, format, instruction, documents);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = JsonContent.Create(new
            {
                model = Model,
                messages = new[] { new { role = "system", content = system }, new { role = "user", content } },
                stream = true,
                think = false,
                keep_alive = "15m",
                options = new { temperature = 0.2, num_ctx = documents?.Count > 0 ? 16384 : 4096, num_predict = 768 }
            })
        };
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InferenceException(response.StatusCode switch
            {
                HttpStatusCode.NotFound => "model-missing",
                HttpStatusCode.TooManyRequests => "busy",
                _ => "engine-unavailable"
            });
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        var complete = false;
        var received = false;
        var truncated = false;
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var frame = JsonSerializer.Deserialize<OllamaFrame>(line, JsonOptions)
                ?? throw new InferenceException("invalid-response");
            if (frame.Error is not null) throw new InferenceException("engine-unavailable");
            if (frame.Message?.Content is { Length: > 0 } token)
            {
                onToken(token);
                received = true;
            }
            complete |= frame.Done;
            truncated |= frame.Done_reason == "length";
        }
        if (!complete || !received) throw new InferenceException("incomplete-response");
        return truncated;
    }

    public void Dispose() => http.Dispose();
    private sealed record OllamaMessage(string? Content);
    private sealed record OllamaFrame(OllamaMessage? Message, bool Done, string? Done_reason, string? Error);
}
