using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Nomi;

public static class NumberModes
{
    public const string Words = "words";
    public const string Proportions = "proportions";
    public const string Figures = "figures";
    public static readonly string[] All = [Words, Proportions, Figures];
}

public sealed class Signal
{
    public int Accepted { get; set; }
    public int Dismissed { get; set; }
}

public sealed record Day(TimeSpan Start, TimeSpan LunchStart, TimeSpan LunchEnd, TimeSpan End)
{
    public static readonly Day Default = new(new(9, 0, 0), new(12, 30, 0), new(13, 30, 0), new(18, 0, 0));

    public DateTimeOffset At(DateTimeOffset now, TimeSpan time) =>
        new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset).Add(time);

    public double Available(DateTimeOffset now)
    {
        var end = At(now, End);
        var from = At(now, Start) > now ? At(now, Start) : now;
        var total = Math.Max(0, (end - from).TotalMinutes);
        var lunchStart = At(now, LunchStart) > from ? At(now, LunchStart) : from;
        var lunchEnd = At(now, LunchEnd) < end ? At(now, LunchEnd) : end;
        return Math.Max(0, total - Math.Max(0, (lunchEnd - lunchStart).TotalMinutes));
    }

    public DateTimeOffset NextStart(DateTimeOffset now)
    {
        var day = now.Date.AddDays(1);
        while (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) day = day.AddDays(1);
        return new DateTimeOffset(day.Add(Start), now.Offset);
    }
    public bool Valid => Start < LunchStart && LunchStart < LunchEnd && LunchEnd < End;
}

public sealed class Profile
{
    public const int MuteAfter = 3;
    public string Language { get; set; } = "fr";
    public string Theme { get; set; } = "system";
    public string Numbers { get; set; } = NumberModes.Words;
    public string Response { get; set; } = "steps";
    public bool Calm { get; set; } = true;
    public bool Chain { get; set; } = true;
    public string StepSize { get; set; } = "small";
    public TimeSpan DayStart { get; set; } = Day.Default.Start;
    public TimeSpan LunchStart { get; set; } = Day.Default.LunchStart;
    public TimeSpan LunchEnd { get; set; } = Day.Default.LunchEnd;
    public TimeSpan DayEnd { get; set; } = Day.Default.End;
    public string Font { get; set; } = "hyperlegible";
    public string Spacing { get; set; } = "standard";
    public string Motion { get; set; } = "system";
    public string Contrast { get; set; } = "standard";
    public bool Onboarded { get; set; }
    public List<string> Corrections { get; set; } = [];
    public List<string> Dismissed { get; set; } = [];
    public List<string> Muted { get; set; } = [];
    public Dictionary<string, Signal> Signals { get; set; } = [];

    public Day Day
    {
        get
        {
            var day = new Day(DayStart, LunchStart, LunchEnd, DayEnd);
            return day.Valid ? day : Day.Default;
        }
    }

    public void Normalize()
    {
        if (Language is not ("fr" or "en")) Language = "fr";
        if (Theme is not ("system" or "light" or "dark")) Theme = "system";
        if (!NumberModes.All.Contains(Numbers)) Numbers = NumberModes.Words;
        if (Response is not ("steps" or "summary")) Response = "steps";
        if (StepSize is not ("small" or "medium")) StepSize = "small";
        if (Font is not ("hyperlegible" or "system")) Font = "hyperlegible";
        if (Spacing is not ("standard" or "airy")) Spacing = "standard";
        if (Motion is not ("system" or "reduced")) Motion = "system";
        if (Contrast is not ("standard" or "strong")) Contrast = "standard";
        if (!Day.Valid || !new Day(DayStart, LunchStart, LunchEnd, DayEnd).Valid)
            (DayStart, LunchStart, LunchEnd, DayEnd) = (Day.Default.Start, Day.Default.LunchStart, Day.Default.LunchEnd, Day.Default.End);
        Corrections = Corrections.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()).Distinct().Take(20).ToList();
        Dismissed = Dismissed.Distinct().TakeLast(200).ToList();
        Muted = Muted.Distinct().ToList();
    }

    public void Record(string kind, bool accepted, string? id = null)
    {
        if (!Signals.TryGetValue(kind, out var signal)) Signals[kind] = signal = new Signal();
        if (accepted) signal.Accepted++;
        else signal.Dismissed++;
        if (id is not null && !Dismissed.Contains(id)) Dismissed.Add(id);
        if (!accepted && signal.Dismissed >= MuteAfter && signal.Dismissed >= signal.Accepted * 3 && !Muted.Contains(kind)) Muted.Add(kind);
        if (accepted) Muted.Remove(kind);
    }

    public void Resume(string kind)
    {
        Muted.Remove(kind);
        Signals.Remove(kind);
    }
}

public sealed class ProfileStore(string? file = null)
{
    public string Path { get; } = file ?? System.IO.Path.Combine(SecureFile.Folder, "profile.json");

