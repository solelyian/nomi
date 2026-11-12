namespace Nomi;

public sealed record LocalResource(string Id, string Name, long Bytes, bool Present, bool Busy, string Stage, double Fraction);

// The files Nomi needs on the computer (the reasoning model and the model that reads images), seen as one download.
public static class LocalResources
{
    public const string Ready = "ready";
    public const string Busy = "busy";
    public const string Paused = "model-paused";
    public const string Missing = "missing";
    public const string Downloading = "model-downloading";

    private static readonly string[] Quiet =
        ["local-model-missing", "local-model-ready", "vision-idle", Paused, "model-verifying", "model-loading", Downloading];

    public static long MissingBytes(IEnumerable<LocalResource> items) => items.Where(item => !item.Present).Sum(item => item.Bytes);

    public static LocalResource? Active(IEnumerable<LocalResource> items) => items.FirstOrDefault(item => item.Busy);

    public static string? Error(IEnumerable<LocalResource> items) =>
        items.FirstOrDefault(item => !item.Busy && !Quiet.Contains(item.Stage))?.Stage;

    // Share of the bytes already on the computer, across every file.
    public static double Fraction(IReadOnlyCollection<LocalResource> items)
    {
        var total = items.Sum(item => (double)item.Bytes);
        if (total <= 0) return items.All(item => item.Present) ? 1 : 0;
        var done = items.Sum(item => item.Present ? item.Bytes
            : item.Busy && item.Stage == Downloading ? item.Bytes * Math.Clamp(item.Fraction, 0, 1) : 0);
        return Math.Clamp(done / total, 0, 1);
    }

    public static string State(IReadOnlyCollection<LocalResource> items)
    {
        if (items.Any(item => item.Busy)) return Busy;
        if (Error(items) is { } error) return error;
        if (items.All(item => item.Present)) return Ready;
        return items.Any(item => !item.Present && item.Stage == Paused) ? Paused : Missing;
    }
}
