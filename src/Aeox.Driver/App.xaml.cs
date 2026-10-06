using System.Windows;

namespace Aeox.Driver;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show($"Aeox Driver hit an error and skipped that action.\n\n{args.Exception.Message}", "Aeox Driver",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };
    }
}
