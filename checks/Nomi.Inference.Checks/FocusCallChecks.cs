using Nomi;

internal static class FocusCallChecks
{
    internal static void Run()
    {
        var count = 0;
        void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            count++;
        }

        byte[] Print(byte value) => Enumerable.Repeat(value, 1024).ToArray();
        var start = new DateTimeOffset(2026, 11, 12, 14, 0, 0, TimeSpan.FromHours(1));
        var call = new FocusCall("Excel — Commissions_T3.xlsx", start, CallModes.Problem);
        Check(!call.ShouldWatch(Print(10), start), "The first image is never analysed before the screen settles");
        Check(call.ShouldWatch(Print(10), start.AddSeconds(1)), "A settled screen is analysed");
        var table = new List<string[]> { new[] { "Commercial", "CA", "Taux", "Commission" }, new[] { "Roux", "19 937,50", "8 %", "1 276,00" } };
        var gap = new TableIssue("Roux", "Commission", "CA × Taux", 1595m, 1276m);
        var insight = new ScreenInsight("Vérifier les commissions", "Excel", "", "", 1, 20, table);
        var first = call.Watched(Print(10), start.AddSeconds(1), insight, new TableReport(2, [gap]));
        Check(first.Found.Count == 1 && call.Speaks(first) && call.Points.Count == 1, "A recalculated gap makes Nomi speak once");
        Check(!call.ShouldWatch(Print(10), start.AddSeconds(30)), "The same screen is not analysed again");
        Check(!call.ShouldWatch(Print(200), start.AddSeconds(31)), "A screen still changing is not analysed");
        Check(!call.ShouldWatch(Print(200), start.AddSeconds(5)), "Nothing is analysed during the cooldown");
        Check(call.ShouldWatch(Print(200), start.AddSeconds(32)), "A new settled screen is analysed after the cooldown");
        var again = call.Watched(Print(200), start.AddSeconds(32), insight, new TableReport(2, [gap]));
        Check(again.Found.Count == 0 && again.Fixed.Count == 0 && !call.Speaks(again), "The same gap is never announced twice");
        var fixedRows = new List<string[]> { table[0], new[] { "Roux", "19 937,50", "8 %", "1 595,00" } };
        var corrected = call.Watched(Print(90), start.AddSeconds(50), insight with { Rows = fixedRows }, new TableReport(2, [], [gap.Formula]));
        Check(corrected.Fixed.Count == 1 && call.Points[0].Fixed && call.Speaks(corrected), "A corrected gap is announced as fixed");
        var scrolled = call.Watched(Print(60), start.AddSeconds(70), insight with { Rows = [table[0]] }, new TableReport(2, [], [gap.Formula]));
        Check(scrolled.Fixed.Count == 0, "A row scrolled out of view is not taken as fixed");
        var unchecked_ = new FocusCall("Excel", start, CallModes.Problem);
        unchecked_.Watched(Print(4), start, insight, new TableReport(2, [gap], [gap.Formula]));
        var unverified = unchecked_.Watched(Print(5), start.AddSeconds(20), insight, new TableReport(2, []));
        Check(unverified.Fixed.Count == 0 && !unchecked_.Points[0].Fixed, "A gap the table could not check again is not taken as fixed");
        unchecked_.Window = "Excel — Autre.xlsx";
        var otherWindow = unchecked_.Watched(Print(6), start.AddSeconds(40), insight, new TableReport(2, [gap], [gap.Formula]));
        Check(otherWindow.Found.Count == 1, "The same gap in another window is announced again");
        var products = TableCheck.Check(new List<string[]> { table[0], new[] { "Roux", "1 000", "10 %", "100" }, new[] { "Dupont", "2 000", "10 %", "300" } }, "fr");
        Check(products.Checked?.Contains("CA × Taux") != true, "One matching product is not enough to check the formula");

