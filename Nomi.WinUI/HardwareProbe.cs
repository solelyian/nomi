using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Nomi;

public static class HardwareProbe
{
    private static readonly Guid ComputeAccelerator = new("f01a9d53-3ff6-48d2-9f97-c8a7004be10c");
    private const uint PresentOnly = 0x2;
    private const uint DeviceDescription = 0x00;
    private const uint FriendlyName = 0x0C;

    public static HardwareReport Detect(string dataDirectory) => new(
        Processor(), Environment.ProcessorCount,
        RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant(),
        Memory(), FreeDisk(dataDirectory), Npus(),
        OperatingSystem.IsWindowsVersionAtLeast(10, 0, 26100));

    private static string Processor()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            return Regex.Replace(key?.GetValue("ProcessorNameString") as string ?? "", @"\s+", " ").Trim();
        }
        catch (Exception error) when (error is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return "";
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length, Load;
        public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile,
            TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);

    private static long Memory()
    {
        var status = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        return GlobalMemoryStatusEx(ref status) ? (long)Math.Min(status.TotalPhysical, long.MaxValue) : 0;
    }

    private static long FreeDisk(string directory)
    {
        try { return new DriveInfo(Path.GetPathRoot(Path.GetFullPath(directory))!).AvailableFreeSpace; }
        catch (Exception error) when (error is IOException or ArgumentException or UnauthorizedAccessException) { return 0; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DeviceInfo
    {
        public uint Size;
        public Guid ClassGuid;
        public uint Instance;
        public IntPtr Reserved;
    }

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevsW(ref Guid classGuid, IntPtr enumerator, IntPtr parent, uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiEnumDeviceInfo(IntPtr set, uint index, ref DeviceInfo data);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDeviceRegistryPropertyW(
        IntPtr set, ref DeviceInfo data, uint property, out uint type, byte[]? buffer, uint size, out uint required);

    [DllImport("setupapi.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);

    private static IReadOnlyList<string> Npus()
    {
        var names = new List<string>();
        var guid = ComputeAccelerator;
        var set = SetupDiGetClassDevsW(ref guid, IntPtr.Zero, IntPtr.Zero, PresentOnly);
        if (set == IntPtr.Zero || set == new IntPtr(-1)) return names;
        try
        {
            for (uint index = 0; index < 16; index++)
            {
                var data = new DeviceInfo { Size = (uint)Marshal.SizeOf<DeviceInfo>() };
                if (!SetupDiEnumDeviceInfo(set, index, ref data)) break;
                var name = Property(set, ref data, FriendlyName) ?? Property(set, ref data, DeviceDescription);
                if (!string.IsNullOrWhiteSpace(name) && !names.Contains(name)) names.Add(name);
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(set);
        }
        return names;
    }

    private static string? Property(IntPtr set, ref DeviceInfo data, uint property)
    {
        var buffer = new byte[512];
        if (!SetupDiGetDeviceRegistryPropertyW(set, ref data, property, out _, buffer, (uint)buffer.Length, out var required)
            || required < 2) return null;
        return System.Text.Encoding.Unicode.GetString(buffer, 0, (int)Math.Min(required, (uint)buffer.Length)).TrimEnd('\0').Trim();
    }
}
