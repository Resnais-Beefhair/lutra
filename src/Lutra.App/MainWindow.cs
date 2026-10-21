using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Lutra.Hardware;
using Forms = System.Windows.Forms;

namespace Lutra.App;

public sealed partial class MainWindow : Window
{
    private readonly bool demo;
    private readonly string[] arguments;
    private readonly string local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lutra");
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private HardwareSession? monitor;
    private RuntimeState state = new(DateTimeOffset.UtcNow, "Connecting", new([], [], []), [], [], [], new());
    private AppConfiguration configuration = new();
    private AppConfiguration active = new();
    private bool configured, dirty, busy, closing, canApply;
    private string page = "overview";
    private string? selectedCurve;
    private string? selectedHistory;
    private TimeSpan historyRange = TimeSpan.FromMinutes(15);
    private CurveForm? curveForm;
    private Action? captureForm;
    private TextBlock status = new(), feedback = new(), draftLabel = new();
    private HistoryPlot? historyPlot;
    private TextBlock? historySummary;
    private Button? apply;
    private DataGrid? readingGrid;
    private readonly ObservableCollection<ReadingRow> rows = [];
    private readonly Queue<TelemetryFrame> recent = new();
    private readonly HashSet<string> announced = [];
    private readonly Dictionary<string, TextBlock> alarmLabels = [];
    private readonly Forms.NotifyIcon tray;
    private Task? poll;
    private int historyGeneration;
    public MainWindow(string[] args)
    {
        arguments = args; demo = args.Contains("--demo"); Title = "Lutra"; Width = 1440; Height = 920; MinWidth = 1120; MinHeight = 720;
        Style = (Style)Application.Current.FindResource(typeof(Window));
        SourceInitialized += (_, _) => { int enabled = 1; _ = DwmSetWindowAttribute(new System.Windows.Interop.WindowInteropHelper(this).Handle, 20, ref enabled, sizeof(int)); };
        Icon = BitmapFrame.Create(new Uri("pack://application:,,,/Lutra;component/lutra.ico"));
        try { Ui.Language = File.Exists(Path.Combine(local, "language.txt")) ? File.ReadAllText(Path.Combine(local, "language.txt")).Trim() : "en"; } catch (IOException) { }
        if (Ui.Language != "fi") Ui.Language = "en";
        if (Value("--language") is { } language) Ui.Language = language == "fi" ? "fi" : "en";
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(Ui.Language == "fi" ? "fi-FI" : "en-GB");
        if (Value("--width") is { } width) Width = double.Parse(width, CultureInfo.InvariantCulture);
        if (Value("--height") is { } height) Height = double.Parse(height, CultureInfo.InvariantCulture);
        if (Value("--page") is { } requestedPage) page = requestedPage;
        if (Value("--update-result") is { } job) { updateMessage = AppUpdate.ReadUpdateResult(job) ?? Ui.T("Update result is unavailable. Check the installed version.", "Päivityksen tulos ei ole saatavilla. Tarkista asennettu versio."); page = "about"; }
        if (demo) { active = configuration = Demo.Configuration(); configured = true; state = Demo.State(configuration); }
        tray = new Forms.NotifyIcon { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!), Text = "Lutra", Visible = !args.Contains("--capture") };
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(() => { Show(); WindowState = WindowState.Normal; Activate(); });
        Render();
        timer.Tick += async (_, _) => { if (poll == null || poll.IsCompleted) { poll = Poll(); await poll; } };
        Loaded += async (_, _) =>
        {
            if (args.Contains("--capture"))
            {
                await Poll(); await LoadMonitorHistory(); await LoadHistory(); await Task.Delay(200); Capture(Value("--capture")!); closing = true; Close(); return;
            }
            StartMonitoring();
        };
        Closing += (_, e) =>
        {
            if (!closing) { e.Cancel = true; Hide(); tray.ShowBalloonTip(3000, "Lutra", Ui.T("Still monitoring in the notification area.", "Seuranta jatkuu ilmoitusalueella."), Forms.ToolTipIcon.Info); }
        };
        Closed += async (_, _) => { timer.Stop(); if (poll != null) await poll; monitor?.Dispose(); tray.Dispose(); Application.Current.Shutdown(); };
    }
    private string? Value(string arg) { int index = Array.IndexOf(arguments, arg); return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null; }
    private void Render()
    {
        Ui.Language = Ui.Language == "fi" ? "fi" : "en";
        curveForm = null; captureForm = null; readingGrid = null; historyPlot = null; historySummary = null;
        gpuRefreshButton = null; gpuRefreshStatus = null;
        temperatureTraces = null; speedTraces = null;
        alarmLabels.Clear();
        var root = new Grid { Background = Ui.Color("#F0F2F0") }; root.ColumnDefinitions.Add(new() { Width = new(176) }); root.ColumnDefinitions.Add(new());
        var rail = new DockPanel { Background = Ui.Color("#252E2C") };
        var brand = Ui.Row(Ui.Mark(32), Ui.Text("Lutra", 25, "#FFFFFF", true)); brand.Margin = new(22, 25, 0, 28); DockPanel.SetDock(brand, Dock.Top); rail.Children.Add(brand);
        var railFooter = Ui.Stack(Ui.Text("Lutra " + AppUpdate.CurrentVersion, 12, "#D0DDD7"), Ui.Text(Ui.T("Cooling control", "Jäähdytyksen ohjaus"), 11, "#B4C6BE"));
        railFooter.Margin = new(23, 0, 10, 22); DockPanel.SetDock(railFooter, Dock.Bottom); rail.Children.Add(railFooter);
        var nav = new StackPanel { Margin = new(12, 0, 12, 0) };
        foreach (var (key, english, finnish) in new[] { ("overview", "Overview", "Yleiskuva"), ("curves", "Control curves", "Säätökäyrät"), ("alarms", "Alarms", "Hälytykset"), ("history", "History", "Historia"), ("devices", "Devices", "Laitteet"), ("settings", "Settings", "Asetukset"), ("about", "About & updates", "Tietoja ja päivitykset") })
        {
            string target = key;
            var button = Ui.Button(Ui.T(english, finnish), () => Guard(() => { CaptureForm(); page = target; Render(); }));
            button.Foreground = Brushes.White; button.Background = key == page ? Ui.Color("#43564F") : Brushes.Transparent;
            button.BorderBrush = Brushes.Transparent; button.HorizontalContentAlignment = HorizontalAlignment.Left; button.Margin = new(0, 0, 0, 5); button.Padding = new(12, 11, 8, 11);
            nav.Children.Add(button);
        }
        rail.Children.Add(nav); root.Children.Add(rail);
        var body = new Grid { Margin = new(30, 24, 30, 16) };
        body.RowDefinitions.Add(new() { Height = GridLength.Auto }); body.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) }); body.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var top = new DockPanel { LastChildFill = true, Margin = new(0, 0, 0, 23) };
        var indicator = Ui.Stack(status = Ui.Text("", 12, "#256450", true), draftLabel = Ui.Text("", 11, "#596763"));
        indicator.HorizontalAlignment = HorizontalAlignment.Right; indicator.MaxWidth = 300; DockPanel.SetDock(indicator, Dock.Right); top.Children.Add(indicator);
        var title = page switch { "curves" => Ui.T("Control curves", "Säätökäyrät"), "alarms" => Ui.T("Alarms", "Hälytykset"),
            "history" => Ui.T("History", "Historia"), "devices" => Ui.T("Devices", "Laitteet"), "settings" => Ui.T("Settings", "Asetukset"), "about" => Ui.T("About & updates", "Tietoja ja päivitykset"), _ => Ui.T("Overview", "Yleiskuva") };
        var subtitle = page switch { "curves" => Ui.T("A temperature in. The right cooling out.", "Lämpötilasta sopivaan jäähdytystehoon."),
            "alarms" => Ui.T("Know when a temperature needs attention.", "Huomaa lämpötilat, jotka vaativat huomiota."),
            "history" => Ui.T("Read the pattern behind the current value.", "Tarkastele mittausten kehitystä."),
            "devices" => Ui.T("Connected hardware and available controls.", "Liitetyt laitteet ja käytettävissä olevat lähdöt."),
            "settings" => Ui.T("Language, service and local storage.", "Kieli, palvelu ja paikallinen tallennus."),
            "about" => Ui.T("Your version, the project and what is new.", "Sovelluksen versio, projekti ja uutuudet."),
            _ => Ui.T("Your cooling loop, at a glance.", "Jäähdytyksen tilanne yhdellä silmäyksellä.") };
        top.Children.Add(Ui.Stack(Ui.Text(title, 30, bold: true), Ui.Text(subtitle, 13, "#596763"))); body.Children.Add(top);
        FrameworkElement view = page switch { "curves" => CurvesView(), "alarms" => AlarmsView(), "history" => HistoryView(), "devices" => DevicesView(), "settings" => SettingsView(), "about" => AboutView(), _ => OverviewView() };
        var scroll = new ScrollViewer { Content = view }; Grid.SetRow(scroll, 1); body.Children.Add(scroll);
        var bottom = new DockPanel { Margin = new(0, 16, 0, 0) };
        var actions = Ui.Row(Ui.Button(Ui.T("Revert draft", "Hylkää luonnos"), () => { configuration = active; dirty = false; Render(); }),
            apply = Ui.Button(Ui.T("Apply changes", "Ota muutokset käyttöön"), () => _ = Apply(), true));
        if (!dirty && page is "overview" or "about" or "history" or "devices") actions.Visibility = Visibility.Collapsed;
        DockPanel.SetDock(actions, Dock.Right); bottom.Children.Add(actions);
        feedback = Ui.Text("", 12, "#596763"); feedback.Margin = new(0, 0, 12, 0); bottom.Children.Add(feedback);
        Grid.SetRow(bottom, 2); body.Children.Add(bottom);
        Grid.SetColumn(body, 1); root.Children.Add(body); Content = root;
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(Ui.T("Open Lutra", "Avaa Lutra"), null, (_, _) => Dispatcher.Invoke(() => { Show(); WindowState = WindowState.Normal; Activate(); }));
        menu.Items.Add(Ui.T("Exit monitor", "Sulje seuranta"), null, (_, _) => Dispatcher.Invoke(() => { closing = true; Close(); }));
        tray.ContextMenuStrip?.Dispose(); tray.ContextMenuStrip = menu;
        Refresh();
        if (page == "history") _ = LoadHistory();
        if (page == "overview") _ = LoadMonitorHistory();
    }
    private void CaptureForm()
    {
        if (curveForm != null)
        {
            var updated = curveForm.Take();
            configuration = configuration with { Curves = configuration.Curves.Select(r => r.Id == updated.Id ? updated : r).ToArray() };
        }
        captureForm?.Invoke();
    }
    private void Changed() { dirty = true; Refresh(); }
    private void Guard(Action action)
    {
        try { action(); }
        catch (Exception e) { feedback.Foreground = Ui.Color("#9B352C"); feedback.Text = Ui.T("Check the settings: ", "Tarkista asetukset: ") + e.GetBaseException().Message; }
    }
    private async Task Apply()
    {
        if (busy) return;
        try
        {
            CaptureForm(); configuration = configuration with { Language = Ui.Language }; Curves.Validate(configuration, state.Catalog);
            if (demo) { active = configuration; dirty = false; state = Demo.State(configuration); feedback.Text = Ui.T("Demo updated. No hardware was changed.", "Esittely päivitetty. Laitteita ei muutettu."); return; }
            if (!canApply) throw new IOException(Ui.T("Install and start the Lutra service in Settings first.", "Asenna ja käynnistä Lutra-palvelu ensin asetuksista."));
            busy = true; RefreshStatus();
            var reply = await ControlMessages.RequestAsync(new("apply", configuration));
            if (!reply.Success) throw new IOException(reply.Message);
            active = configuration; dirty = false; feedback.Foreground = Ui.Color("#256450");
            feedback.Text = Ui.T("Saved to the service. Device settings verified.", "Tallennettu palveluun. Laitteen asetukset tarkistettu.");
        }
        catch (Exception e) { feedback.Foreground = Ui.Color("#9B352C"); feedback.Text = Ui.T("Changes were not applied: ", "Muutoksia ei otettu käyttöön: ") + e.GetBaseException().Message; }
        finally { busy = false; RefreshStatus(); }
    }
    private async Task Poll()
    {
        try
        {
            int previousDevices = state.Catalog.Devices.Length;
            RuntimeState? next = demo ? Demo.State(active) : await Task.Run(ControlMessages.ReadState);
            canApply = !demo && next != null && next.Mode != "Stopped" && DateTimeOffset.UtcNow - next.Time < TimeSpan.FromSeconds(5);
            if (canApply)
            {
                state = next!;
                if (!configured && !dirty) { configuration = active = state.Configuration; configured = true; Render(); }
            }
            else if (demo) state = next!;
            else
            {
                state = await Task.Run(() =>
                {
                    monitor ??= new(false, false);
                    var values = monitor.Update();
                    return new RuntimeState(DateTimeOffset.UtcNow, "Monitoring", monitor.Catalog, values, [], monitor.Errors, configuration);
                });
            }
            recent.Enqueue(new(state.Time, state.Values));
            while (recent.Count > 900) recent.Dequeue();
            if (previousDevices != state.Catalog.Devices.Length && !dirty) Render();
            Refresh();
            if (page == "overview" && historyPlot != null && !demo && selectedHistory != null)
            {
                historyPlot.Points = recent.Select(f => new HistoryPoint(f.Time, f.Values.FirstOrDefault(v => v.Id == selectedHistory && v.IsFresh(f.Time))?.Value)).ToArray();
                historyPlot.UpdateAccessibility(); historyPlot.InvalidateVisual();
            }
            foreach (var alarm in state.Alarms)
            {
                string id = alarm.RuleId + (alarm.SensorUnavailable ? "/missing" : "/active");
                if (announced.Add(id) && state.Configuration.Alarms.Any(a => a.Id == alarm.RuleId && a.Notify))
                    tray.ShowBalloonTip(5000, "Lutra · " + alarm.Name, alarm.SensorUnavailable ? Ui.T("Temperature sensor is unavailable.", "Lämpötila-anturi ei ole saatavilla.") : alarm.Value?.ToString("0.0") + " °C", Forms.ToolTipIcon.Warning);
            }
            announced.RemoveWhere(id => !state.Alarms.Any(a => id == a.RuleId + (a.SensorUnavailable ? "/missing" : "/active")));
        }
        catch (Exception e) { feedback.Text = Ui.T("Connection: ", "Yhteys: ") + e.GetBaseException().Message; }
    }
    private void RefreshStatus()
    {
        status.Text = demo ? Ui.T("DEMO · synthetic readings", "ESITTELY · simuloidut mittaukset")
            : canApply ? state.Mode == "Running" ? Ui.T("Service connected", "Palvelu yhdistetty") : Ui.T("Service connected · control waiting", "Palvelu yhdistetty · ohjaus odottaa")
            : Ui.T("Read-only monitoring", "Vain seuranta");
        if (!demo && state.Errors.Length > 0) status.Text = Ui.T("Service needs attention", "Palvelu vaatii huomiota");
        if (state.Alarms.Length > 0) status.Text = state.Alarms.Length + Ui.T(" active alarm(s)", " aktiivista hälytystä");
        status.ToolTip = state.Alarms.Length > 0 ? string.Join("\n", state.Alarms.Select(a => a.Name + (a.SensorUnavailable ? Ui.T(" · sensor unavailable", " · anturi ei saatavilla") : ""))) : null;
        status.Foreground = state.Alarms.Length > 0 ? Ui.Danger : state.Errors.Length > 0 ? Ui.Warning : Ui.Accent;
        draftLabel.Text = dirty ? Ui.T("Unapplied changes", "Muutoksia odottaa käyttöönottoa") : Ui.T("No pending changes", "Ei odottavia muutoksia");
        if (apply != null) apply.IsEnabled = !busy && (demo || canApply) && dirty && curveForm?.HasPendingCoordinates != true;
        RefreshGpuAction();
    }
    private void Refresh()
    {
        RefreshStatus();
        RefreshMonitor();
        var now = DateTimeOffset.UtcNow;
        if (readingGrid != null)
        {
            foreach (var sensor in state.Catalog.Sensors)
            {
                var row = rows.FirstOrDefault(r => r.Id == sensor.Id);
                if (row == null) { row = new(sensor.Id, sensor.Name, CurveForm.DeviceName(state.Catalog, sensor.DeviceId), sensor.Unit); rows.Add(row); }
                var reading = state.Values.FirstOrDefault(v => v.Id == sensor.Id);
                row.Value = reading != null && reading.IsFresh(now) ? reading.Value?.ToString(sensor.Kind == SensorKind.Speed ? "0" : "0.0") ?? "—" : "—";
            }
            foreach (var removed in rows.Where(r => !state.Catalog.Sensors.Any(s => s.Id == r.Id)).ToArray()) rows.Remove(removed);
        }
        if (curveForm != null)
        {
            curveForm.Plot.LiveTemperature = state.Values.FirstOrDefault(v => v.Id == curveForm.InputSensorId && v.IsFresh(now))?.Value;
            curveForm.Plot.InvalidateVisual();
        }
        foreach (var (id, label) in alarmLabels)
        {
            var alarm = state.Alarms.FirstOrDefault(a => a.RuleId == id);
            label.Text = alarm?.SensorUnavailable == true ? Ui.T("Sensor unavailable", "Anturi ei saatavilla")
                : alarm != null ? Ui.T("Active alarm", "Hälytys aktiivinen") : Ui.T("Not triggered", "Ei lauennut");
            label.Foreground = Ui.Color(alarm != null ? "#9B352C" : "#596763");
        }
        if (!dirty && state.Errors.Length > 0) feedback.Text = string.Join("  ", state.Errors.Take(2));
        if (selectedHistory == null && state.Catalog.Sensors.Length > 0) { selectedHistory = state.Catalog.Sensors.FirstOrDefault(s => s.Kind == SensorKind.Temperature)?.Id; if (page == "overview") _ = LoadHistory(); }
    }
    private void Capture(string path)
    {
        UpdateLayout();
        var target = new RenderTargetBitmap((int)ContentRenderedWidth(), (int)ContentRenderedHeight(), 96, 96, PixelFormats.Pbgra32);
        target.Render((Visual)Content);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(target));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var stream = File.Create(path); encoder.Save(stream);
        double ContentRenderedWidth() => ((FrameworkElement)Content).ActualWidth;
        double ContentRenderedHeight() => ((FrameworkElement)Content).ActualHeight;
    }
    private sealed class ReadingRow(string id, string name, string device, string unit) : INotifyPropertyChanged
    {
        public string Id => id; public string Name => name; public string Device => device; public string Unit => unit;
        private string value = "—"; public string Value { get => value; set { this.value = value; PropertyChanged?.Invoke(this, new(nameof(Value))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr handle, int attribute, ref int value, int size);
}

