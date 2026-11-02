using System.Globalization;
using System.Text.RegularExpressions;

namespace Nomi;

public static class PhraseKinds
{
    public const string Date = "date";
    public const string Time = "time";
    public const string Duration = "duration";
    public const string Space = "space";
    public const string Recurrence = "recurrence";
}

public sealed record PhrasePart(string Kind, string Source);

public sealed record PhraseSpace(string Id, string Title);

public sealed record TaskPhraseResult(
    string Title, DateTimeOffset? Due, int? Minutes, string? Space,
    IReadOnlyList<PhrasePart> Parts, bool Recurring, bool Unclear, bool InvalidDate, bool FromModel = false)
{
    public bool Understood => Due is not null || Minutes is not null || Space is not null;
}

public static class TaskPhrase
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private const string Apostrophe = "['’]";
    private const string Weekday = "lundi|mardi|mercredi|jeudi|vendredi|samedi|dimanche|monday|tuesday|wednesday|thursday|friday|saturday|sunday";
    private const string Month = "janvier|f[ée]vrier|mars|avril|mai|juin|juillet|ao[uû]t|septembre|octobre|novembre|d[ée]cembre|january|february|march|april|may|june|july|august|september|october|november|december";
    private const string Count = @"\d{1,3}|une?|deux|trois|quatre|cinq|six|sept|huit|neuf|dix|quinze|an?|one|two|three|four|five|six|seven|eight|nine|ten|fifteen";
    private const string Lead = @"(?:(?:à|a|at|vers|around|avant|before|pour|by|d['’]ici|until|jusqu['’]à)\s+)?";

    private static readonly Regex RecurrenceWord = new($@"\b(?:chaque|tous\s+les|toutes\s+les|every|each)\s+(?:jours?|semaines?|mois|matins?|soirs?|days?|weeks?|months?|mornings?|evenings?|(?:{Weekday})s?)\b|\b(?:daily|weekly|monthly|quotidiennement|hebdomadaire|chaque\s+jour)\b", Options);
    private static readonly Regex SpaceTag = new(@"(?<![\p{L}\p{N}])#(?<name>[\p{L}\p{N}_\-]{2,40})", Options);
    private static readonly Regex InTime = new($@"\b(?:dans|in)\s+(?<n>{Count})\s+(?<u>heures?|hours?|minutes?|mins?)\b|\b(?:dans|in)\s+(?<half>une\s+demi-?\s?heure|half\s+an\s+hour)\b", Options);
    private static readonly Regex ClockMinutes = new($@"(?<lead>\b{Lead})(?<!\d)(?<h>\d{{1,2}})\s*(?<sep>[:h])\s*(?<m>\d{{2}})\b(?!\s*(?:min|mn))", Options);
    private static readonly Regex ClockMeridiem = new($@"\b{Lead}(?<h>\d{{1,2}})(?::(?<m>\d{{2}}))?\s*(?<ap>am|pm)\b", Options);
    private static readonly Regex ClockLed = new(@"\b(?:à|a|at|vers|around|avant|before|pour|by|d['’]ici|jusqu['’]à)\s+(?<h>\d{1,2})\s*(?:h|heures?)\b(?!\s*\d)", Options);
    private static readonly Regex ClockBare = new(@"(?<![\p{L}\p{N}])(?<h>\d{1,2})\s*h\b(?!\s*\d)", Options);

    private static readonly (Regex Pattern, Func<Day, TimeSpan> Time)[] Moments =
    [
        (new(@"\bavant\s+(?:le\s+|mon\s+)?d[ée]jeuner\b|\bavant\s+midi\b|\bbefore\s+(?:my\s+)?(?:lunch|noon)\b", Options), day => day.LunchStart - TimeSpan.FromMinutes(15)),
        (new(@"\bapr[eè]s\s+(?:le\s+|mon\s+)?d[ée]jeuner\b|\bafter\s+(?:my\s+)?lunch\b", Options), day => day.LunchEnd + TimeSpan.FromMinutes(15)),
        (new($@"\b(?:cet\s+|dans\s+l{Apostrophe}\s*|en\s+|l{Apostrophe}\s*)?apr[eè]s-\s?midi\b|\b(?:this\s+|in\s+the\s+)?afternoon\b", Options), day => day.LunchEnd + (day.End - day.LunchEnd) / 2),
        (new(@"\b(?:en|à\s+la)\s+fin\s+de\s+(?:la\s+|ma\s+)?journ[ée]e\b|\bfin\s+de\s+journ[ée]e\b|\bce\s+soir\b|\b(?:at\s+the\s+)?end\s+of\s+(?:the\s+|my\s+)?day\b|\btonight\b|\b(?:this|in\s+the)\s+evening\b", Options), day => day.End),
        (new(@"\b(?:en|au)\s+d[ée]but\s+de\s+(?:la\s+|ma\s+)?journ[ée]e\b|\b(?:at\s+the\s+)?start\s+of\s+(?:the\s+|my\s+)?day\b|\bfirst\s+thing\b", Options), day => day.Start + TimeSpan.FromMinutes(30)),
        (new(@"\b(?:ce\s+|le\s+|du\s+)?matin\b|\bdans\s+la\s+matin[ée]e\b|\b(?:this\s+|in\s+the\s+)?morning\b", Options), day => day.LunchStart - TimeSpan.FromMinutes(30)),
        (new(@"\b(?:à\s+)?midi\b|\b(?:at\s+)?noon\b", Options), _ => new TimeSpan(12, 0, 0))
    ];

    private static readonly Regex DueLead = new(@"(?:\b(?:avant|pour|d['’]ici|by|before|due|until|jusqu['’]à)\s+)?", Options);
    private static readonly Regex Today = new(@"\baujourd['’]hui\b|\btoday\b", Options);
    private static readonly Regex AfterTomorrow = new(@"\bapr[eè]s-demain\b|\b(?:the\s+)?day\s+after\s+tomorrow\b", Options);
    private static readonly Regex Tomorrow = new(@"\bdemain\b|\btomorrow\b", Options);
    private static readonly Regex Named = new($@"\b(?:(?:le|ce|on|next|this)\s+)?(?<wd>{Weekday})(?:\s+(?:prochain|next))?\b", Options);
    private static readonly Regex InDays = new($@"\b(?:dans|in)\s+(?<n>{Count})\s+(?<u>jours?|semaines?|days?|weeks?)\b", Options);
    private static readonly Regex NextWeek = new(@"\b(?:la\s+)?semaine\s+prochaine\b|\bnext\s+week\b", Options);
    private static readonly Regex DayMonth = new($@"\b(?:le\s+|on\s+(?:the\s+)?)?(?<d>\d{{1,2}})(?:er|st|nd|rd|th)?\s+(?<mon>{Month})(?:\s+(?<y>\d{{4}}))?\b", Options);
    private static readonly Regex MonthDay = new($@"\b(?:on\s+)?(?<mon>{Month})\s+(?<d>\d{{1,2}})(?:st|nd|rd|th)?(?:,?\s+(?<y>\d{{4}}))?\b", Options);
    private static readonly Regex Numeric = new(@"(?<![\d/])(?<a>\d{1,2})/(?<b>\d{1,2})(?:/(?<y>\d{2}|\d{4}))?(?![\d/])", Options);
    private static readonly Regex MonthDayOnly = new(@"\b(?:le\s+(?<d>\d{1,2})(?:er)?|on\s+the\s+(?<d>\d{1,2})(?:st|nd|rd|th))\b(?!\s*(?:h\b|:|min|heures?))", Options);

    private static readonly (Regex Pattern, int Minutes)[] DurationWords =
    [
        (new(@"\b(?:une\s+heure\s+et\s+demie|an\s+hour\s+and\s+a\s+half|one\s+and\s+a\s+half\s+hours?)\b", Options), 90),
        (new(@"\b(?:trois\s+quarts\s+d['’]heure|three\s+quarters\s+of\s+an\s+hour)\b", Options), 45),
        (new(@"\b(?:une\s+demi-?\s?heure|half\s+an\s+hour)\b", Options), 30),
        (new(@"\b(?:un\s+quart\s+d['’]heure|a\s+quarter\s+of\s+an\s+hour|a\s+quarter\s+hour)\b", Options), 15),
        (new(@"\b(?:deux\s+heures|two\s+hours)\b", Options), 120),
        (new(@"\b(?:une\s+heure|an\s+hour|one\s+hour)\b", Options), 60)
    ];
    private static readonly Regex DurationLead = new(@"(?:\b(?:pendant|durant|en|for|environ|about|takes?)\s+)?", Options);
    private static readonly Regex DurationMinutes = new(@"(?<![\p{L}\p{N}])(?<m>\d{1,3})\s*(?:minutes?|min|mn)\b", Options);
    private static readonly Regex DurationHours = new(@"(?<![\p{L}\p{N}])(?<h>\d{1,2})\s*(?:h|heures?|hours?|hrs?)\s*(?<m>\d{2})?\b(?!\s*(?:min|mn))", Options);

    private static readonly Regex Temporal = new(@"\b(?:jours?|semaines?|mois|ann[ée]es?|matin[ée]?e?|soir[ée]?e?|nuit|heures?|minutes?|quinzaine|prochaine?s?|derni[eè]re?s?|fin|d[ée]but|midi|minuit|week-?end|bient[oô]t|tard|t[oô]t|days?|weeks?|months?|years?|morning|evening|night|hours?|noon|midnight|tonight|next|last|end|soon|later|early|asap)\b|\d+\s*(?:j|h|min|sem)\b", Options);
    private static readonly Regex Dangling = new(@"(?:^|\s)(?:à|a|at|le|la|les|on|the|pour|by|avant|before|vers|around|et|and|de|du|d['’]|en|in|dans|ce|cet|this|pendant|for|durant|environ|about|d['’]ici)$", Options);
    private static readonly Regex LeadingConnector = new(@"^(?:et|and|puis|then|nomi\s*[,:])\s+", Options);

    public static TaskPhraseResult Parse(string text, DateTimeOffset now, Day day, IReadOnlyList<PhraseSpace> spaces,
        string language, IReadOnlySet<string>? ignored = null)
    {
        ignored ??= new HashSet<string>();
        var rest = " " + text.Trim() + " ";
        var parts = new List<PhrasePart>();
        bool Take(string kind, Match match)
        {
            parts.Add(new(kind, match.Value.Trim(' ', ',', ';', '.')));
            rest = rest.Remove(match.Index, match.Length).Insert(match.Index, " ");
            return true;
        }
        Match? First(Regex pattern)
        {
            var match = pattern.Match(rest);
            return match.Success ? match : null;
        }

        var recurring = false;
        if (!ignored.Contains(PhraseKinds.Recurrence) && First(RecurrenceWord) is Match repeat)
            recurring = Take(PhraseKinds.Recurrence, repeat);

        string? space = null;
        if (!ignored.Contains(PhraseKinds.Space))
            foreach (Match tag in SpaceTag.Matches(rest))
            {
                var key = Key(tag.Groups["name"].Value);
                var found = spaces.FirstOrDefault(item => Key(item.Title) == key || Key(item.Id) == key)
                    ?? spaces.FirstOrDefault(item => Key(item.Title).StartsWith(key, StringComparison.Ordinal) || Key(item.Id).StartsWith(key, StringComparison.Ordinal));
                if (found is null) continue;
                space = found.Id;
                Take(PhraseKinds.Space, tag);
                break;
            }

        DateTimeOffset? absolute = null;
        TimeSpan? time = null;
        if (!ignored.Contains(PhraseKinds.Time))
        {
            if (First(InTime) is Match inTime)
            {
                var minutes = inTime.Groups["half"].Success ? 30
                    : Number(inTime.Groups["n"].Value) * (inTime.Groups["u"].Value.StartsWith('h') ? 60 : 1);
                if (minutes is > 0 and <= 24 * 60)
                {
                    absolute = now.AddMinutes(minutes);
                    Take(PhraseKinds.Time, inTime);
                }
            }
            if (absolute is null) time = ReadClock(ref rest, parts, day);
        }

        DateTime? date = null;
        var invalid = false;
        if (!ignored.Contains(PhraseKinds.Date) && absolute is null && !recurring)
        {
            var (found, match, bad) = ReadDate(rest, now, day, language);
            if (match is not null)
            {
                var prefix = DueLead.Match(rest[..match.Index]);
                var index = prefix.Success ? prefix.Index : match.Index;
                var whole = rest.Substring(index, match.Index + match.Length - index);
                parts.Add(new(PhraseKinds.Date, whole.Trim(' ', ',', ';', '.')));
                rest = rest.Remove(index, whole.Length).Insert(index, " ");
                date = found;
                invalid = bad;
            }
        }

        int? duration = null;
        if (!ignored.Contains(PhraseKinds.Duration))
        {
            foreach (var (pattern, minutes) in DurationWords)
            {
                if (First(WithLead(pattern)) is not Match word) continue;
                duration = minutes;
                Take(PhraseKinds.Duration, word);
                break;
            }
            if (duration is null && First(WithLead(DurationHours)) is Match hours)
            {
                var value = Number(hours.Groups["h"].Value) * 60 + (hours.Groups["m"].Success ? Number(hours.Groups["m"].Value) : 0);
                if (value is > 0 and <= 600) { duration = value; Take(PhraseKinds.Duration, hours); }
            }
            if (duration is null && First(WithLead(DurationMinutes)) is Match minutesMatch)
            {
                var value = Number(minutesMatch.Groups["m"].Value);
                if (value is > 0 and <= 600) { duration = value; Take(PhraseKinds.Duration, minutesMatch); }
            }
        }

        DateTimeOffset? due = absolute;
        if (due is null && !invalid && (date is not null || time is not null))
        {
            var at = time ?? day.End;
            due = new DateTimeOffset((date ?? now.Date).Add(at), now.Offset);
            if (date is null && due <= now) due = new DateTimeOffset(day.NextStart(now).Date.Add(at), now.Offset);
            else if (date is not null && due < now.AddMinutes(-1)) { invalid = true; due = null; }
        }

        var title = Clean(rest);
        if (title.Length == 0) title = Clean(text);
        if (title.Length > 140) title = title[..140].TrimEnd();
        return new(title, due, duration, space, parts, recurring, Temporal.IsMatch(title), invalid);
    }

    private static Match? Hit(Regex pattern, string text)
    {
        var match = pattern.Match(text);
        return match.Success ? match : null;
    }

    private static Regex WithLead(Regex pattern) => new(DurationLead + "(?:" + pattern + ")", Options);

    private static TimeSpan? ReadClock(ref string rest, List<PhrasePart> parts, Day day)
    {
        foreach (var (pattern, value) in Moments)
        {
            if (Hit(pattern, rest) is not Match moment) continue;
            Cut(ref rest, parts, moment);
            return value(day);
        }
        foreach (Match match in ClockMinutes.Matches(rest))
        {
            int hour = Number(match.Groups["h"].Value), minute = Number(match.Groups["m"].Value);
            var led = match.Groups["lead"].Value.Trim().Length > 0;
            if (hour > 23 || minute > 59) continue;
            if (!led && match.Groups["sep"].Value.Equals("h", StringComparison.OrdinalIgnoreCase) && hour < 7) continue;
            Cut(ref rest, parts, match);
            return new TimeSpan(hour, minute, 0);
        }
        if (Hit(ClockMeridiem, rest) is Match meridiem)
        {
            var hour = Number(meridiem.Groups["h"].Value);
            var minute = meridiem.Groups["m"].Success ? Number(meridiem.Groups["m"].Value) : 0;
            if (hour is >= 1 and <= 12 && minute <= 59)
            {
                var pm = meridiem.Groups["ap"].Value.Equals("pm", StringComparison.OrdinalIgnoreCase);
                Cut(ref rest, parts, meridiem);
                return new TimeSpan(hour % 12 + (pm ? 12 : 0), minute, 0);
            }
        }
        if (Hit(ClockLed, rest) is Match led2 && Number(led2.Groups["h"].Value) <= 23)
        {
            Cut(ref rest, parts, led2);
            return new TimeSpan(Number(led2.Groups["h"].Value), 0, 0);
        }
        if (Hit(ClockBare, rest) is Match bare && Number(bare.Groups["h"].Value) is >= 7 and <= 23)
        {
            Cut(ref rest, parts, bare);
            return new TimeSpan(Number(bare.Groups["h"].Value), 0, 0);
        }
        return null;
    }

    private static void Cut(ref string rest, List<PhrasePart> parts, Match match)
    {
        parts.Add(new(PhraseKinds.Time, match.Value.Trim(' ', ',', ';', '.')));
        rest = rest.Remove(match.Index, match.Length).Insert(match.Index, " ");
    }

    private static (DateTime? Date, Match? Match, bool Invalid) ReadDate(string rest, DateTimeOffset now, Day day, string language)
    {
        var today = now.Date;
        if (Hit(AfterTomorrow, rest) is Match afterTomorrow) return (today.AddDays(2), afterTomorrow, false);
        if (Hit(Tomorrow, rest) is Match tomorrow) return (today.AddDays(1), tomorrow, false);
        if (Hit(Today, rest) is Match todayMatch) return (today, todayMatch, false);
        if (Hit(InDays, rest) is Match inDays && Number(inDays.Groups["n"].Value) is > 0 and <= 366)
            return (today.AddDays(Number(inDays.Groups["n"].Value) * (Regex.IsMatch(inDays.Groups["u"].Value, "^(?:sem|week)", Options) ? 7 : 1)), inDays, false);
        if (Hit(NextWeek, rest) is Match nextWeek)
        {
            var monday = today.AddDays(7 - ((int)today.DayOfWeek + 6) % 7);
            for (var i = 0; i < 7 && !day.Workdays.Contains(monday.DayOfWeek); i++) monday = monday.AddDays(1);
            return (monday, nextWeek, false);
        }
        if (Hit(Named, rest) is Match named)
        {
            var ahead = ((int)WeekdayOf(named.Groups["wd"].Value) - (int)today.DayOfWeek + 7) % 7;
            return (today.AddDays(ahead == 0 ? 7 : ahead), named, false);
        }
        foreach (var pattern in new[] { DayMonth, MonthDay })
        {
            if (Hit(pattern, rest) is not Match match) continue;
            var year = match.Groups["y"].Success ? Number(match.Groups["y"].Value) : today.Year;
            return Calendar(year, MonthOf(match.Groups["mon"].Value), Number(match.Groups["d"].Value), !match.Groups["y"].Success, today, match);
        }
        if (Hit(Numeric, rest) is Match numeric)
        {
            int a = Number(numeric.Groups["a"].Value), b = Number(numeric.Groups["b"].Value);
            var (dayOfMonth, month) = language == "en" ? (b, a) : (a, b);
            var year = numeric.Groups["y"].Success ? Number(numeric.Groups["y"].Value) : today.Year;
            if (year < 100) year += 2000;
            return Calendar(year, month, dayOfMonth, !numeric.Groups["y"].Success, today, numeric);
        }
        if (Hit(MonthDayOnly, rest) is Match only)
        {
            var dayOfMonth = Number(only.Groups["d"].Value);
            for (var offset = 0; offset < 12 && dayOfMonth is >= 1 and <= 31; offset++)
            {
                var month = today.AddMonths(offset);
                if (dayOfMonth > DateTime.DaysInMonth(month.Year, month.Month)) continue;
                var candidate = new DateTime(month.Year, month.Month, dayOfMonth);
                if (candidate >= today) return (candidate, only, false);
            }
            return (null, only, true);
        }
        return (null, null, false);
    }

    private static (DateTime?, Match?, bool) Calendar(int year, int month, int dayOfMonth, bool rolls, DateTime today, Match match)
    {
        if (month is < 1 or > 12 || dayOfMonth < 1 || year is < 2000 or > 2100 || dayOfMonth > DateTime.DaysInMonth(year, month))
            return (null, match, true);
        var date = new DateTime(year, month, dayOfMonth);
        if (date < today && rolls)
        {
            if (dayOfMonth > DateTime.DaysInMonth(year + 1, month)) return (null, match, true);
            date = new DateTime(year + 1, month, dayOfMonth);
        }
        return date < today ? (null, match, true) : (date, match, false);
    }

    private static DayOfWeek WeekdayOf(string name) => Key(name) switch
    {
        "lundi" or "monday" => DayOfWeek.Monday,
        "mardi" or "tuesday" => DayOfWeek.Tuesday,
        "mercredi" or "wednesday" => DayOfWeek.Wednesday,
        "jeudi" or "thursday" => DayOfWeek.Thursday,
        "vendredi" or "friday" => DayOfWeek.Friday,
        "samedi" or "saturday" => DayOfWeek.Saturday,
        _ => DayOfWeek.Sunday
    };

    private static int MonthOf(string name)
    {
        var key = Key(name);
        string[] fr = ["janvier", "fevrier", "mars", "avril", "mai", "juin", "juillet", "aout", "septembre", "octobre", "novembre", "decembre"];
        string[] en = ["january", "february", "march", "april", "may", "june", "july", "august", "september", "october", "november", "december"];
        var index = Array.IndexOf(fr, key);
        return (index >= 0 ? index : Array.IndexOf(en, key)) + 1;
    }

    private static int Number(string value) => value.ToLowerInvariant() switch
    {
        "un" or "une" or "a" or "an" or "one" => 1,
        "deux" or "two" => 2,
        "trois" or "three" => 3,
        "quatre" or "four" => 4,
        "cinq" or "five" => 5,
        "six" => 6,
        "sept" or "seven" => 7,
        "huit" or "eight" => 8,
        "neuf" or "nine" => 9,
        "dix" or "ten" => 10,
        "quinze" or "fifteen" => 15,
        var digits => int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : 0
    };

    public static string Key(string value)
    {
        var decomposed = value.Normalize(System.Text.NormalizationForm.FormD);
        var plain = new string(decomposed.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        return Regex.Replace(plain.ToLowerInvariant(), @"[^\p{L}\p{N}]+", "");
    }

    private static string Clean(string value)
    {
        const string Trim = " ,.;:-–—";
        var text = Regex.Replace(value, @"\s+", " ").Trim(Trim.ToCharArray());
        for (var i = 0; i < 6; i++)
        {
            var next = Dangling.Replace(text, "").Trim(Trim.ToCharArray());
            next = LeadingConnector.Replace(next, "").Trim(Trim.ToCharArray());
            if (next == text) break;
            text = next;
        }
        text = Regex.Replace(text, @"\s+([,.;:])", "$1");
        text = Regex.Replace(text, @"([,;:])\s*(?=[,;:])", "");
        return text.Length == 0 ? text : char.ToUpper(text[0], CultureInfo.InvariantCulture) + text[1..];
    }

    public static string ModelInstruction(DateTimeOffset now, Day day) => string.Format(CultureInfo.InvariantCulture,
        "Read one task sentence written by the user. Today is {0:dddd yyyy-MM-dd} and it is {0:HH:mm}. The user's day starts at {1:hh\:mm}, lunch is {2:hh\:mm}-{3:hh\:mm} and the day ends at {4:hh\:mm}. " +
        "Reply with exactly three lines and nothing else:\nTitle: <the task, short, in the user's language, without the date or duration>\nDue: <yyyy-MM-dd HH:mm, or none>\nMinutes: <whole number of minutes the task takes, or none>\n" +
        "Only use a due date or a duration that the sentence states or clearly implies. Never invent one.",
        now, day.Start, day.LunchStart, day.LunchEnd, day.End);

    private static readonly Regex ModelTitle = new(@"^\s*(?:title|titre)\s*:\s*(?<v>.+?)\s*$", Options | RegexOptions.Multiline);
    private static readonly Regex ModelDue = new(@"^\s*(?:due|[ée]ch[ée]ance)\s*:\s*(?:(?<d>\d{4}-\d{2}-\d{2})(?:[ T](?<t>\d{2}:\d{2}))?|none|aucune?|-)\s*$", Options | RegexOptions.Multiline);
    private static readonly Regex ModelMinutes = new(@"^\s*(?:minutes|dur[ée]e)\s*:\s*(?:(?<m>\d{1,4})|none|aucune?|-)\s*$", Options | RegexOptions.Multiline);

    public static TaskPhraseResult? FromModel(string output, DateTimeOffset now, Day day, string? space)
    {
        var title = ModelTitle.Match(output);
        var dueLine = ModelDue.Match(output);
        var minutesLine = ModelMinutes.Match(output);
        if (!title.Success || !dueLine.Success || !minutesLine.Success) return null;
        var name = Clean(title.Groups["v"].Value.Trim('"', '«', '»', ' '));
        if (name.Length is < 2 or > 140) return null;
        DateTimeOffset? due = null;
        var parts = new List<PhrasePart>();
        if (dueLine.Groups["d"].Success)
        {
            if (!DateTime.TryParseExact(dueLine.Groups["d"].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return null;
            var time = day.End;
            if (dueLine.Groups["t"].Success && !TimeSpan.TryParseExact(dueLine.Groups["t"].Value, @"hh\:mm", CultureInfo.InvariantCulture, out time)) return null;
            due = new DateTimeOffset(date.Add(time), now.Offset);
            if (due < now.AddMinutes(-1) || due > now.AddYears(2)) return null;
            parts.Add(new(PhraseKinds.Date, ""));
        }
        int? minutes = null;
        if (minutesLine.Groups["m"].Success)
        {
            var value = Number(minutesLine.Groups["m"].Value);
            if (value is < 1 or > 600) return null;
            minutes = value;
            parts.Add(new(PhraseKinds.Duration, ""));
        }
        if (due is null && minutes is null) return null;
        if (space is not null) parts.Add(new(PhraseKinds.Space, ""));
        return new(name, due, minutes, space, parts, false, false, false, true);
    }
}
