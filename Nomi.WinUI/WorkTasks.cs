using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Nomi;

public static class Columns
{
    public const string Todo = "todo";
    public const string Doing = "doing";
    public const string Review = "review";
    public const string Done = "done";
    public static readonly string[] All = [Todo, Doing, Review, Done];
}

public sealed class WorkStep
{
    public string Title { get; set; } = "";
    public int Minutes { get; set; }
    public bool Done { get; set; }
}

public sealed class WorkTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    public string Column { get; set; } = Columns.Todo;
    public string? Space { get; set; }
    public string Origin { get; set; } = "manual";
    public string Note { get; set; } = "";
    public int Estimate { get; set; } = 30;
    public DateTimeOffset? Due { get; set; }
    public List<WorkStep> Steps { get; set; } = [];
    public double SpentSeconds { get; set; }
    public DateTimeOffset? RunningSince { get; set; }
    public DateTimeOffset Created { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? Completed { get; set; }
    public double Order { get; set; }

    public double Spent(DateTimeOffset now) =>
        SpentSeconds + (RunningSince is { } since ? Math.Max(0, (now - since).TotalSeconds) : 0);

    public int StepsDone => Steps.Count(step => step.Done);
}

public sealed record Suggestion(string Id, string Kind, string TaskId, string[] Values);

