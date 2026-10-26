using Nomi;

internal static class RescueChecks
{
    internal static void Run()
    {
        var count = 0;
        void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            count++;
        }

        var path = Path.Combine(Path.GetTempPath(), $"nomi-rescue-{Guid.NewGuid():N}.json");
        try
        {
            var board = new TaskBoard(path) { Day = Day.Default };
            var now = new DateTimeOffset(2026, 10, 23, 16, 0, 0, TimeSpan.FromHours(2));
            var running = board.Add("Rapport");
            running.Estimate = 60;
            board.Start(running, now.AddMinutes(-20));
            var stepped = board.Add("Préparer la présentation");
            stepped.Steps = [new() { Title = "Plan", Minutes = 20 }, new() { Title = "Diapositives", Minutes = 60 }, new() { Title = "Relecture", Minutes = 30 }];
            stepped.Estimate = 110;
            var long1 = board.Add("Trier les mails");
            long1.Estimate = 90;
            board.Save();
            var plan = board.Plan(now);
            var lines = DayRescue.Propose(plan, now, Day.Default, 1);
            Check(lines.Count == 3 && lines[0].Task == running && lines[0].Choice == RescueChoices.Keep, "Running task kept");
            Check(lines[1].Choice == RescueChoices.Lighten && lines[1].Light == 20, "Stepped task lightened to its next step");
            Check(lines[2].Choice == RescueChoices.Defer, "What does not fit is deferred");
            var before = board.Tasks.Count;
            var undo = DayRescue.Apply(board, lines, now, Day.Default);
            var monday = new DateTimeOffset(2026, 10, 26, 9, 0, 0, TimeSpan.FromHours(2));
            Check(long1.Due == monday, "Deferred to the next working day");
            Check(!board.Plan(now).Contains(long1), "Deferred task leaves today's plan");
            Check(board.Tasks.Count == before + 1 && stepped.Steps.Count == 1 && stepped.Estimate == 20, "Lightened task keeps one step today");
            var rest = board.Tasks.Single(task => task.Origin == "rescue");
            Check(rest.Steps.Count == 2 && rest.Estimate == 90 && rest.Due == monday, "Rest of the work moved to a follow-up");
            Check(running.Due is null && running.RunningSince is not null, "Kept task untouched");
            DayRescue.Undo(board, undo);
            Check(board.Tasks.Count == before && board.Find(stepped.Id)!.Steps.Count == 3 && board.Find(long1.Id)!.Due is null, "Undo restores the day");
            var thursday = new DateTimeOffset(2026, 10, 22, 16, 0, 0, TimeSpan.FromHours(2));
            var doing = board.Add("Classer les factures");
            doing.Estimate = 120;
            board.Start(doing, thursday);
            var deferDoing = DayRescue.Apply(board, [new RescueLine(doing, RescueChoices.Defer, 120, 60)], thursday, Day.Default);
            Check(doing.Column == Columns.Todo && doing.RunningSince is null && !board.Plan(thursday).Contains(doing), "Deferred running task leaves today");
            DayRescue.Undo(board, deferDoing);
            board.Delete(board.Find(doing.Id)!);
            var empty = DayRescue.Propose([], now, Day.Default, 1);
            Check(empty.Count == 0, "Empty day has nothing to rescue");
            var relaxed = DayRescue.Propose(board.Plan(now.AddHours(-6)), now.AddHours(-6), Day.Default, 1);
            Check(relaxed.All(line => line.Choice == RescueChoices.Keep), "A day that fits keeps everything");
        }
        finally
        {
            SecureFile.Delete(path);
        }

        var release = Updates.Parse("""{"tag_name":"v0.10.0","html_url":"https://github.com/solelyian/nomi/releases/tag/v0.10.0","draft":false,"prerelease":false}""");
        Check(release is { } found && found.Version == new Version(0, 10, 0), "Release parsed");
        Check(Updates.Newer(release!, new Version(0, 9, 0, 0)) && !Updates.Newer(release!, new Version(0, 10, 0)), "Newer release detected");
        Check(Updates.Parse("""{"tag_name":"v1.0.0","html_url":"https://evil.example/solelyian/nomi/"}""") is null, "Foreign release page refused");
        Check(Updates.Parse("""{"tag_name":"v1.0.0","html_url":"https://github.com/solelyian/nomi/releases/tag/v1.0.0","prerelease":true}""") is null, "Pre-release ignored");
        Check(Updates.Parse("not json") is null && Updates.Parse("[]") is null, "Invalid release ignored");
        var day = new DateTimeOffset(2026, 10, 26, 9, 0, 0, TimeSpan.FromHours(1));
        Check(Updates.Due(null, day) && !Updates.Due(day.AddHours(-3), day) && Updates.Due(day.AddDays(-1), day) && Updates.Due(day.AddDays(2), day), "Checked at most once a day");

        Check(AgendaSource.Remote("webcal://p01-caldav.icloud.com/published/2/abc")?.Scheme == "https", "webcal becomes https");
        Check(AgendaSource.Remote("http://calendar.example/feed.ics") is null && !AgendaSource.Valid("ftp://x/y.ics"), "Unencrypted agenda refused");
        Check(AgendaSource.Valid(@"C:\Users\me\agenda.ics") && !AgendaSource.Valid(@"C:\Users\me\agenda.txt") && !AgendaSource.Valid("agenda.ics"), "Local calendar file accepted");
        Check(AgendaSource.Describe("https://outlook.office365.com/owa/calendar/secret/calendar.ics") == "outlook.office365.com", "Agenda address not shown in full");
        Console.WriteLine($"{count} day rescue, update and agenda source checks passed.");
    }
}