    public Profile Load()
    {
        try
        {
            var text = SecureFile.Read(Path);
            var profile = text is null ? new Profile() : JsonSerializer.Deserialize<Profile>(text) ?? new Profile();
            profile.Normalize();
            return profile;
        }
        catch (JsonException)
        {
            return new Profile();
        }
    }

    public void Save(Profile profile)
    {
        profile.Normalize();
        SecureFile.Write(Path, JsonSerializer.Serialize(profile));
    }
}

public static class SecureFile
{
    private static readonly byte[] Magic = "NOMI1"u8.ToArray();
    private static readonly byte[] Entropy = "Nyne.Nomi.local.v1"u8.ToArray();

    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nyne", "Nomi");

    public static bool Encrypts => OperatingSystem.IsWindows();

    public static bool IsEncrypted(string path)
    {
        try
        {
            if (!File.Exists(path)) return false;
            using var stream = File.OpenRead(path);
            var head = new byte[Magic.Length];
            return stream.Read(head, 0, head.Length) == head.Length && head.AsSpan().SequenceEqual(Magic);
        }
        catch (IOException) { return false; }
    }

    public static string? Read(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var bytes = File.ReadAllBytes(path);
            if (!bytes.AsSpan().StartsWith(Magic)) return Encoding.UTF8.GetString(bytes);
            if (!OperatingSystem.IsWindows()) return null;
            var plain = ProtectedData.Unprotect(bytes[Magic.Length..], Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch (CryptographicException)
        {
            StartupDiagnostics.Write($"Unreadable local file set aside: {Path.GetFileName(path)}");
            try { File.Move(path, path + ".unreadable", true); } catch (IOException) { }
            return null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static void Write(string path, string text)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var plain = Encoding.UTF8.GetBytes(text);
            var data = OperatingSystem.IsWindows()
                ? [.. Magic, .. ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser)]
                : plain;
            var temporary = path + ".tmp";
            File.WriteAllBytes(temporary, data);
            File.Move(temporary, path, true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or CryptographicException)
        {
            StartupDiagnostics.Write($"Local file not saved: {Path.GetFileName(path)} ({error.GetType().Name})");
        }
    }

    public static void Delete(string path)
    {
        foreach (var file in new[] { path, path + ".tmp", path + ".unreadable" })
        {
            try { if (File.Exists(file)) File.Delete(file); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        }
    }
}

public static class Readable
{
    private static bool French(string language) => language != "en";

    public static string Figures(double minutes)
    {
        var total = (int)Math.Round(Math.Max(0, minutes));
        return total < 60 ? $"{total} min" : $"{total / 60} h {total % 60:00}";
    }

    public static string Duration(double minutes, string mode, string language)
    {
        var fr = French(language);
        var value = Math.Max(0, minutes);
        if (mode == NumberModes.Figures) return Figures(value);
        if (mode == NumberModes.Proportions)
        {
            if (value < 8) return fr ? "quelques minutes" : "a few minutes";
            var quarters = (int)Math.Max(1, Math.Round(value / 15));
            return quarters switch
            {
                1 => fr ? "un quart d’heure" : "a quarter of an hour",
                2 => fr ? "une demi-heure" : "half an hour",
                3 => fr ? "trois quarts d’heure" : "three quarters of an hour",
                4 => fr ? "une heure" : "an hour",
                5 => fr ? "une heure et quart" : "an hour and a quarter",
                6 => fr ? "une heure et demie" : "an hour and a half",
                7 => fr ? "presque deux heures" : "almost two hours",
                8 => fr ? "deux heures" : "two hours",
                <= 11 => fr ? "environ deux heures et demie" : "about two and a half hours",
                <= 14 => fr ? "environ trois heures" : "about three hours",
                _ => fr ? "plus de trois heures" : "more than three hours"
            };
        }
        return value switch
        {
            < 1 => fr ? "rien" : "nothing",
            <= 10 => fr ? "un tout petit moment" : "a very short while",
            <= 25 => fr ? "un petit moment" : "a short while",
            <= 50 => fr ? "un moment moyen" : "a medium stretch",
            <= 100 => fr ? "un long moment" : "a long stretch",
            <= 200 => fr ? "une bonne partie de la demi-journée" : "a good part of a half-day",
            _ => fr ? "une grande partie de la journée" : "most of the day"
        };
    }

    public static string Fraction(double ratio, string language)
    {
        var fr = French(language);
        return ratio switch
        {
            <= 0.02 => fr ? "rien encore" : "nothing yet",
            < 0.18 => fr ? "une toute petite partie" : "a small part",
            < 0.29 => fr ? "environ un quart" : "about a quarter",
            < 0.42 => fr ? "environ un tiers" : "about a third",
            < 0.58 => fr ? "environ la moitié" : "about half",
            < 0.71 => fr ? "environ deux tiers" : "about two thirds",
            < 0.86 => fr ? "environ trois quarts" : "about three quarters",
            < 0.98 => fr ? "presque tout" : "almost all",
            <= 1.02 => fr ? "tout" : "all",
            _ => fr ? "plus que prévu" : "more than planned"
        };
    }

    public static string Clock(DateTimeOffset time, DateTimeOffset now, string mode, string language, Day day)
    {
        var fr = French(language);
        if (mode == NumberModes.Figures) return time.ToString("HH:mm", CultureInfo.InvariantCulture);
        var t = time.TimeOfDay;
        if (time.Date != now.Date)
        {
            var part = t < day.LunchStart ? (fr ? "matin" : "morning") : t < day.End ? (fr ? "après-midi" : "afternoon") : (fr ? "soir" : "evening");
            if (time.Date == now.Date.AddDays(1)) return fr ? $"demain {part}" : $"tomorrow {part}";
            if (time.Date == now.Date.AddDays(-1)) return fr ? $"hier {part}" : $"yesterday {part}";
            var culture = CultureInfo.GetCultureInfo(fr ? "fr-FR" : "en-GB");
            return time.Date < now.Date ? time.ToString("d MMMM", culture) : $"{time.ToString("dddd", culture)} {part}";
        }
        if (Math.Abs((time - now).TotalMinutes) <= 5) return fr ? "maintenant" : "now";
        if (t < day.Start) return fr ? "tôt le matin" : "early in the morning";
        if (t >= day.LunchStart && t < day.LunchEnd) return fr ? "pendant le déjeuner" : "during lunch";
        if (t > day.End) return (t - day.End).TotalMinutes > 120 ? (fr ? "dans la soirée" : "in the evening") : (fr ? "après votre fin de journée" : "after your working day");
        var morning = t < day.LunchStart;
        var (from, to) = morning ? (day.Start, day.LunchStart) : (day.LunchEnd, day.End);
        var share = (t - from).TotalMinutes / Math.Max(1, (to - from).TotalMinutes);
        if (mode == NumberModes.Proportions)
        {
            var period = morning ? (fr ? "de la matinée" : "morning") : (fr ? "de l’après-midi" : "afternoon");
            return (int)Math.Round(share * 4) switch
            {
                0 => fr ? $"au début {period}" : $"at the start of the {period}",
                1 => fr ? $"au premier quart {period}" : $"a quarter into the {period}",
                2 => fr ? $"à la moitié {period}" : $"halfway through the {period}",
                3 => fr ? $"aux trois quarts {period}" : $"three quarters into the {period}",
                _ => fr ? $"à la fin {period}" : $"at the end of the {period}"
            };
        }
        if (!morning && (t - day.LunchEnd).TotalMinutes <= 45) return fr ? "juste après le déjeuner" : "just after lunch";
        if (morning && (day.LunchStart - t).TotalMinutes <= 30) return fr ? "juste avant le déjeuner" : "just before lunch";
        if (!morning && (day.End - t).TotalMinutes <= 30) return fr ? "juste avant la fin de journée" : "just before the end of the day";
        return share switch
        {
            < 0.34 => morning ? (fr ? "en début de matinée" : "early in the morning") : (fr ? "en début d’après-midi" : "early in the afternoon"),
            < 0.67 => morning ? (fr ? "en milieu de matinée" : "mid-morning") : (fr ? "en milieu d’après-midi" : "mid-afternoon"),
            _ => morning ? (fr ? "en fin de matinée" : "late in the morning") : (fr ? "en fin d’après-midi" : "late in the afternoon")
        };
    }

    public static string Span(DateTimeOffset start, DateTimeOffset end, DateTimeOffset now, string mode, string language, Day day) =>
        mode == NumberModes.Figures
            ? $"{start.ToString("HH:mm", CultureInfo.InvariantCulture)} – {end.ToString("HH:mm", CultureInfo.InvariantCulture)}"
            : Clock(start, now, mode, language, day);

    public static string Pace(double ratio, string language)
    {
        var fr = French(language);
        return ratio switch
        {
            < 0.85 => fr ? "Vous allez plus vite que prévu" : "You finish faster than planned",
            <= 1.15 => fr ? "Vos estimations tombent juste" : "Your estimates are on target",
            <= 1.5 => fr ? "Vos tâches prennent un peu plus de temps que prévu" : "Your tasks take a little longer than planned",
            _ => fr ? "Vos tâches prennent nettement plus de temps que prévu" : "Your tasks take much longer than planned"
        };
    }

    public static string Instruction(string mode, string language)
    {
        var exact = French(language) ? "« Chiffres exacts : »" : "\"Exact figures:\"";
        return mode switch
        {
            NumberModes.Words => $"Number display preference: words. Express times as landmarks of the day (before lunch, mid-afternoon, end of the day), durations as everyday words (a short while, a medium stretch) and amounts or percentages as plain comparisons. Never change a value. Finish with one short line starting with {exact} that lists every exact figure used.",
            NumberModes.Proportions => $"Number display preference: proportions. Express times as positions within the morning or afternoon, durations as fractions of an hour, and amounts or percentages as fractions of a whole (a quarter, about two thirds). Never change a value. Finish with one short line starting with {exact} that lists every exact figure used.",
            _ => ""
        };
    }
}
