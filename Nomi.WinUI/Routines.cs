using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Nomi;

public static class Triggers
{
    public const string DayStart = "day-start";
    public const string AfterLunch = "after-lunch";
    public const string TaskDone = "task-done";
    public const string FocusDone = "focus-done";
    public const string BreakReturn = "break-return";
    public const string DayEnd = "day-end";
    public const string AfterMeeting = "after-meeting";
    public static readonly string[] All = [DayStart, AfterLunch, TaskDone, FocusDone, BreakReturn, DayEnd, AfterMeeting];
}

public sealed class Routine
{
    public static readonly DayOfWeek[] WorkingDays = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Trigger { get; set; } = Triggers.DayStart;
    public List<DayOfWeek> Days { get; set; } = [.. WorkingDays];
    public List<WorkStep> Steps { get; set; } = [];
    public string? Document { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTimeOffset? LastRun { get; set; }
    public DateTimeOffset? Snoozed { get; set; }
    public DateTimeOffset Created { get; set; } = DateTimeOffset.Now;
    public int Minutes => Steps.Sum(step => step.Minutes) is > 0 and var minutes ? minutes : 15;
}

public sealed record RoutineMoment(
    DateTimeOffset Now,
    Day Day,
    string? Event = null,
    double IdleMinutes = 0,
    bool BackFromBreak = false,
    DateTimeOffset? FirstMeetingEnd = null);

public sealed class RoutineStore
{
    public const double ActiveIdle = 5;
    public string Path { get; }
    public List<Routine> Items { get; }

    public RoutineStore(string? file = null)
    {
        Path = file ?? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nyne", "Nomi", "routines.json");
        Items = Load(Path);
    }

