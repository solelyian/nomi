using Nomi;

static class ReminderChecks
{
    public static void Run()
    {
        var count = 0;
        void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("Reminders: " + message);
            count++;
        }
        var offset = TimeSpan.FromHours(2);
        DateTimeOffset At(int date, int hour, int minute) => new(2026, 10, date, hour, minute, 0, offset);
        var day = Day.Default;
        var task = new WorkTask { Title = "Rapport", Estimate = 45, Due = At(21, 16, 0) };
        WorkTask[] tasks = [task];
        ReminderNotice? Next(DateTimeOffset now, bool busy = false, string lead = ReminderLeads.Start) => Reminders.Next(tasks, now, day, lead, busy);

        Check(Reminders.When(task, At(21, 10, 0), day, ReminderLeads.Start) == At(21, 15, 5), "start time keeps a ten minute margin");
        Check(Reminders.When(task, At(21, 10, 0), day, ReminderLeads.Quarter) == At(21, 15, 45), "fifteen minutes before");
        Check(Reminders.When(task, At(21, 10, 0), day, ReminderLeads.Hour) == At(21, 15, 0), "one hour before");
        Check(Next(At(21, 15, 4)) is null, "nothing before the start time");
        var notice = Next(At(21, 15, 5));
        Check(notice is { Kind: Reminders.StartKind, Discreet: false } && notice.Task == task, "start reminder fires");
        Reminders.Mark(task, notice!.Key);
        Check(Next(At(21, 15, 10)) is null, "one reminder per task and deadline");

        task.Due = At(21, 17, 0);
        Check(Next(At(21, 16, 5)) is { Kind: Reminders.StartKind }, "a changed deadline is reminded again");
        Reminders.Mark(task, Next(At(21, 16, 5))!.Key);
        Check(task.Reminded.Count == 1, "old deadline keys are dropped");

        task.Due = At(21, 16, 0);
        task.Reminded.Clear();
        Check(Next(At(21, 15, 5), busy: true) is null, "calm mode holds the reminder during work");
        Check(Next(At(21, 15, 50), busy: true) is { Discreet: true }, "an imminent deadline still gets a discreet reminder");

        Reminders.Snooze(task, At(21, 15, 5));
        Check(Next(At(21, 15, 10)) is null && Next(At(21, 15, 20)) is not null, "later delays by fifteen minutes");
        Reminders.Mark(task, Next(At(21, 15, 20))!.Key);

        Check(Next(At(21, 16, 30)) is { Kind: Reminders.LateKind }, "one reminder after the deadline");
        Reminders.Mark(task, Next(At(21, 16, 30))!.Key);
        Check(Next(At(21, 16, 40)) is null, "late reminder only once");
        Check(Next(At(23, 16, 40)) is null, "old overdue tasks stay quiet");

        task.Reminded.Clear();
        task.Due = At(21, 17, 50);
        Reminders.Mark(task, Reminders.KeyFor(Reminders.StartKind, task.Due.Value));
        Check(Next(At(21, 19, 0)) is null, "no late reminder in the evening");
        Check(Next(At(22, 9, 0)) is { Kind: Reminders.LateKind }, "late reminder waits for the next working morning");

        var morning = new WorkTask { Title = "Point", Estimate = 30, Due = At(22, 9, 30) };
        tasks = [morning];
        Check(Reminders.When(morning, At(21, 19, 0), day, ReminderLeads.Start) == At(22, 9, 0), "early start moves to the start of the day");
        Check(Next(At(21, 19, 0)) is null, "no evening reminder for tomorrow morning");
        var monday = new WorkTask { Title = "Lundi", Estimate = 60, Due = At(26, 9, 15) };
        tasks = [monday];
        Check(Next(At(24, 10, 0)) is null && Reminders.When(monday, At(24, 10, 0), day, ReminderLeads.Start) == At(26, 9, 0), "weekend is skipped");
        monday.Column = Columns.Done;
        Check(Next(At(26, 9, 5)) is null, "finished tasks are not reminded");

        var profile = new Profile { ReminderLead = "soon" };
        profile.Normalize();
        Check(profile.ReminderLead == ReminderLeads.Start && profile.DueReminders, "reminder preferences normalize");
        Console.WriteLine($"{count} C# reminder checks passed.");
    }
}
