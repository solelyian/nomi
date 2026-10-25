using System.Text.Json;

namespace Nomi;

public sealed record HistoryEntry(
    string Action,
    string? Context,
    string Prompt,
    string Output,
    string[] Rules,
    bool Reasoned,
    bool Summary,
    DateTimeOffset Time,
    double Seconds);

public static class HistoryStore
{
    public const int Limit = 30;

    public static readonly string FilePath = Path.Combine(SecureFile.Folder, "history.json");

    public static List<HistoryEntry> Load()
    {
        try
        {
            return SecureFile.Read(FilePath) is { } text ? JsonSerializer.Deserialize<List<HistoryEntry>>(text) ?? [] : [];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }

    public static void Save(IReadOnlyList<HistoryEntry> entries)
    {
        SecureFile.Write(FilePath, JsonSerializer.Serialize(entries.Take(Limit)));
    }
}
