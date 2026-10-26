using System.Text;
using Nomi;

internal static class RoutineChecks
{
    private const string Calendar = """
BEGIN:VCALENDAR
VERSION:2.0
BEGIN:VEVENT
UID:standup
SUMMARY:Point é
 quipe
DTSTART;TZID=Europe/Paris:20261005T093000
DTEND;TZID=Europe/Paris:20261005T094500
RRULE:FREQ=WEEKLY;BYDAY=MO,WE,FR
EXDATE;TZID=Europe/Paris:20261019T093000
END:VEVENT
BEGIN:VEVENT
UID:standup
RECURRENCE-ID;TZID=Europe/Paris:20261026T093000
SUMMARY:Point équipe
DTSTART;TZID=Europe/Paris:20261026T100000
DTEND;TZID=Europe/Paris:20261026T103000
END:VEVENT
BEGIN:VEVENT
UID:cancelled
SUMMARY:Annulé
STATUS:CANCELLED
DTSTART;TZID=Europe/Paris:20261026T083000
DTEND;TZID=Europe/Paris:20261026T090000
END:VEVENT
BEGIN:VEVENT
UID:holiday
SUMMARY:Congé
DTSTART;VALUE=DATE:20261026
DTEND;VALUE=DATE:20261027
END:VEVENT
BEGIN:VEVENT
UID:client
SUMMARY:Revue client\, budget
DTSTART:20261026T130000Z
DURATION:PT1H
END:VEVENT
BEGIN:VEVENT
UID:sprint
SUMMARY:Sprint
DTSTART;TZID=Europe/Paris:20261001T140000
DTEND;TZID=Europe/Paris:20261001T143000
RRULE:FREQ=DAILY;COUNT=3
END:VEVENT
END:VCALENDAR
""";

    internal static void Run()
    {
        var count = 0;
        void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            count++;
        }

        var fr = RoutinePhrase.Parse("Chaque lundi après le déjeuner, préparer le point équipe 20 min puis envoyer le compte rendu");
        Check(fr.Understood && fr.Routine.Trigger == Triggers.AfterLunch, "French trigger understood");
        Check(fr.Routine.Days.SequenceEqual([DayOfWeek.Monday]), "French day understood");
        Check(fr.Routine.Steps.Count == 2 && fr.Routine.Steps[0].Title == "Préparer le point équipe" && fr.Routine.Steps[0].Minutes == 20 && fr.Routine.Steps[1].Minutes == 10, "French steps understood");
        Check(fr.Routine.Name == "Préparer le point équipe", "French name taken from the first step");
        var en = RoutinePhrase.Parse("After my first meeting, review the notes then plan the day (15 min)");
        Check(en.Understood && en.Routine.Trigger == Triggers.AfterMeeting && en.Routine.Days.SequenceEqual(Routine.WorkingDays), "English meeting trigger understood");
        Check(en.Routine.Steps.Count == 2 && en.Routine.Steps[1].Title == "Plan the day" && en.Routine.Steps[1].Minutes == 15, "English steps understood");
        foreach (var (text, trigger) in new[]
        {
            ("Tous les jours en fin de journée : ranger le bureau", Triggers.DayEnd),
            ("Quand je reviens de pause, boire un verre d'eau", Triggers.BreakReturn),
            ("after each focus session, take a walk", Triggers.FocusDone),
            ("when I finish a task, check messages", Triggers.TaskDone),
            ("Chaque matin, relire mes priorités", Triggers.DayStart),
            ("Après ma première réunion, noter les décisions", Triggers.AfterMeeting)
        })
        {
            var parsed = RoutinePhrase.Parse(text);
            Check(parsed.Understood && parsed.Routine.Trigger == trigger, $"Trigger understood: {text}");
        }
        Check(!RoutinePhrase.Parse("acheter du pain").Understood, "Phrase without a moment is not saved silently");
        Check(!RoutinePhrase.Parse("après le déjeuner").Understood, "Phrase without an action is not saved silently");

