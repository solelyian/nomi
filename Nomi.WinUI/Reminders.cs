namespace Nomi;

public static class ReminderLeads
{
    public const string Start = "start";
    public const string Quarter = "15";
    public const string Hour = "60";
    public static readonly string[] All = [Start, Quarter, Hour];
}

public sealed record ReminderNotice(WorkTask Task, string Kind, string Key, bool Discreet);

public static class Reminders
{
    public const string StartKind = "start";
    public const string LateKind = "late";
    public static readonly TimeSpan Margin = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan Imminent = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan SnoozeFor = TimeSpan.FromMinutes(15);

    public static string KeyFor(string kind, DateTimeOffset due) => kind + ":" + due.UtcTicks;

    public static DateTimeOffset Planned(WorkTask task, DateTimeOffset now, string lead)
    {
        var due = task.Due ?? now;
        var remaining = Math.Max(0, task.Estimate * 60 - task.Spent(now));
        return lead switch
        {
            ReminderLeads.Quarter => due.AddMinutes(-15),
            ReminderLeads.Hour => due.AddHours(-1),
            _ => due.AddSeconds(-remaining) - Margin
        };
    }

    public static DateTimeOffset Shift(DateTimeOffset at, Day day)
    {
        if (day.Workdays.Contains(at.DayOfWeek))
        {
            if (at < day.At(at, day.Start)) return day.At(at, day.Start);
            if (at <= day.At(at, day.End)) return at;
        }
        return day.NextStart(at);
    }

    public static DateTimeOffset When(WorkTask task, DateTimeOffset now, Day day, string lead)
    {
        var planned = Planned(task, now, lead);
        var shifted = Shift(planned, day);
        return shifted < task.Due ? shifted : planned;
    }

    public static ReminderNotice? Next(IEnumerable<WorkTask> tasks, DateTimeOffset now, Day day, string lead, bool busy)
    {
        foreach (var task in tasks.Where(task => task.Column != Columns.Done && task.Due is not null).OrderBy(task => task.Due))
        {
            if (task.Snoozed is DateTimeOffset snoozed && now < snoozed) continue;
            var due = task.Due!.Value;
            if (now < due)
            {
                var key = KeyFor(StartKind, due);
                if (task.Reminded.Contains(key) || now < When(task, now, day, lead)) continue;
                var imminent = due - now <= Imminent;
                if (busy && !imminent) continue;
                return new(task, StartKind, key, busy);
            }
            var late = KeyFor(LateKind, due);
            if (task.Reminded.Contains(late) || now - due > TimeSpan.FromDays(1) || busy || Shift(now, day) != now) continue;
            return new(task, LateKind, late, false);
        }
        return null;
    }

    public static void Mark(WorkTask task, string key)
    {
        var due = task.Due;
        task.Reminded.RemoveAll(item => due is null || (item != KeyFor(StartKind, due.Value) && item != KeyFor(LateKind, due.Value)));
        if (!task.Reminded.Contains(key)) task.Reminded.Add(key);
        task.Snoozed = null;
    }

    public static void Snooze(WorkTask task, DateTimeOffset now)
    {
        task.Snoozed = now + SnoozeFor;
        if (task.Due is DateTimeOffset due) task.Reminded.RemoveAll(item => item == KeyFor(StartKind, due) || item == KeyFor(LateKind, due));
    }
}
