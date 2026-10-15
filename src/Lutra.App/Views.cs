using System.Diagnostics;
using System.Security.Principal;
using System.Text.Json;
using Lutra.Hardware;

namespace Lutra.App;

public sealed partial class MainWindow
{
    private FrameworkElement CurvesView()
    {
        var list = new StackPanel { Margin = new(0, 0, 20, 0) };
        list.Children.Add(Ui.Button(Ui.T("Add curve", "Lisää käyrä"), () => Guard(() =>
        {
            CaptureForm();
            var output = state.Catalog.Outputs.FirstOrDefault(o => o.CanControl && !configuration.Curves.Any(c => c.Enabled && c.OutputId == o.Id))
                ?? throw new InvalidDataException(Ui.T("No unused controllable outputs were found.", "Vapaita ohjattavia lähtöjä ei löytynyt."));
            var input = state.Catalog.Sensors.FirstOrDefault(s => s.Kind == SensorKind.Temperature)
                ?? throw new InvalidDataException(Ui.T("No temperature sensors were found.", "Lämpötila-antureita ei löytynyt."));
            var rule = new ControlRule { Name = Ui.T("New curve", "Uusi käyrä"), OutputId = output.Id, SensorId = input.Id };
            configuration = configuration with { Curves = [.. configuration.Curves, rule] }; selectedCurve = rule.Id; Changed(); Render();
        })));
        selectedCurve ??= configuration.Curves.FirstOrDefault()?.Id;
        if (!configuration.Curves.Any(r => r.Id == selectedCurve)) selectedCurve = configuration.Curves.FirstOrDefault()?.Id;
        foreach (var rule in configuration.Curves)
        {
            string id = rule.Id;
            var content = Ui.Stack(Ui.Text(rule.Name, 14, bold: true), Ui.Text(rule.Enabled ? Ui.T("Enabled", "Käytössä") : Ui.T("Disabled", "Pois käytöstä"), 11, "#596763"));
            var button = Ui.Button("", () => Guard(() => { CaptureForm(); selectedCurve = id; Render(); }));
            button.Content = content; button.HorizontalContentAlignment = HorizontalAlignment.Left; button.Margin = new(0, 9, 0, 0);
            button.Background = rule.Id == selectedCurve ? Ui.Color("#DFEBE4") : Ui.Surface;
            list.Children.Add(button);
        }
        var current = configuration.Curves.FirstOrDefault(r => r.Id == selectedCurve);
        if (current == null)
            return Ui.Columns((180, list), (0, Ui.Panel(Ui.Stack(Ui.Text(Ui.T("Connect a sensor to an output", "Yhdistä anturi lähtöön"), 22, bold: true),
                Ui.Text(Ui.T("Add your first curve. Devices lists which channels support control. Applying changes requires the Lutra service.", "Lisää ensimmäinen käyrä. Laitteet-näkymä kertoo, mitä kanavia voi ohjata. Käyttöönotto vaatii Lutra-palvelun."), 14, "#596763")))));
        curveForm = new(current, state.Catalog, Guard); curveForm.Changed += Changed;
        var contentPanel = Ui.Stack(curveForm.View);
        var delete = Ui.Button(Ui.T("Remove this curve", "Poista tämä käyrä"), () =>
        {
            configuration = configuration with { Curves = configuration.Curves.Where(c => c.Id != current.Id).ToArray() };
            selectedCurve = null; Changed(); Render();
        });
        delete.Margin = new(0, 18, 0, 4); contentPanel.Children.Add(delete);
        contentPanel.Children.Add(Ui.Text(Ui.T("On apply, a removed QUADRO channel holds its fallback output. A motherboard channel returns to firmware control.",
            "Käyttöönotossa poistettu QUADRO-kanava jää varateholle. Emolevyn kanava palaa laiteohjelmiston ohjaukseen."), 11, "#596763"));
        return Ui.Columns((180, list), (0, Ui.Panel(contentPanel)));
    }
    private FrameworkElement AlarmsView()
    {
        var layout = new StackPanel();
        var toolbar = Ui.Row(Ui.Button(Ui.T("Add temperature alarm", "Lisää lämpötilahälytys"), () => Guard(() =>
        {
            CaptureForm();
            var input = state.Catalog.Sensors.FirstOrDefault(s => s.Kind == SensorKind.Temperature)
                ?? throw new InvalidDataException(Ui.T("No temperature sensors available.", "Lämpötila-antureita ei ole saatavilla."));
            configuration = configuration with { Alarms = [.. configuration.Alarms, new AlarmRule { Name = Ui.T("Temperature alarm", "Lämpötilahälytys"), SensorId = input.Id }] };
            Changed(); Render();
        })));
        toolbar.Margin = new(0, 0, 0, 18); layout.Children.Add(toolbar);
        layout.Children.Add(Ui.Text(Ui.T("Alarms notify you; they do not change cooling. Use a curve’s safety sensor to trigger fallback output.", "Hälytykset ilmoittavat sinulle; ne eivät muuta jäähdytystä. Käyrän turva-anturilla voit kytkeä varatehon."), 13, "#596763"));
        var forms = new List<Func<AlarmRule>>();
        foreach (var alarm in configuration.Alarms)
        {
            var name = Ui.Input(alarm.Name);
            var sensor = CurveForm.Select(state.Catalog.Sensors.Where(s => s.Kind == SensorKind.Temperature).Select(s => (s.Id, CurveForm.DeviceName(state.Catalog, s.DeviceId) + " · " + s.Name)), alarm.SensorId);
            var direction = CurveForm.Select([("Above", Ui.T("At or above", "Vähintään")), ("Below", Ui.T("At or below", "Enintään"))], alarm.Direction.ToString());
            var threshold = Ui.Number(alarm.Threshold); var hysteresis = Ui.Number(alarm.Hysteresis); var delay = Ui.Number(alarm.DelaySeconds);
            var enabled = CurveForm.Check(Ui.T("Enabled", "Käytössä"), alarm.Enabled); var notify = CurveForm.Check(Ui.T("Desktop notification", "Työpöytäilmoitus"), alarm.Notify);
            var fields = Ui.Columns((0, Ui.Field(Ui.T("Alarm name", "Hälytyksen nimi"), name)), (16, new Border()), (0, Ui.Field(Ui.T("Temperature sensor", "Lämpötila-anturi"), sensor)));
            var limits = Ui.Columns((0, Ui.Field(Ui.T("Condition", "Ehto"), direction)), (16, new Border()), (0, Ui.Field(Ui.T("Threshold · °C", "Raja · °C"), threshold)),
                (16, new Border()), (0, Ui.Field(Ui.T("Reset margin · °C", "Palautumisero · °C"), hysteresis)), (16, new Border()), (0, Ui.Field(Ui.T("Delay · seconds", "Viive · sekuntia"), delay)));
            var remove = Ui.Button(Ui.T("Remove", "Poista"), () => Guard(() => { CaptureForm(); configuration = configuration with { Alarms = configuration.Alarms.Where(a => a.Id != alarm.Id).ToArray() }; Changed(); Render(); }));
            var flag = Ui.Text(state.Alarms.Any(a => a.RuleId == alarm.Id) ? Ui.T("Needs attention", "Vaatii huomiota") : Ui.T("Not triggered", "Ei lauennut"), 12, "#596763");
            alarmLabels[alarm.Id] = flag;
            var panel = Ui.Panel(Ui.Stack(fields, limits, Ui.Row(enabled, new Border { Width = 20 }, notify, new Border { Width = 24 }, remove, flag)));
            panel.Margin = new(0, 16, 0, 0); layout.Children.Add(panel);
            foreach (var box in new[] { name, threshold, hysteresis, delay }) box.TextChanged += (_, _) => Changed();
            foreach (var combo in new[] { sensor, direction }) combo.SelectionChanged += (_, _) => Changed();
            enabled.Click += (_, _) => Changed(); notify.Click += (_, _) => Changed();
            forms.Add(() => alarm with { Name = name.Text.Trim(), SensorId = CurveForm.Id(sensor), Direction = Enum.Parse<AlarmDirection>(CurveForm.Id(direction)),
                Threshold = Ui.Number(threshold), Hysteresis = Ui.Number(hysteresis), DelaySeconds = checked((int)Ui.Number(delay)), Enabled = enabled.IsChecked == true, Notify = notify.IsChecked == true });
        }
        captureForm = () => configuration = configuration with { Alarms = forms.Select(f => f()).ToArray() };
        if (configuration.Alarms.Length == 0) layout.Children.Add(Ui.Text(Ui.T("No alarms configured. Add one for your coolant, CPU or GPU temperature.", "Hälytyksiä ei ole määritetty. Lisää hälytys veden, suorittimen tai näytönohjaimen lämpötilalle."), 18));
        layout.Children.Add(Ui.Text(Ui.T("Notifications require the Lutra monitor to remain open or in the notification area. The service continues logging alarms when it is closed.",
            "Ilmoitukset vaativat Lutran seurannan olevan auki tai ilmoitusalueella. Palvelu kirjaa hälytykset myös seurannan ollessa suljettuna."), 12, "#596763"));
        return layout;
    }
    private FrameworkElement HistoryView()
    {
        selectedHistory ??= state.Catalog.Sensors.FirstOrDefault()?.Id;
        var sensor = CurveForm.Select(state.Catalog.Sensors.Select(s => (s.Id, CurveForm.DeviceName(state.Catalog, s.DeviceId) + " · " + s.Name)), selectedHistory ?? "");
        var range = CurveForm.Select([("15", Ui.T("15 minutes", "15 minuuttia")), ("60", Ui.T("1 hour", "1 tunti")), ("360", Ui.T("6 hours", "6 tuntia")),
            ("1440", Ui.T("24 hours", "24 tuntia")), ("10080", Ui.T("7 days", "7 päivää")), ("43200", Ui.T("30 days", "30 päivää"))], historyRange.TotalMinutes.ToString(CultureInfo.InvariantCulture));
        sensor.SelectionChanged += (_, _) => { selectedHistory = CurveForm.Id(sensor); _ = LoadHistory(); };
        range.SelectionChanged += (_, _) => { historyRange = TimeSpan.FromMinutes(double.Parse(CurveForm.Id(range), CultureInfo.InvariantCulture)); _ = LoadHistory(); };
        var selectors = Ui.Columns((0, Ui.Field(Ui.T("Measurement", "Mittaus"), sensor)), (20, new Border()), (180, Ui.Field(Ui.T("Time range", "Aikaväli"), range)),
            (16, new Border()), (120, Ui.Button(Ui.T("Refresh", "Päivitä"), () => _ = LoadHistory())));
        historyPlot = new() { Height = 380, Empty = Ui.T("No saved readings in this time range.", "Tällä aikavälillä ei ole tallennettuja mittauksia.") };
        historySummary = Ui.Text(Ui.T("Loading history…", "Ladataan historiaa…"), 13, "#596763");
        var panel = Ui.Panel(Ui.Stack(selectors, historyPlot, historySummary));
        return Ui.Stack(panel, Ui.Text(Ui.T("The service stores a measurement every five seconds. Peaks and gaps are retained when displaying long intervals. Retention is set in Settings.",
            "Palvelu tallentaa mittauksen viiden sekunnin välein. Pitkien aikavälien näkymä säilyttää huiput ja katkokset. Säilytysaika valitaan asetuksista."), 12, "#596763"));
    }
    private async Task LoadHistory()
    {
        if (historyPlot == null || selectedHistory == null) return;
        int generation = ++historyGeneration; var plot = historyPlot; var summary = historySummary; string sensor = selectedHistory;
        var info = state.Catalog.Sensors.FirstOrDefault(s => s.Id == sensor);
        plot.Unit = info?.Unit ?? "";
        plot.SensorName = info?.Name ?? "";
        var until = DateTimeOffset.UtcNow; var range = page == "overview" ? TimeSpan.FromMinutes(15) : historyRange;
        plot.From = until - range; plot.To = until;
        plot.Points = []; plot.UpdateAccessibility(); plot.InvalidateVisual();
        try
        {
            HistoryPoint[] data = demo ? Demo.History(sensor, range) : await Task.Run(() => new HistoryStore(Path.Combine(ControlMessages.DataDirectory, "history")).Query(sensor, until - range, until));
            if (generation != historyGeneration || plot != historyPlot) return;
            if (data.Length == 0 && !demo) data = recent.Where(f => f.Time >= until - range).Select(f => new HistoryPoint(f.Time, f.Values.FirstOrDefault(v => v.Id == sensor && v.IsFresh(f.Time))?.Value)).ToArray();
            plot.Points = data; plot.From = until - range; plot.To = until; plot.UpdateAccessibility(); plot.InvalidateVisual();
            var values = data.Where(p => p.Value.HasValue).Select(p => p.Value!.Value).ToArray();
            if (summary != null) summary.Text = (info?.Name ?? "") + "   ·   " + (values.Length == 0 ? Ui.T("Waiting for measurements", "Odotetaan mittauksia")
                : Ui.T("Min ", "Min ") + values.Min().ToString("0.0") + "  /  " + Ui.T("Max ", "Max ") + values.Max().ToString("0.0") + " " + info?.Unit)
                + (demo ? Ui.T("   ·   Synthetic data", "   ·   Simuloitu aineisto") : "");
        }
        catch (Exception e) { if (summary != null) summary.Text = Ui.T("History could not be read: ", "Historiaa ei voitu lukea: ") + e.Message; }
    }
    private FrameworkElement DevicesView()
    {
        var layout = new StackPanel();
        foreach (var device in state.Catalog.Devices)
        {
            var title = Ui.Text(device.Name, 19, bold: true);
            string detail = device.Driver + (device.Firmware > 0 ? "  ·  firmware " + device.Firmware : "");
            var outputRows = state.Catalog.Outputs.Where(o => o.DeviceId == device.Id).Select(o => new { o.Name, State = o.CanControl ? Ui.T("Available", "Käytettävissä") : o.Limitation }).ToArray();
            var table = Ui.Table((Ui.T("Output", "Lähtö"), "Name", 0), (Ui.T("Control", "Ohjaus"), "State", 240)); table.ItemsSource = outputRows;
            var content = Ui.Stack(title, Ui.Text(detail, 12, "#596763"), table);
            if (device.Limitation != null) content.Children.Add(Ui.Text(device.Limitation, 12, "#8B4D19"));
            if (outputRows.Length == 0) content.Children.Add(Ui.Text(Ui.T("Sensor source · no exposed fan controls", "Anturilähde · ei säädettäviä tuuletinlähtöjä"), 12, "#596763"));
            var item = Ui.Panel(content); item.Margin = new(0, 0, 0, 16); layout.Children.Add(item);
        }
        if (state.Catalog.Devices.Length == 0) layout.Children.Add(Ui.Text(Ui.T("Searching for connected hardware…", "Etsitään liitettyjä laitteita…"), 18));
        layout.Children.Add(Ui.Text(Ui.T("Motherboard fan control depends on the board’s controller chip and PawnIO. Enable it in Settings. Unsupported QUADRO firmware remains read-only.",
            "Emolevyn tuuletinohjaus riippuu ohjainpiiristä ja PawnIOsta. Ota se käyttöön asetuksista. Tuntematon QUADRO-laiteohjelmisto jää vain luettavaksi."), 13, "#596763"));
        return layout;
    }
    private FrameworkElement SettingsView()
    {
        var language = CurveForm.Select([("en", "English"), ("fi", "Suomi")], Ui.Language);
        language.SelectionChanged += (_, _) => Guard(() =>
        {
            CaptureForm(); Ui.Language = CurveForm.Id(language); CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(Ui.Language == "fi" ? "fi-FI" : "en-GB");
            Directory.CreateDirectory(local); File.WriteAllText(Path.Combine(local, "language.txt"), Ui.Language); Render();
        });
        var retention = Ui.Number(configuration.RetentionDays);
        var board = CurveForm.Check(Ui.T("Enable motherboard sensors and fan controls", "Ota emolevyn anturit ja tuuletinohjaus käyttöön"), configuration.MotherboardEnabled);
        retention.TextChanged += (_, _) => Changed(); board.Click += (_, _) => Changed();
        captureForm = () => configuration = configuration with { RetentionDays = checked((int)Ui.Number(retention)), MotherboardEnabled = board.IsChecked == true };
        var preferences = Ui.Panel(Ui.Stack(Ui.Text(Ui.T("Preferences", "Asetukset"), 19, bold: true),
            Ui.Field(Ui.T("Interface language", "Käyttöliittymän kieli"), language),
            Ui.Field(Ui.T("History retention · days (1–30)", "Historian säilytys · päivää (1–30)"), retention),
            Ui.Text(Ui.T("Closing the window leaves the monitor in the notification area. Exit there to close it. The Windows service operates independently.",
                "Ikkunan sulkeminen jättää seurannan ilmoitusalueelle. Sulje seuranta sen valikosta. Windows-palvelu toimii itsenäisesti."), 13, "#596763")));
        var service = Ui.Panel(Ui.Stack(Ui.Text(Ui.T("Hardware & background service", "Laitteet ja taustapalvelu"), 19, bold: true),
            board, Ui.Text(Ui.T("Motherboard control needs a supported chip, administrator access and the signed PawnIO driver. Installation never disables Windows security.",
                "Emolevyn ohjaus tarvitsee tuetun piirin, järjestelmänvalvojan oikeudet ja allekirjoitetun PawnIO-ajurin. Asennus ei poista Windowsin suojauksia."), 13, "#596763"),
            Ui.Button(Ui.T("Get PawnIO", "Hanki PawnIO"), () => Open("https://pawnio.eu/")),
            Ui.Button(Ui.T("Install Lutra service…", "Asenna Lutra-palvelu…"), () => Guard(InstallService)),
            Ui.Text(Ui.T("Stop other software that writes the same fan channels before taking control. Lutra refuses control while Aquasuite or Vesi is running.",
                "Sulje samoja tuuletinkanavia ohjaavat ohjelmat ennen käyttöönottoa. Lutra ei ota ohjausta Aquasuiten tai Veden ollessa käynnissä."), 12, "#596763")));
        var files = Ui.Panel(Ui.Stack(Ui.Text(Ui.T("Configuration", "Asetustiedostot"), 19, bold: true),
            Ui.Row(Ui.Button(Ui.T("Export…", "Vie…"), () => Guard(Export)), Ui.Button(Ui.T("Import…", "Tuo…"), () => Guard(Import))),
            Ui.Button(Ui.T("Import installed Vesi curves", "Tuo asennetun Veden käyrät"), () => Guard(ImportVesi)),
            Ui.Text(Ui.T("Imports remain drafts until applied. Device identifiers must match this PC. Local history is not included in exported configurations.",
                "Tuodut asetukset pysyvät luonnoksina käyttöönottoon asti. Laitetunnisteiden on vastattava tätä tietokonetta. Viety asetus ei sisällä mittaushistoriaa."), 12, "#596763")));
        files.Margin = new(0, 18, 0, 0);
        var about = Ui.Panel(Ui.Stack(Ui.Text(Ui.T("Compatibility", "Yhteensopivuus"), 19, bold: true), Ui.Text(Ui.T("Independent cooling control for Windows. Built around explicit device support and local data. No Aquasuite installation is needed.",
                "Itsenäinen jäähdytysohjain Windowsille. Selkeä laitetuki ja paikallinen mittaustieto. Aquasuite-asennusta ei tarvita."), 13, "#596763"),
            Ui.Text(Ui.T("QUADRO writes: firmware 1033 / structure 3. Motherboard headers: LibreHardwareMonitor 0.9.6. Physical compatibility must be checked per board.",
                "QUADRO-kirjoitus: laiteohjelmisto 1033 / rakenne 3. Emolevyn lähdöt: LibreHardwareMonitor 0.9.6. Fyysinen yhteensopivuus tarkistetaan emolevykohtaisesti."), 12, "#596763")));
        about.Margin = new(0, 18, 0, 0);
        return Ui.Columns((0, Ui.Stack(preferences, FlowDisplaySettingsView(), files)), (22, new Border()), (0, Ui.Stack(service, about, StartupSettings())));
    }
    private static void Open(string target) => Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
    private void Export()
    {
        CaptureForm(); Curves.Validate(configuration);
        var dialog = new Microsoft.Win32.SaveFileDialog { FileName = "lutra-configuration.json", Filter = "Lutra configuration (*.json)|*.json" };
        if (dialog.ShowDialog(this) == true) JsonFiles.Save(dialog.FileName, configuration);
    }
    private void Import()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Lutra configuration (*.json)|*.json" };
        if (dialog.ShowDialog(this) != true) return;
        var imported = JsonFiles.Read<AppConfiguration>(dialog.FileName) ?? throw new InvalidDataException("Empty configuration.");
        Curves.Validate(imported); configuration = imported; Changed(); Render();
    }
    private void ImportVesi()
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Vesi", "profile.json");
        using var json = JsonDocument.Parse(File.ReadAllText(path)); var root = json.RootElement;
        string device = "quadro/" + root.GetProperty("DeviceSerial").GetString();
        string gpu = state.Catalog.Sensors.FirstOrDefault(s => s.Kind == SensorKind.Temperature && s.DeviceId.Contains("gpu-nvidia") && s.Name.Contains("Core", StringComparison.OrdinalIgnoreCase))?.Id
            ?? throw new InvalidDataException(Ui.T("The NVIDIA core temperature is not available. Add curves manually instead.", "NVIDIAn ytimen lämpötila ei ole saatavilla. Lisää käyrät käsin."));
        if (!state.Catalog.Devices.Any(d => d.Id == device)) throw new InvalidDataException(Ui.T("The configured QUADRO is not connected.", "Asetusten QUADRO ei ole liitettynä."));
        ControlRule Convert(string key, string sensor, bool pump)
        {
            var c = JsonSerializer.Deserialize<ChannelSettings>(root.GetProperty(key).GetRawText()) ?? throw new InvalidDataException("Invalid Vesi curve.");
            return new() { Name = pump ? "Water pump" : "Radiator fans", SensorId = sensor, OutputId = device + "/output/" + c.Channel,
                Minimum = c.Minimum, Maximum = c.Maximum, Fallback = c.Fallback, HoldMinimum = c.HoldMinimum, StartBoost = c.StartBoost,
                StartupTemperature = c.StartupTemperature, Points = c.Points, IsPump = pump, SafetySensorId = pump ? device + "/temperature/0" : null };
        }
        var next = configuration with { Curves = [Convert("Pump", gpu, true), Convert("Radiator", device + "/temperature/0", false)] };
        Curves.Validate(next, state.Catalog); configuration = next; Changed(); page = "curves"; Render();
    }
    private void InstallService()
    {
        if (demo) throw new InvalidOperationException(Ui.T("Exit demo mode before installing a service.", "Poistu esittelystä ennen palvelun asennusta."));
        CaptureForm(); Curves.Validate(configuration);
        string script = Path.Combine(AppContext.BaseDirectory, "tools", "Install-Lutra.ps1");
        if (!File.Exists(script)) throw new FileNotFoundException(Ui.T("Use a packaged Lutra release to install the service.", "Asenna palvelu Lutran julkaisupaketista."));
        Directory.CreateDirectory(local); string source = Path.Combine(local, "install-configuration.json"); JsonFiles.Save(source, configuration);
        string sid = WindowsIdentity.GetCurrent().User!.Value;
        var start = new ProcessStartInfo("powershell.exe") { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden };
        start.Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + script + "\" -Source \"" + AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar) + "\" -OwnerSid \"" + sid + "\" -ConfigurationPath \"" + source + "\"";
        Process.Start(start);
        closing = true; Close();
    }
}

