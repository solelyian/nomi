using Nomi;

static class TaskPhraseChecks
{
    public static void Run()
    {
        var count = 0;
        void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("Task phrase: " + message);
            count++;
        }
        var offset = TimeSpan.FromHours(2);
        var now = new DateTimeOffset(2026, 10, 21, 10, 0, 0, offset);
        var day = Day.Default;
        PhraseSpace[] spaces = [new("acme", "Acme"), new("perso", "Personnel")];
        DateTimeOffset At(int month, int date, int hour, int minute, int year = 2026) => new(year, month, date, hour, minute, 0, offset);
        TaskPhraseResult Fr(string text, DateTimeOffset? when = null, Day? landmarks = null, IReadOnlySet<string>? ignored = null) =>
            TaskPhrase.Parse(text, when ?? now, landmarks ?? day, spaces, "fr", ignored);
        TaskPhraseResult En(string text) => TaskPhrase.Parse(text, now, day, spaces, "en");

        var marc = Fr("appeler Marc demain après le déjeuner, 20 min #Acme");
        Check(marc.Title == "Appeler Marc", "title of the reference sentence: " + marc.Title);
        Check(marc.Due == At(10, 22, 13, 45), "after lunch uses the lunch landmark");
        Check(marc.Minutes == 20 && marc.Space == "acme" && !marc.Unclear, "duration and space");
        Check(marc.Parts.Count == 4, "four understood parts");

        var english = En("call Marc tomorrow at 2:30pm for 45 min");
        Check(english.Title == "Call Marc" && english.Due == At(10, 22, 14, 30) && english.Minutes == 45, "English sentence: " + english.Title);
        Check(Fr("rapport avant vendredi").Due == At(10, 23, 18, 0), "deadline weekday ends with the day");
        Check(Fr("rapport avant vendredi").Title == "Rapport", "deadline lead removed from the title");
        Check(Fr("réunion lundi prochain à 14h").Due == At(10, 26, 14, 0), "next Monday at 14h");
        Check(Fr("envoyer le devis dans 3 jours").Due == At(10, 24, 18, 0), "in three days");
        Check(En("plan next week").Due == At(10, 26, 18, 0), "next week starts on Monday");
        Check(Fr("relire 12/11").Due == At(11, 12, 18, 0) && En("review 12/11").Due == At(12, 11, 18, 0), "numeric dates follow the language");
        Check(En("send it at 2 pm").Due == At(10, 21, 14, 0), "2 pm");
        Check(Fr("bilan le 5 janvier").Due == At(1, 5, 18, 0, 2027), "past month rolls to next year");

        var february = Fr("relire le contrat le 31 février");
        Check(february.InvalidDate && february.Due is null && february.Title == "Relire le contrat", "31 February is ignored");
        var weekly = Fr("point équipe chaque lundi");
        Check(weekly.Recurring && weekly.Due is null && weekly.Title == "Point équipe", "recurrence becomes a routine hint");
        Check(Fr("faire le ménage 1h30").Minutes == 90 && Fr("faire le ménage 1h30").Due is null, "1h30 is a duration");
        Check(Fr("préparer les slides une demi-heure").Minutes == 30, "half an hour in words");
        Check(Fr("ranger un quart d'heure").Minutes == 15, "quarter of an hour in words");

        var early = new Day(new(8, 0, 0), new(12, 0, 0), new(13, 0, 0), new(17, 0, 0));
        Check(Fr("appeler Léa après le déjeuner", landmarks: early).Due == At(10, 21, 13, 15), "moments follow the profile landmarks");
        Check(Fr("écrire à Paul en fin de journée", landmarks: early).Due == At(10, 21, 17, 0), "end of day follows the profile");
        Check(Fr("faire le point ce matin", At(10, 21, 15, 0)).Due == At(10, 22, 12, 0), "a passed moment moves to the next working day");
        Check(Fr("écrire à Paul à 9h", At(10, 23, 19, 0)).Due == At(10, 26, 9, 0), "Friday evening moves to Monday");
        Check(Fr("faire X aujourd'hui à 9h").InvalidDate, "an explicit past time is not applied");

        var kept = Fr("appeler Marc demain après le déjeuner, 20 min #Acme", ignored: new HashSet<string> { PhraseKinds.Duration });
        Check(kept.Minutes is null && kept.Title.Contains("20 min"), "a removed chip goes back into the title");
        Check(Fr("préparer #zzz demain").Space is null && Fr("préparer #zzz demain").Title.Contains("#zzz"), "unknown space stays in the title");
        Check(Fr("préparer le dossier #perso").Space == "perso", "space by title prefix");
        Check(Fr("appeler Marc quand la quinzaine commence").Unclear, "unparsed time words ask for the model");
        Check(!Fr("appeler 3 clients").Unclear && !Fr("appeler 3 clients").Understood, "plain numbers stay in the title");

        var model = TaskPhrase.FromModel("Title: Appeler Marc\nDue: 2026-10-22 15:00\nMinutes: 20", now, day, null);
        Check(model is { FromModel: true, Title: "Appeler Marc", Minutes: 20 } && model.Due == At(10, 22, 15, 0), "model reply is read");
        Check(TaskPhrase.FromModel("Title: X\nDue: 2020-01-01 10:00\nMinutes: none", now, day, null) is null, "model past date rejected");
        Check(TaskPhrase.FromModel("Sure! Here it is.", now, day, null) is null, "model chatter rejected");
        Check(TaskPhrase.FromModel("Title: Appeler Marc\nDue: none\nMinutes: 9000", now, day, null) is null, "model absurd duration rejected");
        Check(TaskPhrase.ModelInstruction(now, day).Contains("12:30"), "model knows the landmarks");

        Console.WriteLine($"{count} C# task phrase checks passed.");
    }
}
