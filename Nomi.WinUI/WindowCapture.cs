using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Nomi;

public sealed record WindowChoice(IntPtr Handle, string Title, string Process);

public static class WindowCapture
{
    private const int ExtendedStyle = -20;
    private const long ToolWindow = 0x80;
    private const uint Owner = 4;
    private const int Cloaked = 14;
    private const uint RenderFullContent = 2;

    private delegate bool EnumProc(IntPtr handle, IntPtr state);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapHeader
    {
        public int Size;
        public int Width;
        public int Height;
        public short Planes;
        public short BitCount;
        public int Compression;
        public int SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public int ClrUsed;
        public int ClrImportant;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumProc callback, IntPtr state);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr handle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr handle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr handle);

    [DllImport("user32.dll", EntryPoint = "GetWindowTextLengthW")]
    private static extern int GetWindowTextLength(IntPtr handle);

    [DllImport("user32.dll", EntryPoint = "GetWindowTextW", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr handle, StringBuilder text, int length);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint process);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr handle, int index);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr handle, uint command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr handle, out Rect rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PrintWindow(IntPtr handle, IntPtr context, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr handle, IntPtr context);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr handle, int attribute, out int value, int size);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr context);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateDIBSection(IntPtr context, ref BitmapHeader header, uint usage, out IntPtr bits, IntPtr section, uint offset);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr context, IntPtr item);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr item);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(IntPtr context);

    public static IReadOnlyList<WindowChoice> List(IntPtr own)
    {
        var current = Environment.ProcessId;
        var found = new List<WindowChoice>();
        EnumWindows((handle, _) =>
        {
            if (handle == own || !IsWindowVisible(handle) || GetWindow(handle, Owner) != IntPtr.Zero) return true;
            if ((GetWindowLongPtr(handle, ExtendedStyle).ToInt64() & ToolWindow) != 0) return true;
            if (DwmGetWindowAttribute(handle, Cloaked, out var cloaked, sizeof(int)) == 0 && cloaked != 0) return true;
            var length = GetWindowTextLength(handle);
            if (length == 0) return true;
            var text = new StringBuilder(length + 1);
            GetWindowText(handle, text, text.Capacity);
            GetWindowThreadProcessId(handle, out var id);
            if (id == current) return true;
            var name = "";
            try { using var process = Process.GetProcessById((int)id); name = process.ProcessName; }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException) { return true; }
            if (name is "TextInputHost" or "ApplicationFrameHost" or "ShellExperienceHost" or "SearchHost" or "StartMenuExperienceHost") return true;
            found.Add(new WindowChoice(handle, text.ToString(), name));
            return true;
        }, IntPtr.Zero);
        return found;
    }

    public static bool Exists(IntPtr handle) => handle != IntPtr.Zero && IsWindow(handle);

    public static ScreenFrame Capture(IntPtr handle)
    {
        if (!Exists(handle)) throw new InferenceException("focusClosed");
        if (IsIconic(handle)) throw new InferenceException("focusMinimized");
        if (!GetWindowRect(handle, out var rect)) throw new InferenceException("focusCaptureError");
        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        if (width < 40 || height < 40 || width > 10000 || height > 10000) throw new InferenceException("focusCaptureError");
        var screen = GetDC(IntPtr.Zero);
        var context = CreateCompatibleDC(screen);
        var header = new BitmapHeader
        {
            Size = Marshal.SizeOf<BitmapHeader>(),
            Width = width,
            Height = -height,
            Planes = 1,
            BitCount = 32
        };
        var bitmap = CreateDIBSection(context, ref header, 0, out var bits, IntPtr.Zero, 0);
        var previous = bitmap == IntPtr.Zero ? IntPtr.Zero : SelectObject(context, bitmap);
        try
        {
            if (bitmap == IntPtr.Zero || !PrintWindow(handle, context, RenderFullContent))
                throw new InferenceException("focusCaptureError");
            var pixels = new byte[width * height * 4];
            Marshal.Copy(bits, pixels, 0, pixels.Length);
            for (var index = 3; index < pixels.Length; index += 4) pixels[index] = 255;
            var frame = new ScreenFrame(width, height, pixels);
            if (frame.IsBlank()) throw new InferenceException("focusBlank");
            return frame;
        }
        finally
        {
            if (previous != IntPtr.Zero) SelectObject(context, previous);
            if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
            DeleteDC(context);
            ReleaseDC(IntPtr.Zero, screen);
        }
    }
}
