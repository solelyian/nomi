using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Nomi;

public sealed class InferenceException(string code) : Exception(code);

public sealed class OllamaClient : IDisposable
{
    private readonly HttpClient http;
    private readonly InferenceConfig config;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    public string Model { get; }

    public OllamaClient(HttpMessageHandler? handler = null)
    {
        config = JsonSerializer.Deserialize<InferenceConfig>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "content", "inference.json")), JsonOptions)
            ?? throw new InvalidDataException("Invalid inference configuration.");
        Model = Environment.GetEnvironmentVariable("NOMI_MODEL") ?? config.Model;
        var endpoint = new Uri(Environment.GetEnvironmentVariable("NOMI_OLLAMA_URL") ?? "http://127.0.0.1:11434");
        if (!endpoint.IsLoopback || endpoint.Scheme != "http")
            throw new InvalidOperationException("Ollama must use a local HTTP address.");
        http = handler is null ? new HttpClient() : new HttpClient(handler);
        http.BaseAddress = endpoint;
        http.Timeout = Timeout.InfiniteTimeSpan;
    }

    public async Task<bool> GenerateAsync(
        string prompt, string action, string language, string format,
        Action<string> onToken, CancellationToken cancellationToken, string instruction = "",
        IReadOnlyList<AttachedDocument>? documents = null)
    {
        documents ??= [];
        DocumentText.Validate(documents);
        if ((string.IsNullOrWhiteSpace(prompt) && documents.Count == 0) || prompt.Length > 6000
            || !config.Actions.ContainsKey(action) || !config.Languages.ContainsKey(language)
            || !config.Formats.ContainsKey(format))
            throw new InferenceException("invalid-request");

        var system = string.Join("\n\n", config.System, config.Actions[action],
            config.Formats[format], config.Languages[language]);
        if (instruction.Length > 0) system += $"\n\n{instruction}";
        var content = string.IsNullOrWhiteSpace(prompt) ? config.DocumentPrompts[language] : prompt.Trim();
        if (documents.Count > 0)
        {
            system += $"\n\n{config.Documents}";
            content += DocumentText.Context(documents);
            if (Encoding.UTF8.GetByteCount(content) > 12_000) throw new InferenceException("document-context-limit");
        }
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = JsonContent.Create(new
            {
                model = Model,
                messages = new[] { new { role = "system", content = system }, new { role = "user", content } },
                stream = true,
                think = false,
                keep_alive = "15m",
                options = new { temperature = 0.2, num_ctx = documents.Count > 0 ? 16384 : 4096, num_predict = 768 }
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

    public async Task<PolicyResult> GenerateNomiAsync(
        string prompt, string action, string language, string format, CancellationToken cancellationToken,
        IReadOnlyList<AttachedDocument>? documents = null)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var buffer = new StringBuilder();
            var truncated = await GenerateAsync(prompt, action, language, format, token =>
            {
                buffer.Append(token);
                if (buffer.Length > ResponsePolicy.MaxCharacters) throw new InferenceException("policy-blocked");
            }, cancellationToken, attempt == 0 ? "" : ResponsePolicy.RegenerationInstruction, documents);
            cancellationToken.ThrowIfCancellationRequested();
            if (truncated) throw new InferenceException("incomplete-response");
            var result = ResponsePolicy.Apply(buffer.ToString(), format);
            if (attempt == 0 && result.Reasons.Contains("conflicting-result")) continue;
            if (!result.Accepted) throw new InferenceException("policy-blocked");
            return attempt == 0 ? result : result with { Changes = ["regenerated", .. result.Changes] };
        }
        throw new InferenceException("policy-blocked");
    }

    public void Dispose() => http.Dispose();

    private sealed record InferenceConfig(
        string Model, string System, Dictionary<string, string> Languages,
        Dictionary<string, string> Actions, Dictionary<string, string> Formats,
        string Documents, Dictionary<string, string> DocumentPrompts);
    private sealed record OllamaMessage(string? Content);
    private sealed record OllamaFrame(OllamaMessage? Message, bool Done, string? Done_reason, string? Error);
}
