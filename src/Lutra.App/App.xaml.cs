namespace Lutra.App;
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, error) =>
        {
            MessageBox.Show(error.Exception.GetBaseException().Message, "Lutra", MessageBoxButton.OK, MessageBoxImage.Error);
            error.Handled = true;
        };
        var window = new MainWindow(e.Args); MainWindow = window; window.Show();
    }
}
