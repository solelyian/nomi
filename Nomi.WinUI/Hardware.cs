namespace Nomi;

public sealed record HardwareReport(
    string Processor, int Cores, string Architecture, long Memory, long FreeDisk,
    IReadOnlyList<string> Npus, bool NpuSupported);

public static class Hardware
{
    public const long Gigabyte = 1L << 30;
    public const long MinimumMemory = 7 * Gigabyte;
    public const long DiskMargin = Gigabyte;

    public static string Verdict(HardwareReport report, long modelBytes)
    {
        if (DiskShortfall(report, modelBytes) > 0) return "disk";
        if (report.Npus.Count > 0) return report.NpuSupported ? "npu" : "npu-windows";
        return report.Memory > 0 && report.Memory < MinimumMemory ? "cpu-memory" : "cpu";
    }

    public static long DiskShortfall(HardwareReport report, long modelBytes) =>
        modelBytes <= 0 || report.FreeDisk <= 0 ? 0 : Math.Max(0, modelBytes + DiskMargin - report.FreeDisk);

    public static int Gigabytes(long bytes) => bytes <= 0 ? 0 : Math.Max(1, (int)Math.Round((double)bytes / Gigabyte));
}
