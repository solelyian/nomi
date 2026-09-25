using System.Text;
using System.Text.Json;

namespace Nomi;

public sealed class InferenceException(string code) : Exception(code);

public abstract class NomiInferenceClient
{
    protected readonly InferenceConfig Config = JsonSerializer.Deserialize<InferenceConfig>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "content", "inference.json")),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new InvalidDataException("Invalid inference configuration.");

    protected (string System, string Content) BuildRequest(
        string prompt, string action, string language, string format, string instruction,
        IReadOnlyList<AttachedDocument>? documents)
    {
        documents ??= [];
        DocumentText.Validate(documents);
        if ((string.IsNullOrWhiteSpace(prompt) && documents.Count == 0) || prompt.Length > 6000
            || !Config.Actions.ContainsKey(action) || !Config.Languages.ContainsKey(language)
            || !Config.Formats.ContainsKey(format))
            throw new InferenceException("invalid-request");

        var system = string.Join("\n\n", Config.System, Config.Actions[action],
            Config.Formats[format], Config.Languages[language]);
        if (instruction.Length > 0) system += $"\n\n{instruction}";
        var content = string.IsNullOrWhiteSpace(prompt) ? Config.DocumentPrompts[language] : prompt.Trim();
        if (documents.Count > 0)
        {
            system += $"\n\n{Config.Documents}";
            content += DocumentText.Context(documents);
            if (Encoding.UTF8.GetByteCount(content) > 12_000) throw new InferenceException("document-context-limit");
        }
        return (system, content);
    }

    public abstract Task<bool> GenerateAsync(
        string prompt, string action, string language, string format,
        Action<string> onToken, CancellationToken cancellationToken, string instruction = "",
        IReadOnlyList<AttachedDocument>? documents = null, bool reasoning = false,
        Action<string>? onReasoning = null);

    public async Task<PolicyResult> GenerateNomiAsync(
        string prompt, string action, string language, string format, CancellationToken cancellationToken,
        IReadOnlyList<AttachedDocument>? documents = null, bool reasoning = false,
        Action<string>? onReasoning = null)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var buffer = new StringBuilder();
            var truncated = await GenerateAsync(prompt, action, language, format, token =>
            {
                buffer.Append(token);
                if (buffer.Length > ResponsePolicy.MaxCharacters) throw new InferenceException("policy-blocked");
            }, cancellationToken, attempt == 0 ? "" : ResponsePolicy.RegenerationInstruction, documents, reasoning, onReasoning);
            cancellationToken.ThrowIfCancellationRequested();
            if (truncated) throw new InferenceException("incomplete-response");
            var result = ResponsePolicy.Apply(buffer.ToString(), format);
            if (attempt == 0 && result.Reasons.Contains("conflicting-result")) continue;
            if (!result.Accepted) throw new InferenceException("policy-blocked");
            return attempt == 0 ? result : result with { Changes = ["regenerated", .. result.Changes] };
        }
        throw new InferenceException("policy-blocked");
    }

    protected sealed record InferenceConfig(
        string Model, string System, Dictionary<string, string> Languages,
        Dictionary<string, string> Actions, Dictionary<string, string> Formats,
        string Documents, Dictionary<string, string> DocumentPrompts);
}
