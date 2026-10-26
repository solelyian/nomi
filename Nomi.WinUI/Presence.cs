using System.Runtime.InteropServices;

namespace Nomi;

public sealed class Presence : IDisposable
{
    public const int CommandOpen = 1;
    public const int CommandCapture = 2;
    public const int CommandQuit = 3;
    private const uint TrayMessage = 0x8001;
    private const int HotKeyId = 0x4E4F;
    private const uint WmHotKey = 0x0312;
    private const uint WmLButtonUp = 0x0202;
    private const uint WmRButtonUp = 0x0205;
    private const uint WmContextMenu = 0x007B;
    private const uint NinSelect = 0x0400;
    private const uint NinBalloonUserClick = 0x0405;
    private const uint NimAdd = 0, NimModify = 1, NimDelete = 2, NimSetVersion = 4;
    private const uint NifMessage = 1, NifIcon = 2, NifTip = 4, NifInfo = 0x10, NifShowTip = 0x80;
    private const uint NiifUser = 4, NiifLargeIcon = 0x20;
    private const uint ModAlt = 1, ModControl = 2, ModNoRepeat = 0x4000;
    private const uint VkSpace = 0x20;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint Size;
        public IntPtr Window;
        public uint Id;
        public uint Flags;
        public uint CallbackMessage;
        public IntPtr Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State;
        public uint StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint Version;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string InfoTitle;
        public uint InfoFlags;
        public Guid Item;
        public IntPtr BalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    private delegate IntPtr SubclassProc(IntPtr window, uint message, IntPtr wParam, IntPtr lParam, UIntPtr id, UIntPtr data);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "Shell_NotifyIconW")]
    private static extern bool ShellNotifyIcon(uint message, ref NotifyIconData data);

    [DllImport("comctl32.dll")]
    private static extern bool SetWindowSubclass(IntPtr window, SubclassProc callback, UIntPtr id, UIntPtr data);

    [DllImport("comctl32.dll")]
    private static extern bool RemoveWindowSubclass(IntPtr window, SubclassProc callback, UIntPtr id);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr window, int id);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegisterWindowMessageW")]
    private static extern uint RegisterWindowMessage(string name);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "LoadImageW")]
    private static extern IntPtr LoadImage(IntPtr instance, string name, uint type, int width, int height, uint flags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr icon);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "AppendMenuW")]
    private static extern bool AppendMenu(IntPtr menu, uint flags, UIntPtr id, string? text);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr menu);

    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenuEx(IntPtr menu, uint flags, int x, int y, IntPtr window, IntPtr parameters);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr window, int command);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr window);

    private readonly IntPtr window;
    private readonly SubclassProc procedure;
    private readonly uint taskbarCreated;
    private readonly IntPtr icon;
    private bool added;
    private bool wanted;
    private bool versioned;
    private bool hotKey;
    private string tip = "Nomi";
    private (string Open, string Capture, string Quit) labels = ("Nomi", "+", "×");

    public event Action<int>? Command;
    public event Action? Activated;
    public event Action? BalloonClicked;
    public event Action? HotKeyPressed;

    public Presence(IntPtr window, string iconPath)
    {
        this.window = window;
        procedure = Procedure;
        taskbarCreated = RegisterWindowMessage("TaskbarCreated");
        icon = File.Exists(iconPath) ? LoadImage(IntPtr.Zero, iconPath, 1, 0, 0, 0x10 | 0x40) : IntPtr.Zero;
        SetWindowSubclass(window, procedure, (UIntPtr)1, UIntPtr.Zero);
    }

    public bool HotKeyActive => hotKey;

    public void Labels(string tip, string open, string capture, string quit)
    {
        this.tip = tip;
        labels = (open, capture, quit);
        if (added) Show();
    }

    public void Show()
    {
        wanted = true;
        var data = Data(NifMessage | NifIcon | NifTip | NifShowTip);
        if (ShellNotifyIcon(added ? NimModify : NimAdd, ref data)) added = true;
        if (!added || versioned) return;
        data.Version = 4;
        versioned = ShellNotifyIcon(NimSetVersion, ref data);
    }

    public void Hide()
    {
        wanted = false;
        if (!added) return;
        var data = Data(0);
        ShellNotifyIcon(NimDelete, ref data);
        added = versioned = false;
    }

    public void Notify(string title, string text)
    {
        if (!added) Show();
        if (!added) return;
        var data = Data(NifInfo);
        data.InfoTitle = title.Length > 63 ? title[..63] : title;
        data.Info = text.Length > 255 ? text[..255] : text;
        data.InfoFlags = icon == IntPtr.Zero ? 1u : NiifUser | NiifLargeIcon;
        data.BalloonIcon = icon;
        ShellNotifyIcon(NimModify, ref data);
    }

    public bool SetHotKey(bool on)
    {
        if (on && !hotKey) hotKey = RegisterHotKey(window, HotKeyId, ModControl | ModAlt | ModNoRepeat, VkSpace);
        else if (!on && hotKey)
        {
            UnregisterHotKey(window, HotKeyId);
            hotKey = false;
        }
        return hotKey == on;
    }

    public void Bring()
    {
        if (IsIconic(window)) ShowWindow(window, 9);
        SetForegroundWindow(window);
    }

    private NotifyIconData Data(uint flags) => new()
    {
        Size = (uint)Marshal.SizeOf<NotifyIconData>(),
        Window = window,
        Id = 1,
        Flags = flags,
        CallbackMessage = TrayMessage,
        Icon = icon,
        Tip = tip,
        Info = "",
        InfoTitle = ""
    };

    private IntPtr Procedure(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam, UIntPtr id, UIntPtr data)
    {
        if (message == TrayMessage)
        {
            var happened = (uint)((long)lParam & 0xFFFF);
            if (happened == NinSelect || !versioned && happened == WmLButtonUp) Activated?.Invoke();
            else if (happened == WmContextMenu || !versioned && happened == WmRButtonUp) Menu();
            else if (happened == NinBalloonUserClick) BalloonClicked?.Invoke();
            return IntPtr.Zero;
        }
        if (message == WmHotKey && (int)wParam == HotKeyId)
        {
            HotKeyPressed?.Invoke();
            return IntPtr.Zero;
        }
        if (message == taskbarCreated && wanted)
        {
            added = versioned = false;
            Show();
        }
        return DefSubclassProc(hwnd, message, wParam, lParam);
    }

    private void Menu()
    {
        var menu = CreatePopupMenu();
        AppendMenu(menu, 0, (UIntPtr)CommandOpen, labels.Open);
        AppendMenu(menu, 0, (UIntPtr)CommandCapture, labels.Capture);
        AppendMenu(menu, 0x800, UIntPtr.Zero, null);
        AppendMenu(menu, 0, (UIntPtr)CommandQuit, labels.Quit);
        GetCursorPos(out var point);
        SetForegroundWindow(window);
        var chosen = TrackPopupMenuEx(menu, 0x0100 | 0x0002, point.X, point.Y, window, IntPtr.Zero);
        DestroyMenu(menu);
        if (chosen != 0) Command?.Invoke((int)chosen);
    }

    public void Dispose()
    {
        SetHotKey(false);
        if (added)
        {
            var data = Data(0);
            ShellNotifyIcon(NimDelete, ref data);
            added = false;
        }
        RemoveWindowSubclass(window, procedure, (UIntPtr)1);
        if (icon != IntPtr.Zero) DestroyIcon(icon);
    }
}
