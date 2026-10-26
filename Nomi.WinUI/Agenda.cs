using System.Globalization;
using System.Text.RegularExpressions;

namespace Nomi;

public sealed record Meeting(DateTimeOffset Start, DateTimeOffset End, string Title);

public static class Agenda
{
    private sealed class Event
    {
        public string Uid = "";
        public string Title = "";
        public DateTimeOffset? Start;
        public DateTimeOffset? End;
        public TimeSpan? Duration;
        public bool AllDay;
        public bool Cancelled;
        public string? Rule;
        public DateTimeOffset? RecurrenceId;
        public HashSet<DateTime> Excluded = [];
    }

    public static IReadOnlyList<Meeting> On(string ics, DateTime date, TimeZoneInfo? zone = null)
    {
        zone ??= TimeZoneInfo.Local;
        var events = Parse(ics, zone);
        var moved = events.Where(item => item.RecurrenceId is not null)
            .GroupBy(item => item.Uid)
            .ToDictionary(group => group.Key, group => group.Select(item => item.RecurrenceId!.Value.Date).ToHashSet());
        var list = new List<Meeting>();
        foreach (var item in events)
        {
            if (item.Cancelled || item.AllDay || item.Start is not { } start) continue;
            var length = item.End is { } end ? end - start : item.Duration ?? TimeSpan.FromMinutes(30);
            if (length <= TimeSpan.Zero || length > TimeSpan.FromHours(12)) continue;
            var occurs = item.Rule is null || item.RecurrenceId is not null
                ? start.Date == date.Date
                : !item.Excluded.Contains(date.Date) && !(moved.TryGetValue(item.Uid, out var skip) && skip.Contains(date.Date)) && Recurs(item.Rule, start, date.Date);
            if (!occurs) continue;
            var at = new DateTimeOffset(date.Date.Add(start.TimeOfDay), zone.GetUtcOffset(date.Date.Add(start.TimeOfDay)));
            list.Add(new(at, at + length, item.Title));
        }
        return list.OrderBy(item => item.Start).ToArray();
    }

    public static DateTimeOffset? FirstMeetingEnd(IEnumerable<Meeting> meetings, Day day) =>
        meetings.Where(item => item.Start.TimeOfDay >= day.Start - TimeSpan.FromHours(1) && item.Start.TimeOfDay < day.End)
            .OrderBy(item => item.Start).FirstOrDefault()?.End;

