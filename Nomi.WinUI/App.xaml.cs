using Microsoft.UI.Xaml;

namespace Nomi;

public partial class App : Application
{
    private Window? window;

    public App()
    {
        UnhandledException += (_, args) => StartupDiagnostics.Report(args.Exception);
        StartupDiagnostics.Write("Initializing application");
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            StartupDiagnostics.Write("Creating main window");
            window = new MainWindow();
            window.Activate();
            StartupDiagnostics.Write("Main window activated");
        }
        catch (Exception error)
        {
            StartupDiagnostics.Report(error);
            Exit();
        }
    }
}