    private static List<Routine> Load(string file)
    {
        try
        {
            if (SecureFile.Read(file) is not { } text) return [];
            var items = JsonSerializer.Deserialize<List<Routine>>(text) ?? [];
            return items.Where(item => item.Name.Length > 0 && Triggers.All.Contains(item.Trigger)).ToList();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }

    public void Save() => SecureFile.Write(Path, JsonSerializer.Serialize(Items));

    public IReadOnlyList<Routine> Ready(RoutineMoment moment) => Items.Where(item => Due(item, moment)).ToArray();

    public static bool Due(Routine routine, RoutineMoment moment)
    {
        var now = moment.Now;
        if (!routine.Enabled || !routine.Days.Contains(now.DayOfWeek)) return false;
        if (routine.LastRun is { } last && last.Date == now.Date) return false;
        var active = moment.IdleMinutes < ActiveIdle;
        if (routine.Snoozed is { } until && until.Date == now.Date) return until <= now && active;
        var time = now.TimeOfDay;
        var day = moment.Day;
        return routine.Trigger switch
        {
            Triggers.DayStart => time >= day.Start && time < day.LunchStart && active,
            Triggers.AfterLunch => time >= day.LunchEnd && time < day.End && active,
            Triggers.TaskDone => moment.Event == Triggers.TaskDone,
            Triggers.FocusDone => moment.Event == Triggers.FocusDone,
            Triggers.BreakReturn => moment.BackFromBreak && time >= day.Start && time < day.End,
            Triggers.DayEnd => time >= day.End - TimeSpan.FromMinutes(30) && time < day.End + TimeSpan.FromHours(1) && active,
            Triggers.AfterMeeting => moment.FirstMeetingEnd is { } end && now >= end && now - end < TimeSpan.FromHours(3) && active,
            _ => false
        };
    }

    public void Ran(Routine routine, DateTimeOffset now)
    {
        routine.LastRun = now;
        routine.Snoozed = null;
        Save();
    }

    public void Snooze(Routine routine, DateTimeOffset now, int minutes = 30)
    {
        routine.Snoozed = now.AddMinutes(minutes);
        Save();
    }

    public static Routine? Suggest(IEnumerable<WorkTask> tasks, IEnumerable<Routine> existing, DateTimeOffset now)
    {
        var names = existing.Select(item => Key(item.Name)).ToHashSet();
        var done = tasks.Where(task => task.Completed is { } completed && completed > now.AddDays(-35)).ToArray();
        foreach (var group in done.GroupBy(task => (Name: Key(task.Title), task.Completed!.Value.DayOfWeek)))
        {
            var weeks = group.Select(task => ISOWeek.GetWeekOfYear(task.Completed!.Value.DateTime)).Distinct().Count();
            if (weeks < 3 || group.Key.Name.Length < 3 || names.Contains(group.Key.Name)) continue;
            var sample = group.OrderByDescending(task => task.Completed).First();
            var routine = new Routine
            {
                Name = sample.Title,
                Days = [group.Key.DayOfWeek],
                Steps = sample.Steps.Select(step => new WorkStep { Title = step.Title, Minutes = step.Minutes }).ToList()
            };
            if (routine.Steps.Count == 0) routine.Steps.Add(new WorkStep { Title = sample.Title, Minutes = Math.Max(5, sample.Estimate) });
            return routine;
        }
        return null;
    }

    public static string Key(string value) => Regex.Replace(value.ToLowerInvariant(), @"[^\p{L}\p{N}]+", " ").Trim();
}

public sealed record PhraseResult(Routine Routine, bool HasTrigger, bool HasName)
{
    public bool Understood => HasTrigger && HasName;
}

public static class RoutinePhrase
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    private static readonly (string Trigger, Regex Pattern)[] Patterns =
    [
        (Triggers.AfterMeeting, new(@"\b(?:apr[eè]s|à la fin de|after)\s+(?:(?:ma|la|une|chaque|my|the|a|each)\s+)?(?:premi[eè]re\s+|first\s+)?(?:r[ée]union|meeting)s?(?:\s+(?:du matin|de la journ[ée]e|of the day))?", Options)),
        (Triggers.AfterLunch, new(@"(?:\bapr[eè]s\s+(?:le\s+|mon\s+|la\s+pause\s+)?d[ée]jeuner|\bafter\s+(?:my\s+)?lunch|\bl['’]apr[eè]s-midi|\b(?:in\s+the\s+)?afternoon)\b", Options)),
        (Triggers.TaskDone, new(@"\b(?:(?:quand|lorsque|d[eè]s que)\s+(?:je\s+termine|je\s+finis|j['’]ai\s+(?:fini|termin[ée]))\s+(?:une|chaque|ma)\s+t[aâ]che|apr[eè]s\s+(?:chaque|une|ma)\s+t[aâ]che|when\s+I\s+(?:finish|complete|have\s+finished)\s+(?:a|each|my)\s+task|after\s+(?:each|a|every|my)\s+task)\b", Options)),
        (Triggers.FocusDone, new(@"\b(?:apr[eè]s|after)\s+(?:(?:une|ma|chaque|a|my|each)\s+)?(?:session\s+)?focus(?:\s+session)?\b", Options)),
        (Triggers.BreakReturn, new(@"\b(?:(?:au\s+)?retour\s+(?:de|d['’]une)\s+pause|quand\s+je\s+reviens(?:\s+(?:de|d['’]une)\s+pause)?|apr[eè]s\s+(?:une|ma|chaque)\s+pause|(?:when\s+I\s+)?(?:come\s+|get\s+)?back\s+from\s+(?:a\s+|my\s+)?break|after\s+(?:a|my|each)\s+break)\b", Options)),
        (Triggers.DayEnd, new(@"(?:\b(?:en|à\s+la)\s+fin\s+de\s+(?:ma\s+|la\s+)?journ[ée]e|\bavant\s+de\s+partir|\ble\s+soir|\b(?:at\s+the\s+)?end\s+of\s+(?:my\s+|the\s+)?day|\bbefore\s+(?:I\s+)?leav(?:e|ing)|\bin\s+the\s+evening)\b", Options)),
        (Triggers.DayStart, new(@"(?:\b(?:au\s+)?d[ée]but\s+de\s+(?:ma\s+|la\s+)?journ[ée]e|\ben\s+arrivant|\b(?:chaque|tous\s+les|le)\s+matins?|\b(?:at\s+the\s+)?start\s+of\s+(?:my\s+|the\s+)?day|\bwhen\s+I\s+(?:arrive|start)|\b(?:every|in\s+the)\s+morning)\b", Options))
    ];

