namespace Nomi;

public static class CallModes
{
    public const string Ask = "ask";
    public const string Problem = "problem";
    public const string Comment = "comment";
    public static readonly string[] All = [Ask, Problem, Comment];
}

// One caption of the call. Seen is the time of the image the answer is about.
public sealed class CallLine(string who, string text, DateTimeOffset at, DateTimeOffset? seen = null, string source = "", bool verified = false)
{
    public string Who { get; } = who;
    public string Text { get; set; } = text;
    public DateTimeOffset At { get; } = at;
    public DateTimeOffset? Seen { get; set; } = seen;
    public string Source { get; } = source;
    public bool Verified { get; set; } = verified;
    public bool Live { get; set; }
    public CallPoint? Point { get; init; }
}

// A figure Nomi flagged during the call, recalculated by the table check rather than read from the model.
public sealed class CallPoint(string key, TableIssue issue, DateTimeOffset at, string window)
{
    public string Key { get; } = key;
    public TableIssue Issue { get; } = issue;
    public DateTimeOffset At { get; } = at;
    public string Window { get; } = window;
    public bool Fixed { get; set; }
}

public sealed record WatchOutcome(IReadOnlyList<CallPoint> Found, IReadOnlyList<CallPoint> Fixed, bool TaskChanged);

// The state of a screen-sharing call: when to look again, what Nomi says on its own, and the summary at the end.
public sealed class FocusCall(string window, DateTimeOffset started, string mode)
{
    public const double Change = 0.015;
    public static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(8);
    private const int Kept = 200;
    private byte[]? previous;
    private byte[]? analyzed;
    private string task = "";

    public string Window { get; set; } = window;
    public DateTimeOffset Started { get; } = started;
    public DateTimeOffset? Ended { get; private set; }
    public string Mode { get; set; } = CallModes.All.Contains(mode) ? mode : CallModes.Problem;
    public bool Paused { get; set; }
    public List<CallLine> Lines { get; } = [];
    public List<CallPoint> Points { get; } = [];
    public int Questions { get; private set; }
    public DateTimeOffset? LastAnalysis { get; private set; }
    public DateTimeOffset? LastSeen { get; private set; }
    public string Task => task;

    public TimeSpan Duration(DateTimeOffset now) => (Ended ?? now) - Started;

    // Looks again only once the screen has settled on something new, and not more often than the cooldown.
    public bool ShouldWatch(byte[] print, DateTimeOffset now)
    {
        var stable = ScreenFrame.Difference(previous, print) < Change;
        previous = print;
        if (Paused || Ended is not null || Mode == CallModes.Ask || !stable) return false;
        if (analyzed is not null && ScreenFrame.Difference(analyzed, print) < Change) return false;
        return LastAnalysis is not { } last || now - last >= Cooldown;
    }

    public void Seen(DateTimeOffset at) => LastSeen = at;

    // A new screen change (a scroll, another window) is looked at again even if the last one was analysed.
    public void Forget() => analyzed = null;

    public WatchOutcome Watched(byte[] print, DateTimeOffset seen, ScreenInsight insight, TableReport? report)
    {
        analyzed = print;
        LastAnalysis = seen;
        LastSeen = seen;
        var issues = report?.Issues ?? [];
        var found = new List<CallPoint>();
        foreach (var issue in issues)
        {
            var key = Key(issue);
            if (Points.Any(point => point.Key == key && point.Window == Window && !point.Fixed)) continue;
            var point = new CallPoint(key, issue, seen, Window);
            Points.Add(point);
            found.Add(point);
        }
        var corrected = new List<CallPoint>();
        if (report is { Rows: >= 2 })
        {
            foreach (var point in Points.Where(point => !point.Fixed && point.Window == Window && !found.Contains(point)))
            {
                var still = issues.Any(issue => issue.Row == point.Issue.Row && issue.Column == point.Issue.Column);
                var visible = insight.Rows.Any(row => row.Any(cell => cell.Contains(point.Issue.Row, StringComparison.OrdinalIgnoreCase)));
                var checkedAgain = report.Checked?.Contains(point.Issue.Formula) == true;
                if (still || !visible || !checkedAgain) continue;
                point.Fixed = true;
                corrected.Add(point);
            }
        }
        var changed = insight.Task.Length > 0 && !string.Equals(insight.Task, task, StringComparison.OrdinalIgnoreCase);
        if (insight.Task.Length > 0) task = insight.Task;
        return new(found, corrected, changed);
    }

    // Whether Nomi speaks on its own after an analysis, depending on the chosen mode.
    public bool Speaks(WatchOutcome outcome) => Mode switch
    {
        CallModes.Ask => false,
        CallModes.Comment => outcome.Found.Count > 0 || outcome.Fixed.Count > 0 || outcome.TaskChanged,
        _ => outcome.Found.Count > 0 || outcome.Fixed.Count > 0
    };

    public CallLine Asked(string text, string source, DateTimeOffset at)
    {
        Questions++;
        return Add(new CallLine("you", text, at, null, source));
    }

    public CallLine Add(CallLine line)
    {
        Lines.Add(line);
        if (Lines.Count > Kept) Lines.RemoveRange(0, Lines.Count - Kept);
        return line;
    }

    public void End(DateTimeOffset at) => Ended ??= at;

    public static string Key(TableIssue issue) => $"{issue.Row}|{issue.Column}|{issue.Expected}|{issue.Shown}";
}
