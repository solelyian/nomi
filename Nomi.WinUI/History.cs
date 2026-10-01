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

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nyne", "Nomi", "history.json");

    public static List<HistoryEntry> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return [];
            return JsonSerializer.Deserialize<List<HistoryEntry>>(File.ReadAllText(FilePath)) ?? [];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }

    public static void Save(IReadOnlyList<HistoryEntry> entries)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(entries.Take(Limit)));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            StartupDiagnostics.Write($"History not saved: {error.GetType().Name}");
        }
    }
}
