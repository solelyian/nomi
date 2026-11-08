using System.Globalization;
using System.Text;

namespace Nomi;

public static class DraftKinds
{
    public const string Mail = "mail";
    public const string Message = "message";
    public const string Outline = "outline";
    public const string Plan = "plan";
    public const string Checklist = "checklist";
    public static readonly string[] All = [Mail, Message, Outline, Plan, Checklist];
}

public static class FirstStep
{
    public const int NoteLimit = 1500;
    private static readonly string Line = ((char)10).ToString();

    private static readonly (string Kind, string[] Words)[] Keywords =
    [
        (DraftKinds.Mail, ["mail", "courriel", "écrire à", "ecrire a", "répondre à", "repondre a", "relancer", "reply", "write to", "follow up", "follow-up", "newsletter"]),
        (DraftKinds.Message, ["message", "slack", "teams", "sms", "texto", "prévenir", "prevenir", "whatsapp", "ping", "let know", "tell "]),
        (DraftKinds.Outline, ["rapport", "document", "article", "présentation", "presentation", "diapo", "slides", "compte rendu", "compte-rendu", "report", "proposal", "proposition", "mémo", "memo", "dossier", "rédiger", "rediger", "draft", "write up", "devis", "quote"]),
        (DraftKinds.Plan, ["plan", "organiser", "organise", "organize", "préparer", "preparer", "prepare", "projet", "project", "atelier", "workshop", "réunion", "reunion", "meeting", "point équipe", "lancer", "launch", "événement", "event"])
    ];

    public static string Kind(string title, string note)
    {
        var text = $" {title} {note} ".ToLower(CultureInfo.InvariantCulture);
        foreach (var (kind, words) in Keywords)
            if (words.Any(word => Contains(text, word))) return kind;
        return DraftKinds.Checklist;
    }

    private static bool Contains(string text, string word)
    {
        var index = text.IndexOf(word, StringComparison.Ordinal);
        while (index >= 0)
        {
            var before = index == 0 || !char.IsLetter(text[index - 1]);
            var end = index + word.Length;
            var after = end >= text.Length || !char.IsLetter(text[end]) || word.EndsWith(' ');
            if (before && after) return true;
            index = text.IndexOf(word, index + 1, StringComparison.Ordinal);
        }
        return false;
    }

    public static string Target(WorkTask task) =>
        task.Steps.FirstOrDefault(step => !step.Done)?.Title is { Length: > 0 } step ? step : task.Title;

    public static string Prompt(WorkTask task, string? space, DateTimeOffset now)
    {
        var text = new StringBuilder();
        text.Append("Task: ").Append(task.Title.Trim()).Append(Line);
        text.Append("First step to write: ").Append(Target(task).Trim()).Append(Line);
        if (task.Due is DateTimeOffset due)
            text.Append("Deadline: ").Append(due.ToString("dddd yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))
                .Append(due < now ? " (already passed)" : "").Append(Line);
        if (space is { Length: > 0 }) text.Append("Space: ").Append(space).Append(Line);
        var open = task.Steps.Where(step => !step.Done).Skip(1).Take(5).Select(step => step.Title.Trim()).ToArray();
        if (open.Length > 0) text.Append("Later steps: ").Append(string.Join("; ", open)).Append(Line);
        var note = task.Note.Trim();
        if (note.Length > NoteLimit) note = note[..NoteLimit];
        if (note.Length > 0) text.Append("Notes from the user:").Append(Line).Append(note).Append(Line);
        return text.ToString().Trim();
    }

    public static string Instruction(string kind)
    {
        var shape = kind switch
        {
            DraftKinds.Mail => "Write a ready-to-send email: a first line starting with the word for Subject in the reply language, then the body with a greeting and a sign-off.",
            DraftKinds.Message => "Write a short chat message of two to four sentences, ready to send.",
            DraftKinds.Outline => "Write the outline of the document: a title, then four to seven headings, each with one line describing what goes there.",
            DraftKinds.Plan => "Write a short plan: the goal in one sentence, then three to six ordered actions, each with who or what is needed.",
            _ => "Write a checklist of three to seven concrete items to prepare before starting."
        };
        return "You are writing a first draft so the user can start this task right away. Write only the draft itself, with no introduction, no comment about the draft and no question. "
            + shape
            + " Use only the facts given. Put any unknown name, date, figure or detail in square brackets, for example [date]. Never invent numbers, prices, deadlines or commitments.";
    }
}
