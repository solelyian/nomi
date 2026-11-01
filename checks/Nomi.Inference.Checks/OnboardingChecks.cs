using System.Text.Json;
using Nomi;

internal static class OnboardingChecks
{
    internal static void Run()
    {
        var count = 0;
        void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            count++;
        }

        Check(Onboarding.Steps.Length == 10 && Onboarding.Steps[0] == Onboarding.Welcome && Onboarding.Steps[^1] == Onboarding.Tasks, "Ten steps, from welcome to the first task");
        Check(Onboarding.Steps.Distinct().Count() == Onboarding.Steps.Length, "Step ids are unique");
        Check(Onboarding.Next("numbers") == "day" && Onboarding.Previous("numbers") == "reading", "Steps follow the validated order");
        Check(Onboarding.Previous(Onboarding.Welcome) is null && Onboarding.Next(Onboarding.Tasks) is null, "Nothing before welcome or after the first task");
        Check(!Onboarding.CanSkip(Onboarding.Welcome) && Onboarding.Steps.Skip(1).All(Onboarding.CanSkip), "Only the language step is required");

        var fresh = new Profile();
        Check(!fresh.Onboarded && fresh.OnboardingStep == Onboarding.Welcome && !fresh.TourShown, "A new profile starts the onboarding");
        Check(fresh.WorkingDays.SequenceEqual(Day.Weekdays), "Monday to Friday by default");
        var resumed = JsonSerializer.Deserialize<Profile>("{\"OnboardingStep\":\"agenda\",\"WorkingDays\":[]}")!;
        resumed.Normalize();
        Check(resumed.OnboardingStep == "agenda", "The onboarding resumes where it stopped");
        Check(resumed.WorkingDays.SequenceEqual(Day.Weekdays), "Empty working days fall back to the week");
        var broken = JsonSerializer.Deserialize<Profile>("{\"OnboardingStep\":\"unknown\",\"WorkingDays\":[6,6,2,42]}")!;
        broken.Normalize();
        Check(broken.OnboardingStep == Onboarding.Welcome, "Unknown step restarts at welcome");
        Check(broken.WorkingDays.SequenceEqual([DayOfWeek.Tuesday, DayOfWeek.Saturday]), "Working days are deduplicated and validated");
        var existing = JsonSerializer.Deserialize<Profile>("{\"Onboarded\":true}")!;
        existing.Normalize();
        Check(existing.Onboarded, "Existing users do not see the onboarding again");

        var tuesdayToSaturday = new Profile { WorkingDays = [DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday] };
        var friday = new DateTimeOffset(2026, 10, 23, 16, 0, 0, TimeSpan.FromHours(2));
        Check(tuesdayToSaturday.Day.NextStart(friday) == new DateTimeOffset(2026, 10, 24, 9, 0, 0, TimeSpan.FromHours(2)), "Next start follows the chosen working days");
        Check(new Profile().Day.NextStart(friday) == new DateTimeOffset(2026, 10, 26, 9, 0, 0, TimeSpan.FromHours(2)), "Default week skips the weekend");
        Check(Day.Default.NextStart(friday).DayOfWeek == DayOfWeek.Monday, "Default day keeps weekdays");

        var lines = Onboarding.TaskLines("  Appeler Marc \n\n- Envoyer le devis\r\n• appeler marc\n" + new string('a', 300));
        Check(lines.Count == 3 && lines[0] == "Appeler Marc" && lines[1] == "Envoyer le devis", "One task per line, bullets and duplicates removed");
        Check(lines[2].Length == Onboarding.MaxTitle, "Long lines are shortened");
        Check(Onboarding.TaskLines(string.Join('\n', Enumerable.Range(1, 40).Select(i => $"Tâche {i}"))).Count == Onboarding.MaxTasks, "At most twelve first tasks");
        Check(Onboarding.TaskLines(" \n \r\n").Count == 0, "Blank text adds nothing");

        const long gb = Hardware.Gigabyte;
        HardwareReport Pc(long memory, long disk, params string[] npus) => new("CPU", 8, "x64", memory, disk, npus, true);
        Check(Hardware.Verdict(Pc(16 * gb, 200 * gb, "Intel(R) AI Boost"), 5 * gb) == "npu", "A present NPU is recommended");
        Check(Hardware.Verdict(Pc(16 * gb, 200 * gb, "Intel(R) AI Boost") with { NpuSupported = false }, 5 * gb) == "npu-windows", "An NPU on an older Windows falls back to the processor");
        Check(Hardware.Verdict(Pc(16 * gb, 200 * gb), 5 * gb) == "cpu", "Without an NPU, Nomi uses the processor");
        Check(Hardware.Verdict(Pc((long)(7.8 * gb), 200 * gb), 5 * gb) == "cpu", "An 8 GB computer is enough for the processor");
        Check(Hardware.Verdict(Pc(4 * gb, 200 * gb), 5 * gb) == "cpu-memory", "Little memory is flagged");
        Check(Hardware.Verdict(Pc(16 * gb, 3 * gb, "NPU"), 5 * gb) == "disk" && Hardware.Gigabytes(Hardware.DiskShortfall(Pc(16 * gb, 3 * gb), 5 * gb)) == 3, "Missing disk space comes first, with the amount to free");
        Check(Hardware.Verdict(Pc(16 * gb, 3 * gb), 0) == "cpu", "A model already on disk needs no space");
        Check(Hardware.Verdict(Pc(0, 0), 5 * gb) == "cpu", "Unknown memory and disk never block");
        Check(Hardware.Gigabytes((long)(15.7 * gb)) == 16 && Hardware.Gigabytes(1) == 1 && Hardware.Gigabytes(0) == 0, "Sizes are rounded to whole gigabytes");

        Console.WriteLine($"{count} C# onboarding checks passed.");
    }
}
