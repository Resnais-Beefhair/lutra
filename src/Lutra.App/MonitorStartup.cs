using Microsoft.Win32;

namespace Lutra.App;

public static class LaunchAtLogin
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private static string InstalledPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Lutra", "Lutra.exe");
    private static string Command => "\"" + InstalledPath + "\" --tray";
    public static bool IsInstalled => string.Equals(Environment.ProcessPath, InstalledPath, StringComparison.OrdinalIgnoreCase);
    public static bool IsEnabled
    {
        get { using var key = Registry.CurrentUser.OpenSubKey(RunKey); return string.Equals(key?.GetValue("Lutra") as string, Command, StringComparison.OrdinalIgnoreCase); }
    }
    public static void SetEnabled(bool enabled)
    {
        if (enabled && !IsInstalled) throw new InvalidOperationException(Ui.T("Enable this from the installed copy of Lutra.", "Ota tämä käyttöön asennetussa Lutrassa."));
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, true);
        if (enabled) key.SetValue("Lutra", Command, RegistryValueKind.String);
        else if (IsEnabled) key.DeleteValue("Lutra", false);
    }
}

public sealed partial class MainWindow
{
    private FrameworkElement StartupSettings()
    {
        bool enabled = false;
        try { enabled = LaunchAtLogin.IsEnabled; } catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException) { }
        var startup = CurveForm.Check(Ui.T("Start monitor when I sign in", "Käynnistä seuranta kirjautuessani"), enabled);
        startup.IsEnabled = !demo && LaunchAtLogin.IsInstalled;
        startup.Click += (_, _) => Guard(() =>
        {
            try { LaunchAtLogin.SetEnabled(startup.IsChecked == true); }
            catch { startup.IsChecked = enabled; throw; }
            enabled = startup.IsChecked == true;
        });
        var panel = Ui.Panel(Ui.Stack(Ui.Text(Ui.T("Alarm notifications", "Hälytysilmoitukset"), 19, bold: true), startup,
            Ui.Text(Ui.T("Starts quietly in the notification area. Desktop alarms need the monitor running and Windows notifications enabled. Cooling and alarm logging continue in the service.",
                "Käynnistyy huomaamatta ilmoitusalueelle. Ilmoitukset tarvitsevat käynnissä olevan seurannan ja Windowsin ilmoitusluvan. Jäähdytys ja hälytysten lokitus toimivat palvelussa."), 12, "#596763")));
        panel.Margin = new(0, 18, 0, 0); return panel;
    }
    internal void StartInTray() => StartMonitoring();
    private bool monitorStarted;
    private async void StartMonitoring()
    {
        if (monitorStarted) return;
        monitorStarted = true; timer.Start(); poll = Poll(); await poll;
    }
}
