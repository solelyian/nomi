using System.Text.Json;

namespace Nomi;

public sealed record RoutingEntry(DateTimeOffset Time, string Primitive, string Level, string Reason, int Score,
    string? EscalatedFrom, bool? Verified, string Model, double Seconds, int Documents);

public sealed class RoutingLog
{
    public const int Limit = 500;
    public string FilePath { get; }
    public List<RoutingEntry> Entries { get; } = [];

    public RoutingLog(string? path = null)
    {
        FilePath = path ?? Path.Combine(SecureFile.Folder, "routing.json");
        try
        {
            var text = SecureFile.Read(FilePath);
            if (text is not null) Entries.AddRange(JsonSerializer.Deserialize<List<RoutingEntry>>(text) ?? []);
        }
        catch (JsonException) { SecureFile.SetAside(FilePath); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException) { }
    }

    public void Add(RoutingEntry entry)
    {
        Entries.Add(entry);
        if (Entries.Count > Limit) Entries.RemoveRange(0, Entries.Count - Limit);
        SecureFile.Write(FilePath, JsonSerializer.Serialize(Entries));
    }

    public void Clear()
    {
        Entries.Clear();
        SecureFile.Delete(FilePath);
    }
}

public static class Escalation
{
    public static string? Next(string level) => level switch
    {
        Levels.Fast => Levels.Applied,
        Levels.Applied => Levels.Deep,
        _ => null
    };

    public static int Score(string text, int documents)
    {
        var score = Math.Min(40, text.Length / 20);
        score += Math.Min(20, text.Count(char.IsDigit) * 2);
        score += documents * 15;
        score += text.Count(c => c == '?') > 1 ? 10 : 0;
        score += text.Split([" et ", " puis ", " and ", " then "], StringSplitOptions.None).Length > 2 ? 10 : 0;
        return Math.Clamp(score, 0, 100);
    }

    public static string Level(Route route, string text, int documents, string chosen) =>
        chosen != Levels.Auto && Levels.All.Contains(chosen) ? route.Level
        : route.Level == Levels.Fast && Score(text, documents) >= 50 ? Levels.Applied
        : route.Level;
}
