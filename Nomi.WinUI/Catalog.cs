using System.Text.Json;

namespace Nomi;

public sealed record NomiAction(
    string Id,
    string Title,
    string Description,
    string Prompt,
    string[] Steps,
    string Summary);

public sealed record WorkContext(
    string Id,
    string Title,
    string Description,
    string Action);

public sealed record Catalog(
    string Locale,
    Dictionary<string, string> Labels,
    NomiAction[] Actions,
    WorkContext[] Contexts)
{
    public static Catalog Load(string language)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "content", $"{language}.json");
        return JsonSerializer.Deserialize<Catalog>(
            File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException($"Invalid catalog: {language}");
    }
}
