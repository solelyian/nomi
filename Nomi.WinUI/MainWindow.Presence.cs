using System.Globalization;
using Microsoft.UI.Xaml;

namespace Nomi;

public sealed partial class MainWindow
{
    private Presence? presence;
    private EventWaitHandle? showSignal;
    private bool quitting;
    private bool hidden;
    private bool windowActive = true;
    private bool hotKeyTaken;
    private string presenceLanguage = "";
    private string? pendingTarget;

    public bool StartsHidden { get; private set; }

    private bool Away => hidden || !windowActive;

    private void StartPresence(bool background)
    {
        var handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        presence = new Presence(handle, Path.Combine(AppContext.BaseDirectory, "Assets", "nomi.ico"));
        presence.Command += command =>
        {
            if (command == Presence.CommandOpen) Summon(null);
            else if (command == Presence.CommandCapture) Capture();
            else if (command == Presence.CommandQuit) Quit();
        };
        presence.Activated += () => Summon(null);
        presence.BalloonClicked += () => Summon(pendingTarget);
        presence.HotKeyPressed += Capture;
        ApplyPresence();
        showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, App.ShowName);
        var signal = showSignal;
        new Thread(() =>
        {
            while (signal.WaitOne())
            {
                if (quitting) return;
                DispatcherQueue.TryEnqueue(() => Summon(null));
            }
        }) { IsBackground = true, Name = "Nomi show signal" }.Start();
        AppWindow.Closing += (_, args) =>
        {
            if (quitting || !profile.KeepRunning || presence is null) return;
            args.Cancel = true;
            HideToTray();
        };
        Activated += (_, args) => windowActive = args.WindowActivationState != WindowActivationState.Deactivated;
        StartsHidden = background && profile.KeepRunning;
        if (!StartsHidden) return;
        hidden = true;
        StartBackground();
    }

    private void StartBackground()
    {
        if (ticker is null) StartTicker();
        _ = CheckForUpdates(false);
        _ = RefreshAgenda(false);
    }

    private void ApplyPresence()
    {
        if (presence is null) return;
        presenceLanguage = language;
        presence.Labels(T("trayTip"), T("trayOpen"), T("trayCapture"), T("trayQuit"));
        if (profile.KeepRunning) presence.Show();
        else presence.Hide();
        hotKeyTaken = !presence.SetHotKey(profile.CaptureHotKey);
    }

    private void HideToTray()
    {
        board.Save();
        routines.Save();
        CloseSheet();
        hidden = true;
        AppWindow.Hide();
        if (profile.TrayHintShown || presence is null) return;
        presence.Notify(T("trayHintTitle"), T("trayHint"));
        profile.TrayHintShown = true;
        SaveProfile();
    }

    private void Summon(string? target)
    {
        if (target is not null && target != view) Show(target);
        hidden = false;
        AppWindow.Show();
        Activate();
        presence?.Bring();
    }

    private void Capture()
    {
        Summon(null);
        CloseSheet();
        if (view != "board") Show("board");
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, FocusQuickAdd);
    }

    private void Quit()
    {
        quitting = true;
        showSignal?.Set();
        Close();
    }

    private void NotifyAway(string title, string text, string target)
    {
        if (presence is null || !profile.KeepRunning || !profile.Notifications || !Away || Busy) return;
        pendingTarget = target;
        presence.Notify(title, text);
    }

    private string CaptureKeys => string.Format(CultureInfo.InvariantCulture, "Ctrl Alt {0}", language == "fr" ? "Espace" : "Space");
}
