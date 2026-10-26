using Microsoft.Win32;

namespace Nomi;

public static class AutoStart
{
    public const string Flag = "--background";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Nomi";

    public static string Command(string exe) => $"\"{exe}\" {Flag}";

    public static bool Background(IEnumerable<string> args) => args.Any(arg => arg.Equals(Flag, StringComparison.OrdinalIgnoreCase));

    public static bool Enabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                return key?.GetValue(ValueName) is string value && Environment.ProcessPath is { } exe
                    && value.Equals(Command(exe), StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception error) when (error is System.Security.SecurityException or UnauthorizedAccessException or IOException)
            {
                return false;
            }
        }
    }

    public static bool Set(bool on)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (on && Environment.ProcessPath is { } exe) key.SetValue(ValueName, Command(exe));
            else key.DeleteValue(ValueName, false);
            return true;
        }
        catch (Exception error) when (error is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }
}