        call.Mode = CallModes.Ask;
        Check(!call.ShouldWatch(Print(1), start.AddSeconds(90)) && !call.ShouldWatch(Print(1), start.AddSeconds(91)), "Ask mode never looks on its own");
        call.Mode = CallModes.Comment;
        var other = call.Watched(Print(2), start.AddSeconds(100), insight with { Task = "Réviser le chapitre 4", Rows = [] }, null);
        Check(other.TaskChanged && call.Speaks(other), "Comment mode speaks when the work changes");
        call.Mode = CallModes.Problem;
        Check(!call.Speaks(other), "Problem mode stays quiet when only the work changes");
        call.Paused = true;
        Check(!call.ShouldWatch(Print(3), start.AddSeconds(200)) && !call.ShouldWatch(Print(3), start.AddSeconds(201)), "Nothing is captured for analysis while paused");
        call.Paused = false;

        call.Asked("Le total est bon ?", "voice", start.AddMinutes(4));
        for (var index = 0; index < 250; index++) call.Add(new CallLine("nomi", $"{index}", start));
        Check(call.Questions == 1 && call.Lines.Count == 200, "Questions are counted and captions are bounded");
        call.End(start.AddMinutes(18));
        call.End(start.AddMinutes(30));
        Check(call.Duration(start.AddHours(2)) == TimeSpan.FromMinutes(18), "The call ends once");
        Check(new FocusCall("x", start, "other").Mode == CallModes.Problem, "Unknown mode falls back to problems only");

        var silence = new float[SpeechSegmenter.Rate];
        var tone = Enumerable.Range(0, SpeechSegmenter.Rate).Select(index => 0.2f * MathF.Sin(index * 0.1f)).ToArray();
        var segmenter = new SpeechSegmenter();
        var events = segmenter.Feed(silence).Concat(segmenter.Feed(tone)).Concat(segmenter.Feed(silence)).ToArray();
        Check(events.Length == 2 && events[0].Started && events[1].Samples is { Length: >= 16000 }, "One second of speech gives one utterance with its start");
        var click = new float[SpeechSegmenter.Rate / 20];
        Array.Fill(click, 0.3f);
        var noise = segmenter.Feed(click).Concat(segmenter.Feed(silence)).ToArray();
        Check(noise.All(item => item.Samples is null), "A click is not an utterance");
        segmenter.Damped = true;
        var quiet = Enumerable.Range(0, SpeechSegmenter.Rate).Select(index => 0.02f * MathF.Sin(index * 0.1f)).ToArray();
        Check(!segmenter.Feed(quiet).Any(item => item.Started), "Nomi's own voice from the speakers does not interrupt it");
        segmenter.Damped = false;
        var endless = segmenter.Feed(Enumerable.Repeat(tone, 20).SelectMany(item => item).ToArray()).ToArray();
        Check(endless.Any(item => item.Samples is { Length: <= 15 * SpeechSegmenter.Rate + 480 }), "A long utterance is cut at 15 seconds");

        Check(SpeechText.Clean(" [Musique] ") == "" && SpeechText.Clean("Sous-titres réalisés par la communauté d'Amara.org") == "", "Whisper silence and credits removed");
        Check(SpeechText.Clean("  Le total est   bon ? (bruit) ") == "Le total est bon ?", "Speech text kept and tidied");
        Check(SpeechText.Samples([0x00, 0x40, 0x00, 0xC0]).SequenceEqual([0.5f, -0.5f]), "PCM 16-bit converted to samples");
        Check(VisionClient.Spoken("**Oui** : le total est bon.<turn|>\n- 11 022,50 €") == "Oui : le total est bon. 11 022,50 €", "Spoken answers without markers or Markdown");
        Check(VisionClient.Spoken("-10 € d'écart.\n- -5 % de remise") == "-10 € d'écart. -5 % de remise", "Spoken answers keep minus signs");
        Console.WriteLine($"{count} screen-sharing call and speech checks passed.");
    }
}
