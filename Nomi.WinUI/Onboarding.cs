namespace Nomi;

public static class Onboarding
{
    public const string Welcome = "welcome";
    public const string Tasks = "tasks";
    public const int MaxTasks = 12;
    public const int MaxTitle = 200;

    public static readonly string[] Steps = [Welcome, "reading", "numbers", "day", "pace", "model", "presence", "agenda", "data", Tasks];

    public static int Index(string step) => Math.Max(0, Array.IndexOf(Steps, step));

    public static string? Next(string step) => Index(step) + 1 < Steps.Length ? Steps[Index(step) + 1] : null;

    public static string? Previous(string step) => Index(step) > 0 ? Steps[Index(step) - 1] : null;

    public static bool CanSkip(string step) => step != Welcome;

    public static IReadOnlyList<string> TaskLines(string text) => text
        .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(line => line.TrimStart('-', '*', '•', ' ').Trim())
        .Where(line => line.Length > 0)
        .Select(line => line.Length > MaxTitle ? line[..MaxTitle].TrimEnd() : line)
        .Distinct(StringComparer.CurrentCultureIgnoreCase)
        .Take(MaxTasks)
        .ToList();
}
