using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Nomi;

public static class ChatActions
{
    public const string Create = "create";
    public const string Postpone = "postpone";
    public const string Split = "split";
    public const string Focus = "focus";
    public static readonly string[] All = [Create, Postpone, Split, Focus];
}

public sealed class ChatAction
{
    public string Kind { get; set; } = ChatActions.Create;
    public string? TaskId { get; set; }
    public string Title { get; set; } = "";
    public bool Done { get; set; }
}

public sealed class ChatEntry
{
    public string Role { get; set; } = "user";
    public string Text { get; set; } = "";
    public DateTimeOffset Time { get; set; } = DateTimeOffset.Now;
    public List<ChatAction> Actions { get; set; } = [];
}

public sealed record ChatContext(string Text, IReadOnlyDictionary<string, string> Refs, int Count);

public sealed class ChatStore(string? file = null)
{
    public const int Limit = 60;
    public string Path { get; } = file ?? System.IO.Path.Combine(SecureFile.Folder, "conversation.json");
    public List<ChatEntry> Items { get; private set; } = [];

    public void Load()
    {
        try
        {
            Items = SecureFile.Read(Path) is { } text ? JsonSerializer.Deserialize<List<ChatEntry>>(text) ?? [] : [];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            Items = [];
        }
        Items = Items.Where(item => item.Role is "user" or "nomi" && item.Text.Length + item.Actions.Count > 0).TakeLast(Limit).ToList();
        foreach (var item in Items) item.Actions = item.Actions.Where(action => ChatActions.All.Contains(action.Kind)).ToList();
    }

    public void Add(ChatEntry message)
    {
        Items.Add(message);
        if (Items.Count > Limit) Items.RemoveRange(0, Items.Count - Limit);
        Save();
    }

    public void Save() => SecureFile.Write(Path, JsonSerializer.Serialize(Items));

    public void Clear()
    {
        Items.Clear();
        SecureFile.Delete(Path);
    }
}

public static class Conversation
{
    public const int Budget = 5600;
    public const int MaxTasks = 12;
    public const int QuestionLimit = 1500;
    public const int MaxActions = 3;
    private static readonly string Line = ((char)10).ToString();

