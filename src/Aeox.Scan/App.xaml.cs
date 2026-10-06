using System.Windows;

namespace Aeox.Scan;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show($"Aeox Scan hit an error and skipped that action.\n\n{args.Exception.Message}", "Aeox Scan",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };
    }
}
