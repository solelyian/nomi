using System.Runtime.InteropServices;

namespace Nomi;

internal static class StartupDiagnostics
{
    internal static string DirectoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nyne", "Nomi", "logs");

    internal static void Write(string message)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            var path = Path.Combine(DirectoryPath, "startup.log");
            if (File.Exists(path) && new FileInfo(path).Length > 256_000) File.Delete(path);
            File.AppendAllText(path, $"{DateTimeOffset.UtcNow:O} {message}{Environment.NewLine}");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }

    internal static void Report(Exception error)
    {
        Write(error.ToString());
        MessageBox(IntPtr.Zero,
            $"Nomi n’a pas pu démarrer / Nomi could not start.\n\n{error.Message}\n\nJournal / Log: {DirectoryPath}",
            "Nomi", 0x10);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxW")]
    private static extern int MessageBox(IntPtr window, string text, string caption, uint type);
}
