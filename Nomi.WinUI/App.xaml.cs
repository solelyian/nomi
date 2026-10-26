using Microsoft.UI.Xaml;

namespace Nomi;

public partial class App : Application
{
    public const string InstanceName = "Nyne.Nomi.Instance";
    public const string ShowName = "Nyne.Nomi.Show";
    private static Mutex? single;
    private MainWindow? window;

    public App()
    {
        UnhandledException += (_, args) => StartupDiagnostics.Report(args.Exception);
        DebugSettings.IsXamlResourceReferenceTracingEnabled = true;
        DebugSettings.XamlResourceReferenceFailed += (_, args) => StartupDiagnostics.Write(args.Message);
        StartupDiagnostics.Write("Initializing application");
        InitializeComponent();
    }

    private static bool First()
    {
        single = new Mutex(true, InstanceName, out var created);
        if (created) return true;
        try { return single.WaitOne(0); }
        catch (AbandonedMutexException) { return true; }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            if (!First())
            {
                StartupDiagnostics.Write("Nomi already running, showing it");
                try
                {
                    using var show = EventWaitHandle.OpenExisting(ShowName);
                    show.Set();
                }
                catch (WaitHandleCannotBeOpenedException) { }
                Exit();
                return;
            }
            StartupDiagnostics.Write("Creating main window");
            window = new MainWindow(AutoStart.Background(Environment.GetCommandLineArgs()));
            if (window.StartsHidden) StartupDiagnostics.Write("Started in the notification area");
            else
            {
                window.Activate();
                StartupDiagnostics.Write("Main window activated");
            }
        }
        catch (Exception error)
        {
            StartupDiagnostics.Report(error);
            Exit();
        }
    }
}