        var day = Day.Default;
        var monday = new DateTimeOffset(2026, 10, 26, 9, 30, 0, TimeSpan.FromHours(1));
        var start = new Routine { Name = "Relire", Trigger = Triggers.DayStart };
        Check(RoutineStore.Due(start, new(monday, day)), "Start of day routine due");
        Check(!RoutineStore.Due(start, new(monday.AddHours(-2), day)), "Not due before the day starts");
        Check(!RoutineStore.Due(start, new(monday.AddDays(-2), day)), "Not due on a day off");
        Check(!RoutineStore.Due(start, new(monday, day, IdleMinutes: 12)), "Not due while away");
        start.Enabled = false;
        Check(!RoutineStore.Due(start, new(monday, day)), "Disabled routine never due");
        start.Enabled = true;
        start.LastRun = monday.AddMinutes(-10);
        Check(!RoutineStore.Due(start, new(monday.AddMinutes(30), day)), "Once a day at most");
        Check(RoutineStore.Due(start, new(monday.AddDays(1), day)), "Due again the next day");
        start.LastRun = null;
        start.Snoozed = monday.AddMinutes(30);
        Check(!RoutineStore.Due(start, new(monday.AddMinutes(15), day)) && RoutineStore.Due(start, new(monday.AddMinutes(35), day)), "Later waits for the snooze");
        var task = new Routine { Name = "Messages", Trigger = Triggers.TaskDone };
        Check(RoutineStore.Due(task, new(monday, day, Triggers.TaskDone)) && !RoutineStore.Due(task, new(monday, day)), "Task routine waits for a finished task");
        var focus = new Routine { Name = "Marcher", Trigger = Triggers.FocusDone };
        Check(RoutineStore.Due(focus, new(monday, day, Triggers.FocusDone)) && !RoutineStore.Due(focus, new(monday, day, Triggers.TaskDone)), "Focus routine waits for Focus");
        var lunch = new Routine { Name = "Point", Trigger = Triggers.AfterLunch };
        Check(!RoutineStore.Due(lunch, new(monday, day)) && RoutineStore.Due(lunch, new(monday.AddHours(4.5), day)), "Lunch routine after lunch");
        var back = new Routine { Name = "Eau", Trigger = Triggers.BreakReturn };
        Check(RoutineStore.Due(back, new(monday, day, BackFromBreak: true)) && !RoutineStore.Due(back, new(monday, day)), "Break routine on return");
        var end = new Routine { Name = "Ranger", Trigger = Triggers.DayEnd };
        Check(!RoutineStore.Due(end, new(monday, day)) && RoutineStore.Due(end, new(monday.AddHours(8.1), day)), "End of day routine before leaving");
        var meeting = new Routine { Name = "Noter", Trigger = Triggers.AfterMeeting };
        var meetingEnd = monday.AddHours(1);
        Check(!RoutineStore.Due(meeting, new(monday, day)), "Meeting routine needs an agenda");
        Check(!RoutineStore.Due(meeting, new(monday.AddMinutes(50), day, FirstMeetingEnd: meetingEnd)), "Meeting routine waits for the meeting to end");
        Check(RoutineStore.Due(meeting, new(monday.AddMinutes(70), day, FirstMeetingEnd: meetingEnd)), "Meeting routine after the meeting");

        var path = Path.Combine(Path.GetTempPath(), $"nomi-routines-{Guid.NewGuid():N}.json");
        try
        {
            var store = new RoutineStore(path);
            store.Items.Add(fr.Routine);
            store.Save();
            Check(!SecureFile.Encrypts || (SecureFile.IsEncrypted(path) && !Encoding.UTF8.GetString(File.ReadAllBytes(path)).Contains("équipe")), "Routines encrypted at rest");
            store.Ran(fr.Routine, monday);
            var again = new RoutineStore(path);
            Check(again.Items.Count == 1 && again.Items[0].Steps.Count == 2 && again.Items[0].LastRun == monday, "Routines reloaded");
            File.WriteAllText(path, "{not json");
            Check(new RoutineStore(path).Items.Count == 0, "Corrupt routines ignored");
        }
        finally
        {
            SecureFile.Delete(path);
        }

        var weekly = Enumerable.Range(0, 3).Select(week => new WorkTask
        {
            Title = "Point équipe",
            Column = Columns.Done,
            Estimate = 20,
            Completed = monday.AddDays(-7 * (week + 1)).AddHours(5)
        }).ToArray();
        var suggestion = RoutineStore.Suggest(weekly, [], monday);
        Check(suggestion is { } found && found.Days.SequenceEqual([DayOfWeek.Monday]) && found.Steps.Count == 1, "Weekly task suggests a routine");
        Check(RoutineStore.Suggest(weekly, [new Routine { Name = "point équipe" }], monday) is null, "Existing routine not suggested twice");
        Check(RoutineStore.Suggest(weekly.Take(2), [], monday) is null, "Two weeks are not enough");

        var paris = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");
        var today = Agenda.On(Calendar, new DateTime(2026, 10, 26), paris);
        Check(today.Count == 2, $"Agenda keeps timed, live events ({today.Count})");
        Check(today[0].Title == "Point équipe" && today[0].Start.TimeOfDay == new TimeSpan(10, 0, 0) && today[0].End.TimeOfDay == new TimeSpan(10, 30, 0), "Moved occurrence replaces the series");
        Check(today[1].Title == "Revue client, budget" && today[1].Start.TimeOfDay == new TimeSpan(14, 0, 0) && today[1].Start.Offset == TimeSpan.FromHours(1), "UTC event shown in local time");
        Check(Agenda.FirstMeetingEnd(today, day) == today[0].End, "First meeting found");
        Check(Agenda.On(Calendar, new DateTime(2026, 10, 19), paris).Count == 0, "Excluded date skipped");
        var wednesday = Agenda.On(Calendar, new DateTime(2026, 10, 21), paris);
        Check(wednesday.Count == 1 && wednesday[0].Start.TimeOfDay == new TimeSpan(9, 30, 0) && wednesday[0].Title == "Point équipe", "Weekly series keeps its local time across DST");
        Check(Agenda.On(Calendar, new DateTime(2026, 10, 20), paris).Count == 0, "Weekly series only on its days");
        Check(Agenda.On(Calendar, new DateTime(2026, 10, 3), paris).Any(item => item.Title == "Sprint") && !Agenda.On(Calendar, new DateTime(2026, 10, 4), paris).Any(item => item.Title == "Sprint"), "Count limits a series");
        Check(Agenda.FirstMeetingEnd([new(monday.AddHours(10), monday.AddHours(11), "Late")], day) is null, "Evening events ignored");
        Check(Agenda.On("garbage", new DateTime(2026, 10, 26), paris).Count == 0, "Invalid calendar ignored");
        Console.WriteLine($"{count} routine and agenda checks passed.");
    }
}
