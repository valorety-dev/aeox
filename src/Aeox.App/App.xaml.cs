using System.Globalization;
using System.Threading;
using System.Windows;

namespace Aeox.App;

public partial class App : Application
{
    private const string InstanceName = "Aeox.SingleInstance";
    private const string ShowSignalName = "Aeox.ShowWindow";
    private Mutex? _instance;
    private EventWaitHandle? _showSignal;

    protected override void OnStartup(StartupEventArgs e)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        if (e.Args.Length == 2 && e.Args[0] == Aeox.Core.Changes.Elevation.Arg)
        {
            Shutdown(Aeox.Core.Changes.Elevation.RunFromFile(e.Args[1]));
            return;
        }
        _instance = new Mutex(true, InstanceName, out var isFirst);
        if (!isFirst)
        {
            if (EventWaitHandle.TryOpenExisting(ShowSignalName, out var existing)) existing.Set();
            Shutdown();
            return;
        }

        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            LogError(args.Exception);
            MessageBox.Show($"Aeox hit an error and skipped that action.\n\n{args.Exception.Message}\n\nDetails are in {ErrorLogPath}",
                "Aeox", MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) => LogError(args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            LogError(args.Exception);
            args.SetObserved();
        };
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var window = new MainWindow();
        MainWindow = window;
        if (!e.Args.Contains("--tray", StringComparer.OrdinalIgnoreCase)) window.Show();

        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSignalName);
        var listener = new Thread(() =>
        {
            while (_showSignal.WaitOne())
            {
                Dispatcher.Invoke(window.ShowFromTray);
            }
        }) { IsBackground = true };
        listener.Start();
    }

    private static string ErrorLogPath => System.IO.Path.Combine(Aeox.Core.Tweaks.AeoxContext.DefaultDataDir(), "error.log");

    private static void LogError(Exception? ex)
    {
        if (ex is null) return;
        try
        {
            System.IO.Directory.CreateDirectory(Aeox.Core.Tweaks.AeoxContext.DefaultDataDir());
            System.IO.File.AppendAllText(ErrorLogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (System.IO.IOException)
        {
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _instance?.ReleaseMutex();
        _instance?.Dispose();
        base.OnExit(e);
    }
}
