using System.Text.Json;

namespace Nomi;

public static class RescueChoices
{
    public const string Keep = "keep";
    public const string Lighten = "lighten";
    public const string Defer = "defer";
    public static readonly string[] All = [Keep, Lighten, Defer];
}

public sealed class RescueLine(WorkTask task, string choice, int today, int light)
{
    public WorkTask Task { get; } = task;
    public string Choice { get; set; } = choice;
    public int Today { get; } = today;
    public int Light { get; } = light;
}

public sealed record RescueUndo(IReadOnlyList<string> Originals, IReadOnlyList<string> Created);

public static class DayRescue
{
    public const double Margin = 0.85;

    public static IReadOnlyList<RescueLine> Propose(IReadOnlyList<WorkTask> plan, DateTimeOffset now, Day day, double ratio)
    {
        var available = day.Available(now) * Margin;
        var used = 0.0;
        var lines = new List<RescueLine>();
        foreach (var task in plan)
        {
            var remaining = TaskBoard.Remaining(task, now);
            var light = Lighter(task, now);
            var pressing = task.RunningSince is not null || task.Due is { } due && due <= day.At(now, day.End);
            var fits = used + remaining * ratio <= available || pressing && used + remaining * ratio <= available / Margin;
            if (fits)
            {
                used += remaining * ratio;
                lines.Add(new(task, RescueChoices.Keep, remaining, light));
            }
            else if (light < remaining && used + light * ratio <= available)
            {
                used += light * ratio;
                lines.Add(new(task, RescueChoices.Lighten, remaining, light));
            }
            else lines.Add(new(task, RescueChoices.Defer, remaining, light));
        }
        return lines;
    }

    public static int Lighter(WorkTask task, DateTimeOffset now)
    {
        var remaining = TaskBoard.Remaining(task, now);
        var open = task.Steps.Where(step => !step.Done).ToArray();
        if (open.Length > 1 && open[0].Minutes > 0) return Math.Min(remaining, open[0].Minutes);
        return Math.Min(remaining, Math.Max(10, (int)Math.Round(remaining / 2.0 / 5) * 5));
    }

    public static RescueUndo Apply(TaskBoard board, IEnumerable<RescueLine> lines, DateTimeOffset now, Day day)
    {
        var originals = new List<string>();
        var created = new List<string>();
        var next = day.NextStart(now);
        foreach (var line in lines.Where(item => item.Choice != RescueChoices.Keep))
        {
            var task = line.Task;
            originals.Add(JsonSerializer.Serialize(task));
            if (line.Choice == RescueChoices.Defer)
            {
                board.Pause(task, now);
                if (task.Due is not { } due || due < next) task.Due = next;
                continue;
            }
            var open = task.Steps.Where(step => !step.Done).ToList();
            var rest = board.Add(task.Title, Columns.Todo, task.Space, "rescue");
            rest.Due = task.Due is { } later && later > next ? later : next;
            rest.Note = task.Note;
            if (open.Count > 1 && open[0].Minutes > 0)
            {
                rest.Steps = open.Skip(1).Select(step => new WorkStep { Title = step.Title, Minutes = step.Minutes }).ToList();
                task.Steps = task.Steps.Where(step => step.Done).Append(open[0]).ToList();
                rest.Estimate = Math.Max(5, rest.Steps.Sum(step => step.Minutes));
            }
            else rest.Estimate = Math.Max(5, line.Today - line.Light);
            task.Estimate = Math.Max(5, (int)Math.Ceiling(task.Spent(now) / 60) + line.Light);
            created.Add(rest.Id);
        }
        board.Save();
        return new(originals, created);
    }

    public static void Undo(TaskBoard board, RescueUndo undo)
    {
        board.Tasks.RemoveAll(task => undo.Created.Contains(task.Id));
        foreach (var json in undo.Originals)
        {
            if (JsonSerializer.Deserialize<WorkTask>(json) is not { } original) continue;
            var index = board.Tasks.FindIndex(task => task.Id == original.Id);
            if (index >= 0) board.Tasks[index] = original;
            else board.Tasks.Add(original);
        }
        board.Save();
    }
}