    private static readonly string[] DayNames = ["dimanche|sunday", "lundi|monday", "mardi|tuesday", "mercredi|wednesday", "jeudi|thursday", "vendredi|friday", "samedi|saturday"];
    private static readonly Regex DayWord = new(@"\b(?:(?:chaque|tous\s+les|le|les|every|on|each)\s+)?(?<day>lundi|mardi|mercredi|jeudi|vendredi|samedi|dimanche|monday|tuesday|wednesday|thursday|friday|saturday|sunday)s?\b(?:\s*(?:,|et|and)\s*)?", Options);
    private static readonly Regex EveryDay = new(@"\b(?:chaque\s+jour|tous\s+les\s+jours|en\s+semaine|every\s*day|each\s+day|daily|on\s+weekdays|weekdays)\b", Options);
    private static readonly Regex Lead = new(@"^(?:nomi\s*[,:]\s*|rappelle[- ]moi\s+(?:de\s+|d['’])?|remind\s+me\s+to\s+|(?:et|and|puis|then)\s+|[,:\-–—]\s*)+", Options);
    private static readonly Regex Separator = new(@"\s*(?:,\s*puis\s+|;\s*|\s+puis\s+|,\s*then\s+|\s+then\s+|,\s*et\s+ensuite\s+)\s*", Options);
    private static readonly Regex StepMinutes = new(@"\s*[(\-–—:]?\s*(?<minutes>\d{1,3})\s*(?:min(?:utes?)?|mn)\b\)?", Options);

    public static PhraseResult Parse(string text)
    {
        var routine = new Routine();
        var rest = " " + text.Trim() + " ";
        var hasTrigger = false;
        foreach (var (trigger, pattern) in Patterns)
        {
            var match = pattern.Match(rest);
            if (!match.Success) continue;
            routine.Trigger = trigger;
            rest = rest.Remove(match.Index, match.Length).Insert(match.Index, " ");
            hasTrigger = true;
            break;
        }
        var days = new HashSet<DayOfWeek>();
        rest = DayWord.Replace(rest, match =>
        {
            var name = match.Groups["day"].Value;
            for (var index = 0; index < DayNames.Length; index++)
                if (Regex.IsMatch(name, $"^(?:{DayNames[index]})$", Options)) days.Add((DayOfWeek)index);
            return " ";
        });
        rest = EveryDay.Replace(rest, " ");
        if (days.Count > 0) routine.Days = [.. days.OrderBy(day => ((int)day + 6) % 7)];
        rest = Lead.Replace(Regex.Replace(rest, @"\s+", " ").Trim(), "").Trim(' ', ',', '.', ':', ';', '-', '–', '—');
        foreach (var part in Separator.Split(rest))
        {
            var clean = Lead.Replace(part.Trim(), "").Trim(' ', ',', '.', ':', ';');
            var minutes = StepMinutes.Match(clean);
            var title = minutes.Success ? clean.Remove(minutes.Index, minutes.Length).Trim(' ', ',', '.', '-') : clean;
            if (title.Length < 2) continue;
            var value = minutes.Success ? Math.Clamp(int.Parse(minutes.Groups["minutes"].Value, CultureInfo.InvariantCulture), 1, 240) : 10;
            routine.Steps.Add(new WorkStep { Title = Capital(title), Minutes = value });
        }
        routine.Steps = routine.Steps.Take(8).ToList();
        routine.Name = routine.Steps.FirstOrDefault()?.Title ?? "";
        if (routine.Name.Length > 80) routine.Name = routine.Name[..80].TrimEnd() + "…";
        return new(routine, hasTrigger, routine.Name.Length > 0);
    }

    private static string Capital(string value) =>
        value.Length == 0 ? value : char.ToUpper(value[0], CultureInfo.CurrentCulture) + value[1..];
}
