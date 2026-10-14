namespace Lutra.App;
public partial class App : Application
{
    private Mutex? instance;
    private EventWaitHandle? activation;
    private RegisteredWaitHandle? activationWait;
    private bool ownsInstance;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (!e.Args.Contains("--capture") && !e.Args.Contains("--demo"))
        {
            string name = @"Local\Lutra.Monitor." + System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value;
            instance = new Mutex(false, name);
            try { ownsInstance = instance.WaitOne(0); } catch (AbandonedMutexException) { ownsInstance = true; }
            if (!ownsInstance)
            {
                if (!e.Args.Contains("--tray") && EventWaitHandle.TryOpenExisting(name + ".Activate", out var existing))
                    using (existing) existing.Set();
                Shutdown(); return;
            }
            activation = new EventWaitHandle(false, EventResetMode.AutoReset, name + ".Activate");
            activationWait = ThreadPool.RegisterWaitForSingleObject(activation, (_, _) => Dispatcher.BeginInvoke(new Action(() =>
            {
                if (MainWindow is { } current) { current.Show(); current.WindowState = WindowState.Normal; current.Activate(); }
            })), null, Timeout.Infinite, false);
        }
        DispatcherUnhandledException += (_, error) =>
        {
            MessageBox.Show(error.Exception.GetBaseException().Message, "Lutra", MessageBoxButton.OK, MessageBoxImage.Error);
            error.Handled = true;
        };
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var window = new MainWindow(e.Args); MainWindow = window;
        if (e.Args.Contains("--tray") && !e.Args.Contains("--capture")) window.StartInTray(); else window.Show();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        activationWait?.Unregister(null); activation?.Dispose();
        if (ownsInstance) instance?.ReleaseMutex();
        instance?.Dispose(); base.OnExit(e);
    }
}