public sealed class TaskBoard
{
    private static readonly Regex StepLine = new(
        @"^\s*(?:[-•*·–]|\d{1,2}\s*[.)])?\s*(?<title>.+?)\s*(?:[|—–:(-]\s*)?(?:~|≈|env\.?\s*)?(?<minutes>\d{1,3})\s*(?:min(?:utes?)?|mn)\b\)?\s*\.?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex PlainStep = new(@"^\s*(?:[-•*·–]|(?:étape|step)?\s*\d{1,2}\s*[.):\-–—])\s+(?<title>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private readonly string path;
    public List<WorkTask> Tasks { get; }
    public HashSet<string> Dismissed { get; } = [];

    public TaskBoard(string? file = null)
    {
        path = file ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nyne", "Nomi", "tasks.json");
        Tasks = Load(path);
    }

    private static List<WorkTask> Load(string file)
    {
        try
        {
            if (!File.Exists(file)) return [];
            var tasks = JsonSerializer.Deserialize<List<WorkTask>>(File.ReadAllText(file)) ?? [];
            return tasks.Where(task => task.Title.Length > 0 && Columns.All.Contains(task.Column)).ToList();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(Tasks));
            File.Move(temporary, path, true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            StartupDiagnostics.Write($"Tasks not saved: {error.GetType().Name}");
        }
    }

    public WorkTask? Find(string id) => Tasks.FirstOrDefault(task => task.Id == id);

    public IEnumerable<WorkTask> Column(string column, string? space) => Tasks
        .Where(task => task.Column == column && (space is null || task.Space == space))
        .OrderBy(task => task.Order).ThenBy(task => task.Created);

    public WorkTask Add(string title, string column = Columns.Todo, string? space = null, string origin = "manual")
    {
        var task = new WorkTask
        {
            Title = title.Trim().Length > 140 ? title.Trim()[..140] : title.Trim(),
            Column = column,
            Space = space,
            Origin = origin,
            Order = NextOrder(column)
        };
        task.Estimate = Math.Max(5, (int)(Math.Round(30 * Pace().Ratio / 5) * 5));
        Tasks.Add(task);
        Save();
        return task;
    }

    private double NextOrder(string column) =>
        Tasks.Where(task => task.Column == column).Select(task => task.Order).DefaultIfEmpty(0).Max() + 1;

    public void Move(WorkTask task, string column, DateTimeOffset now)
    {
        if (task.Column == column) return;
        if (column != Columns.Doing) Pause(task, now);
        task.Column = column;
        task.Order = NextOrder(column);
        task.Completed = column == Columns.Done ? now : null;
        if (column == Columns.Done) task.Steps.ForEach(step => step.Done = true);
        Save();
    }

    public void Start(WorkTask task, DateTimeOffset now)
    {
        foreach (var other in Tasks.Where(item => item != task && item.RunningSince is not null)) Pause(other, now);
        task.RunningSince ??= now;
        if (task.Column is Columns.Todo or Columns.Done) Move(task, Columns.Doing, now);
        Save();
    }

    public void Pause(WorkTask task, DateTimeOffset now)
    {
        if (task.RunningSince is not { } since) return;
        task.SpentSeconds += Math.Max(0, (now - since).TotalSeconds);
        task.RunningSince = null;
        Save();
    }

    public void Delete(WorkTask task)
    {
        Tasks.Remove(task);
        Save();
    }

    public WorkTask? Active => Tasks.FirstOrDefault(task => task.RunningSince is not null)
        ?? Tasks.Where(task => task.Column == Columns.Doing).OrderBy(task => task.Order).FirstOrDefault();

    public (double Ratio, int Samples) Pace()
    {
        var ratios = Tasks
            .Where(task => task.Column == Columns.Done && task.Estimate > 0 && task.SpentSeconds >= 120)
            .OrderByDescending(task => task.Completed)
            .Take(12)
            .Select(task => task.SpentSeconds / 60 / task.Estimate)
            .Order()
            .ToArray();
        if (ratios.Length < 3) return (1, ratios.Length);
        var median = ratios.Length % 2 == 1 ? ratios[ratios.Length / 2] : (ratios[ratios.Length / 2 - 1] + ratios[ratios.Length / 2]) / 2;
        return (Math.Clamp(median, 0.5, 2.5), ratios.Length);
    }

    public IReadOnlyList<WorkTask> Plan(DateTimeOffset now) => Tasks
        .Where(task => task.Column is Columns.Doing or Columns.Todo)
        .Where(task => task.Column == Columns.Doing || task.Due is null || task.Due.Value.Date <= now.Date.AddDays(1))
        .OrderBy(task => task.RunningSince is null ? 1 : 0)
        .ThenBy(task => task.Column == Columns.Doing ? 0 : 1)
        .ThenBy(task => task.Due ?? DateTimeOffset.MaxValue)
        .ThenBy(task => task.Order)
        .ToArray();

    public static int Remaining(WorkTask task, DateTimeOffset now)
    {
        var steps = task.Steps.Where(step => !step.Done && step.Minutes > 0).Sum(step => step.Minutes);
        var left = task.Estimate - task.Spent(now) / 60;
        return (int)Math.Ceiling(Math.Max(steps > 0 && task.Steps.Any(step => step.Done) ? steps : left, 0));
    }

    public IReadOnlyList<Suggestion> Suggestions(DateTimeOffset now)
    {
        var list = new List<Suggestion>();
        var culture = CultureInfo.InvariantCulture;
        foreach (var task in Tasks.Where(task => task.RunningSince is not null))
        {
            var spent = task.Spent(now) / 60;
            if (task.Estimate > 0 && spent > task.Estimate * 1.1)
            {
                var extended = (int)(Math.Ceiling((spent + 10) / 5) * 5);
                list.Add(new($"overrun:{task.Id}:{task.Estimate}", "overrun", task.Id,
                    [task.Title, ((int)spent).ToString(culture), task.Estimate.ToString(culture), extended.ToString(culture)]));
            }
        }
        foreach (var task in Tasks.Where(task => task.Column == Columns.Todo && task.Due is { } due && due > now && due - now < TimeSpan.FromHours(4)))
            list.Add(new($"due:{task.Id}", "due", task.Id, [task.Title, task.Due!.Value.ToString("HH:mm", culture)]));
        foreach (var task in Tasks.Where(task => task.Column is Columns.Todo or Columns.Doing && task.Due is { } due && due < now))
            list.Add(new($"late:{task.Id}", "late", task.Id, [task.Title]));
        var (ratio, samples) = Pace();
        var open = Tasks.Where(task => task.Column == Columns.Todo && task.Estimate > 0).ToArray();
        if (samples >= 3 && Math.Abs(ratio - 1) >= 0.2 && open.Length > 0)
            list.Add(new($"pace:{Math.Round(ratio, 1).ToString(culture)}:{open.Length}", "pace", "",
                [Math.Round((ratio - 1) * 100).ToString(culture), open.Length.ToString(culture), Math.Round(ratio, 2).ToString(culture)]));
        var plan = Plan(now);
        var planned = plan.Sum(task => Remaining(task, now));
        var endOfDay = new DateTimeOffset(now.Year, now.Month, now.Day, 18, 0, 0, now.Offset);
        var available = Math.Max(0, (endOfDay - now).TotalMinutes);
        if (plan.Count > 1 && planned > available + 15)
        {
            var moved = plan.Where(task => task.Column == Columns.Todo && (task.Due is null || task.Due.Value.Date > now.Date))
                .OrderByDescending(task => task.Estimate).FirstOrDefault()
                ?? plan.Where(task => task.Column == Columns.Todo).LastOrDefault();
            if (moved is not null)
                list.Add(new($"overload:{moved.Id}:{now:yyyyMMdd}", "overload", moved.Id,
                    [moved.Title, ((int)(planned - available)).ToString(culture)]));
        }
        foreach (var task in Tasks.Where(task => task.Column is Columns.Todo or Columns.Doing && task.Estimate >= 60 && task.Steps.Count == 0).Take(2))
            list.Add(new($"split:{task.Id}", "split", task.Id, [task.Title, task.Estimate.ToString(culture)]));
        return list.Where(item => !Dismissed.Contains(item.Id)).ToArray();
    }

    public void Apply(Suggestion suggestion, DateTimeOffset now)
    {
        var task = Find(suggestion.TaskId);
        switch (suggestion.Kind)
        {
            case "overrun" when task is not null:
                task.Estimate = int.Parse(suggestion.Values[3], CultureInfo.InvariantCulture);
                break;
            case "due" when task is not null:
                Start(task, now);
                break;
            case "late" when task is not null:
                task.Due = new DateTimeOffset(now.Date.AddDays(1).AddHours(9), now.Offset);
                break;
            case "overload" when task is not null:
                task.Due = new DateTimeOffset(now.Date.AddDays(1).AddHours(9), now.Offset);
                break;
            case "pace":
                var ratio = double.Parse(suggestion.Values[2], CultureInfo.InvariantCulture);
                foreach (var item in Tasks.Where(item => item.Column == Columns.Todo && item.Estimate > 0))
                    item.Estimate = Math.Max(5, (int)(Math.Round(item.Estimate * ratio / 5) * 5));
                break;
        }
        Dismissed.Add(suggestion.Id);
        Save();
    }

    public static List<WorkStep> ParseSteps(string text)
    {
        var steps = new List<WorkStep>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Replace("**", "", StringComparison.Ordinal).Trim();
            if (line.Length < 3 || line.EndsWith(':')) continue;
            var timed = StepLine.Match(line);
            if (timed.Success)
            {
                var title = Regex.Replace(timed.Groups["title"].Value, @"^\s*(?:[-•*·–]|\d{1,2}\s*[.)])\s*", "").Trim(' ', '-', '—', '–', '|', ':', '(');
                if (title.Length >= 2)
                {
                    steps.Add(new WorkStep { Title = Trim(title), Minutes = Math.Clamp(int.Parse(timed.Groups["minutes"].Value, CultureInfo.InvariantCulture), 1, 480) });
                    continue;
                }
            }
            var plain = PlainStep.Match(line);
            if (plain.Success) steps.Add(new WorkStep { Title = Trim(plain.Groups["title"].Value.Trim()) });
        }
        return steps.Take(8).ToList();
    }

    private static string Trim(string value) => value.Length > 120 ? value[..120].TrimEnd() + "…" : value;
}
