using System.Globalization;
using Nomi;

internal static class WorkChecks
{
    internal static void Run()
    {
        var count = 0;
        void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            count++;
        }

        var rows = TableCheck.ParseRows("""
            Commercial | CA sept. (€) | Taux | Commission (€)
            A. Martin | 48 200 | 5 % | 2 410
            L. Nguyen | 39 750 | 5 % | 1 987,50
            S. Diallo | 52 400 | 6 % | 3 144
            C. Roux | 31 900 | 5 % | 1 276
            M. Petit | 44 100 | 5 % | 2 205
            Total | 216 350 | | 11 022,50
            """.Split('\n'));
        var report = TableCheck.Check(rows, "fr");
        Check(report.Rows == 6, "Table rows counted");
        Check(report.Issues.Count == 1 && report.Issues[0].Row == "C. Roux" && report.Issues[0].Expected == 1595m
            && report.Issues[0].Shown == 1276m, "Commission gap detected");
        var wrongTotal = TableCheck.Check(TableCheck.ParseRows("""
            Item | Qty | Price
            A | 2 | 1,250.00
            B | 3 | 740.50
            Total | 5 | 2,000.00
            """.Split('\n')), "en");
        Check(wrongTotal.Issues.Count == 1 && wrongTotal.Issues[0].Expected == 1990.5m, "Column total gap detected");
        var folded = TableCheck.ParseRows(["Commercial | CA (€) | Taux | Commission (€) | A. Martin | 48 200 | 5 % | 2 410 | C. Roux | 31 900 | 5 % | 1 276 | Total | 80 100 | | 3 686"]);
        Check(folded.Count == 4 && folded[2][0] == "C. Roux" && folded[3][3] == "3 686", "Table written on one line unfolded into rows");
        Check(TableCheck.ParseRows(["Note | 12 | x"]).Count == 1, "Short single row kept as is");
        Check(TableCheck.Check(TableCheck.ParseRows(["a | b", "x | 1"]), "fr").Issues.Count == 0, "Small table has no issue");
        Check(TableCheck.TryNumber("12,5 %", "fr", out var rate, out var percent) && rate == 0.125m && percent, "Percent parsing");
        Check(TableCheck.TryNumber("$1,234", "en", out var dollars, out _) && dollars == 1234m, "English thousands");
        Check(!TableCheck.TryNumber("Q3 2025", "fr", out _, out _), "Labels are not numbers");

        var insight = VisionClient.Parse("""
            **TASK:** Vérifier les commissions de septembre
            WHERE: Excel, Commissions_T3.xlsx
            CHECK: AUCUN
            NEXT: Corriger la ligne C. Roux
            MATCH: 1
            ROWS: Commercial | CA | Taux | Commission
            A | 100 | 5 % | 5
            <|im_end|>
            """, 2, 12);
        Check(insight.Task.StartsWith("Vérifier") && insight.Check.Length == 0 && insight.Match == 1 && insight.Rows.Count == 2,
            "Vision answer parsed");
        Check(VisionClient.Parse("TASK: x\nMATCH: 7", 2, 1).Match == 0, "Out of range match ignored");

        var file = Path.Combine(Path.GetTempPath(), $"nomi-tasks-{Guid.NewGuid():N}.json");
        try
        {
            var now = new DateTimeOffset(2026, 9, 30, 9, 0, 0, TimeSpan.Zero);
            var board = new TaskBoard(file);
            var task = board.Add("Vérifier les commissions", space: "sales", origin: "focus");
            Check(task.Column == Columns.Todo && task.Estimate == 30, "Task added with default estimate");
            board.Start(task, now);
            Check(task.Column == Columns.Doing && board.Active == task, "Starting moves to in progress");
            var second = board.Add("Préparer le point client");
            board.Start(second, now.AddMinutes(10));
            Check(task.RunningSince is null && Math.Abs(task.SpentSeconds - 600) < 0.1, "Only one running timer");
            Check(board.Suggestions(now.AddMinutes(50)).Any(item => item.Kind == "overrun" && item.TaskId == second.Id), "Overrun suggested");
            var overrun = board.Suggestions(now.AddMinutes(50)).First(item => item.Kind == "overrun");
            board.Apply(overrun, now.AddMinutes(50));
            Check(second.Estimate == 50 && board.Suggestions(now.AddMinutes(50)).All(item => item.Id != overrun.Id),
                "Suggestion applied only on request and dismissed");
            board.Move(second, Columns.Done, now.AddMinutes(60));
            Check(second.RunningSince is null && second.Completed is not null, "Done pauses timer");
            for (var index = 0; index < 3; index++)
            {
                var done = board.Add($"Tâche {index}");
                done.Estimate = 20;
                done.SpentSeconds = 30 * 60;
                board.Move(done, Columns.Done, now);
            }
            Check(Math.Abs(board.Pace().Ratio - 1.5) < 0.01, "Pace learned from actual durations");
            board.Add("Lire le chapitre 4");
            Check(board.Suggestions(now).Any(item => item.Kind == "pace"), "Estimate adjustment suggested");
            var reloaded = new TaskBoard(file);
            Check(reloaded.Tasks.Count == board.Tasks.Count && reloaded.Find(task.Id)?.Space == "sales", "Tasks persisted locally");
        }
        finally
        {
            if (File.Exists(file)) File.Delete(file);
        }

        var steps = TaskBoard.ParseSteps("""
            Plan :
            1. Exporter le tableau des ventes — 10 min
            2. Recalculer les commissions (15 min)
            - Envoyer le récapitulatif : 5 min
            3. Relire les totaux
            """);
        Check(steps.Count == 4 && steps[0].Title == "Exporter le tableau des ventes" && steps[0].Minutes == 10
            && steps[1].Minutes == 15 && steps[2].Minutes == 5 && steps[3].Minutes == 0, "Steps parsed with durations");

        var frame = new ScreenFrame(4, 2, Enumerable.Range(0, 32).Select(index => (byte)(index * 7)).ToArray());
        var bitmap = frame.Fit(2).ToBitmap();
        Check(bitmap[0] == 'B' && bitmap[1] == 'M' && BitConverter.ToInt32(bitmap, 18) == 2 && BitConverter.ToInt32(bitmap, 22) == 1,
            "Frames resized and encoded");
        Check(new ScreenFrame(2, 2, new byte[16]).IsBlank() && !frame.IsBlank(), "Blank capture detected");
        var enlarged = frame.Fit(16, 32);
        Check(enlarged.Width == 8 && enlarged.Height == 4 && enlarged.Pixels.Length == 128, "Small captures enlarged for the vision model");
        Check(frame.Fit(6, 1000).Width == 6, "Enlargement capped by the longest side");
        var print = frame.Thumbprint();
        var changed = new ScreenFrame(4, 2, Enumerable.Range(0, 32).Select(index => (byte)(255 - index * 7)).ToArray()).Thumbprint();
        Check(print.Length == 8 && ScreenFrame.Difference(print, frame.Thumbprint()) == 0, "Identical frames share a thumbprint");
        Check(ScreenFrame.Difference(print, changed) > 0.2, "Changed frame detected");
        Check(ScreenFrame.Difference(null, print) == 1 && ScreenFrame.Difference(print, new byte[3]) == 1, "Missing thumbprint forces analysis");
        _ = CultureInfo.InvariantCulture;
        Console.WriteLine($"{count} task, table and vision checks passed.");
    }
}