    private static string When(DateTimeOffset value) => value.ToString("dddd yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    public static ChatContext Context(IEnumerable<WorkTask> tasks, DateTimeOffset now, Day day, Func<string?, string?> spaceTitle)
    {
        var endOfDay = day.At(now, TimeSpan.FromDays(1));
        var open = tasks.Where(task => task.Column != Columns.Done)
            .OrderBy(task => task.Column == Columns.Doing ? 0 : task.Due is DateTimeOffset due && due < endOfDay ? 1 : 2)
            .ThenBy(task => task.Due ?? DateTimeOffset.MaxValue)
            .ThenBy(task => task.Order)
            .Take(MaxTasks).ToArray();
        var refs = new Dictionary<string, string>();
        var text = new StringBuilder();
        text.Append("Now: ").Append(When(now)).Append(". Working day ends at ")
            .Append(day.End.ToString(@"hh\:mm", CultureInfo.InvariantCulture)).Append(". Free working time left today: ")
            .Append(Math.Round(day.Available(now)).ToString(CultureInfo.InvariantCulture)).Append(" min.").Append(Line);
        text.Append(open.Length == 0 ? "The user has no open tasks." : "Open tasks of the user:").Append(Line);
        for (var index = 0; index < open.Length; index++)
        {
            var task = open[index];
            var id = $"T{index + 1}";
            refs[id] = task.Id;
            text.Append(id).Append(" | ").Append(Short(task.Title, 120)).Append(" | ").Append(task.Column);
            if (task.Due is DateTimeOffset due) text.Append(" | due ").Append(When(due)).Append(due < now ? " (overdue)" : "");
            text.Append(" | estimate ").Append(task.Estimate.ToString(CultureInfo.InvariantCulture)).Append(" min");
            if (task.Steps.Count > 0) text.Append(" | steps done ").Append(task.StepsDone.ToString(CultureInfo.InvariantCulture))
                .Append('/').Append(task.Steps.Count.ToString(CultureInfo.InvariantCulture));
            if (spaceTitle(task.Space) is { Length: > 0 } space) text.Append(" | space ").Append(Short(space, 40));
            text.Append(Line);
        }
        return new(text.ToString().Trim(), refs, open.Length);
    }

    public static string Prompt(ChatContext context, IReadOnlyList<ChatEntry> earlier, string question)
    {
        question = Short(question.Trim(), QuestionLimit);
        var room = Budget - context.Text.Length - question.Length - 420;
        var kept = new List<string>();
        var index = earlier.Count - 1;
        for (; index >= 0; index--)
        {
            var item = earlier[index];
            var entry = $"{(item.Role == "user" ? "User" : "Nomi")}: {Short(item.Text.ReplaceLineEndings(" "), 800)}";
            if (entry.Length + 1 > room) break;
            room -= entry.Length + 1;
            kept.Insert(0, entry);
        }
        var text = new StringBuilder(context.Text).Append(Line).Append(Line);
        var dropped = earlier.Take(index + 1).Where(item => item.Role == "user").Select(item => FirstWords(item.Text, 8)).ToArray();
        if (dropped.Length > 0)
            text.Append("Earlier topics, summarised: ").Append(Short(string.Join("; ", dropped), 300)).Append(Line);
        if (kept.Count > 0) text.Append("Conversation so far:").Append(Line).Append(string.Join(Line, kept)).Append(Line).Append(Line);
        text.Append("User: ").Append(question);
        return text.ToString();
    }

    public static string Instruction() =>
        "You are having an ongoing conversation with the user about their work. Answer the last user message using their open tasks and the conversation so far. "
        + "Refer to tasks by their title, never by their T number. Be concise. You cannot change anything yourself: never claim that you created, moved or changed a task. "
        + "When an action would clearly help, add at most three lines at the very end, each exactly in one of these forms: "
        + "'Action: create | <a short task sentence, which may include a day, a time and a duration>', 'Action: postpone | T<n>', 'Action: split | T<n>', 'Action: focus | T<n>'. "
        + "Only use T numbers from the task list. The user decides whether to apply each action.";

    public static (string Text, List<ChatAction> Actions) Read(string output, ChatContext context, IReadOnlyCollection<WorkTask> tasks)
    {
        var text = new List<string>();
        var actions = new List<ChatAction>();
        foreach (var raw in output.ReplaceLineEndings(Line).Split(Line))
        {
            var line = raw.Trim().TrimStart('-', '*', '•').Trim();
            if (!line.StartsWith("action", StringComparison.OrdinalIgnoreCase) || line.IndexOf(':') is < 6 or > 8)
            {
                text.Add(raw.TrimEnd());
                continue;
            }
            var parts = line[(line.IndexOf(':') + 1)..].Split('|', 2);
            if (parts.Length != 2 || actions.Count >= MaxActions) continue;
            var kind = parts[0].Trim().ToLowerInvariant();
            var argument = parts[1].Trim().Trim('<', '>', '"', '\'', '[', ']', '.').Trim();
            ChatAction? action = null;
            if (kind == ChatActions.Create)
            {
                if (argument.Length is >= 2 and <= 160 && !context.Refs.ContainsKey(argument.ToUpperInvariant()))
                    action = new ChatAction { Kind = kind, Title = argument };
            }
            else if (ChatActions.All.Contains(kind) && context.Refs.TryGetValue(argument.ToUpperInvariant(), out var id)
                && tasks.FirstOrDefault(task => task.Id == id) is { Column: not Columns.Done } task)
                action = new ChatAction { Kind = kind, TaskId = id, Title = task.Title };
            if (action is not null && !actions.Any(other => other.Kind == action.Kind && other.TaskId == action.TaskId
                && string.Equals(other.Title, action.Title, StringComparison.OrdinalIgnoreCase)))
                actions.Add(action);
        }
        return (string.Join(Line, text).Trim(), actions);
    }

    private static string FirstWords(string text, int count) =>
        string.Join(' ', text.Split((char[]?)null, count + 1, StringSplitOptions.RemoveEmptyEntries).Take(count));

    private static string Short(string text, int limit) => text.Length <= limit ? text : text[..(limit - 1)].TrimEnd() + "…";
}