    private static List<Event> Parse(string ics, TimeZoneInfo zone)
    {
        var lines = Regex.Replace(ics, @"\r?\n[ \t]", "").Split('\n');
        var events = new List<Event>();
        Event? current = null;
        foreach (var raw in lines)
        {
            var line = raw.TrimEnd('\r');
            if (line == "BEGIN:VEVENT") { current = new(); continue; }
            if (line == "END:VEVENT") { if (current is not null) events.Add(current); current = null; continue; }
            if (current is null) continue;
            var colon = line.IndexOf(':');
            if (colon <= 0) continue;
            var head = line[..colon].Split(';');
            var name = head[0].ToUpperInvariant();
            var value = line[(colon + 1)..];
            var tz = head.Skip(1).FirstOrDefault(part => part.StartsWith("TZID=", StringComparison.OrdinalIgnoreCase))?[5..];
            switch (name)
            {
                case "UID": current.Uid = value; break;
                case "SUMMARY": current.Title = value.Replace(@"\,", ",").Replace(@"\;", ";").Replace(@"\n", " "); break;
                case "STATUS": current.Cancelled = value.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase); break;
                case "RRULE": current.Rule = value.ToUpperInvariant(); break;
                case "DTSTART":
                    current.AllDay = value.Length == 8;
                    current.Start = Time(value, tz, zone);
                    break;
                case "DTEND": current.End = Time(value, tz, zone); break;
                case "DURATION": current.Duration = Duration(value); break;
                case "RECURRENCE-ID": current.RecurrenceId = Time(value, tz, zone); break;
                case "EXDATE":
                    foreach (var part in value.Split(','))
                        if (Time(part, tz, zone) is { } excluded) current.Excluded.Add(excluded.Date);
                    break;
            }
        }
        return events;
    }

    private static DateTimeOffset? Time(string value, string? tz, TimeZoneInfo zone)
    {
        value = value.Trim();
        if (DateTime.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
            return new DateTimeOffset(day, zone.GetUtcOffset(day));
        var utc = value.EndsWith('Z');
        if (!DateTime.TryParseExact(value.TrimEnd('Z'), "yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)) return null;
        if (utc) return TimeZoneInfo.ConvertTime(new DateTimeOffset(time, TimeSpan.Zero), zone);
        var source = zone;
        if (tz is not null)
        {
            try { source = TimeZoneInfo.FindSystemTimeZoneById(tz.Trim('"')); }
            catch (Exception error) when (error is TimeZoneNotFoundException or InvalidTimeZoneException) { }
        }
        var instant = new DateTimeOffset(time, source.GetUtcOffset(time));
        return TimeZoneInfo.ConvertTime(instant, zone);
    }

    private static TimeSpan? Duration(string value)
    {
        var match = Regex.Match(value, @"^P(?:(?<d>\d+)D)?(?:T(?:(?<h>\d+)H)?(?:(?<m>\d+)M)?)?$");
        if (!match.Success) return null;
        int Part(string key) => match.Groups[key].Success ? int.Parse(match.Groups[key].Value, CultureInfo.InvariantCulture) : 0;
        return new TimeSpan(Part("d"), Part("h"), Part("m"), 0);
    }

    private static bool Recurs(string rule, DateTimeOffset start, DateTime date)
    {
        if (date < start.Date) return false;
        var parts = rule.Split(';').Select(part => part.Split('=', 2)).Where(part => part.Length == 2).ToDictionary(part => part[0], part => part[1]);
        var frequency = parts.GetValueOrDefault("FREQ", "");
        var interval = int.TryParse(parts.GetValueOrDefault("INTERVAL"), out var every) && every > 0 ? every : 1;
        if (parts.TryGetValue("UNTIL", out var untilText) && Time(untilText, null, TimeZoneInfo.Local) is { } until && date > until.Date) return false;
        var byDay = parts.TryGetValue("BYDAY", out var days)
            ? days.Split(',').Select(code => Regex.Replace(code, @"^[+-]?\d+", "")).Select(code => Array.IndexOf(["SU", "MO", "TU", "WE", "TH", "FR", "SA"], code)).Where(index => index >= 0).Select(index => (DayOfWeek)index).ToHashSet()
            : null;
        bool Matches(DateTime day) => frequency switch
        {
            "DAILY" => (day - start.Date).Days % interval == 0 && (byDay is null || byDay.Contains(day.DayOfWeek)),
            "WEEKLY" => Weeks(start.Date, day) % interval == 0 && (byDay ?? [start.DayOfWeek]).Contains(day.DayOfWeek),
            "MONTHLY" => ((day.Year - start.Year) * 12 + day.Month - start.Month) % interval == 0 && day.Day == start.Day,
            "YEARLY" => (day.Year - start.Year) % interval == 0 && day.Month == start.Month && day.Day == start.Day,
            _ => false
        };
        if (!Matches(date)) return false;
        if (!parts.TryGetValue("COUNT", out var countText) || !int.TryParse(countText, out var count)) return true;
        var seen = 0;
        for (var day = start.Date; day <= date; day = day.AddDays(1))
            if (Matches(day) && ++seen > count) return false;
        return true;
    }

    private static int Weeks(DateTime from, DateTime to)
    {
        static DateTime Monday(DateTime day) => day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
        return (Monday(to) - Monday(from)).Days / 7;
    }
}
