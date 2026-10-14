using System.Windows.Automation;
using System.Windows.Controls.Primitives;

namespace Lutra.App;

public sealed partial class MainWindow
{
    private readonly List<LiveInstrument> instruments = [];
    private readonly List<OutputInstrument> outputInstruments = [];
    private readonly Dictionary<string, HistoryPoint[]> monitorHistory = [];
    private MultiTracePlot? temperatureTraces, speedTraces;
    private TextBlock? monitorHistoryStatus;
    private TimeSpan monitorRange = TimeSpan.FromMinutes(15);
    private int monitorGeneration;

    private FrameworkElement OverviewView()
    {
        instruments.Clear(); outputInstruments.Clear();
        var layout = new StackPanel();
        var rules = active.Curves.Where(r => r.Enabled).ToArray();
        var candidates = rules.SelectMany(r => new[] { r.SafetySensorId, r.SensorId }).Where(id => id != null).Distinct()
            .Select(id => state.Catalog.Sensors.FirstOrDefault(s => s.Id == id && s.Kind == SensorKind.Temperature)).OfType<SensorInfo>().ToArray();
        if (candidates.Length == 0) candidates = state.Catalog.Sensors.Where(s => s.Kind == SensorKind.Temperature).Take(3).ToArray();
        var temperatures = candidates.OrderByDescending(s => rules.Any(r => r.IsPump && r.SafetySensorId == s.Id)).Take(3).ToArray();
        var flow = state.Catalog.Sensors.FirstOrDefault(s => s.Kind == SensorKind.Flow && rules.Any(r => r.OutputId.StartsWith(s.DeviceId + "/", StringComparison.Ordinal)))
            ?? state.Catalog.Sensors.FirstOrDefault(s => s.Kind == SensorKind.Flow);
        var shown = temperatures.Take(flow != null ? 2 : 3).Concat(flow != null ? [flow] : []).ToArray();
        if (shown.Length == 0)
            layout.Children.Add(Ui.Panel(Ui.Stack(Ui.Text(Ui.T("Waiting for your cooling sensors", "Odotetaan jäähdytyksen antureita"), 22, bold: true),
                Ui.Text(Ui.T("Connect a device or choose the inputs for your control curves. Live instruments will appear here.", "Liitä laite tai valitse säätökäyrien anturit. Mittarit ilmestyvät tähän."), 14, "#596763"))));
        else
        {
            var gauges = new UniformGrid { Columns = shown.Length, Margin = new(-6, 0, -6, 0) };
            foreach (var sensor in shown)
            {
                bool coolant = rules.Any(r => r.IsPump && r.SafetySensorId == sensor.Id) && sensor.NativeIndex.HasValue;
                string title = coolant ? Ui.T("Coolant", "Vesi") : sensor.Kind == SensorKind.Flow ? Ui.T("Flow", "Virtaus") : sensor.Name;
                string color = sensor.Kind == SensorKind.Flow ? "#B8ABEA" : coolant ? "#6DC5EA" : "#E4C584";
                var starts = rules.Where(r => r.SensorId == sensor.Id).Select(r => r.StartupTemperature).ToArray();
                double? threshold = starts.Length > 0 ? starts.Min() : null;
                string note = sensor.Kind == SensorKind.Flow ? Ui.T("Instantaneous flow", "Hetkellinen virtaus")
                    : threshold.HasValue ? Ui.T("Curve starts at ", "Käyrän alkuraja ") + threshold.Value.ToString("0.#") + " °C" : sensor.Name;
                var meter = new LiveInstrument(sensor, title, color, coolant ? 60 : sensor.Kind == SensorKind.Flow ? 200 : 100, threshold, note,
                    sensor.Kind == SensorKind.Flow ? DisplayPreferences.FlowLowerLimit(sensor.Id) : 0);
                instruments.Add(meter); gauges.Children.Add(meter);
            }
            layout.Children.Add(gauges);
        }
        var outputsHeader = new DockPanel { Margin = new(0, 22, 0, 12) };
        var edit = Ui.Button(Ui.T("Edit curves", "Muokkaa käyriä"), () => { page = "curves"; Render(); });
        DockPanel.SetDock(edit, Dock.Right); outputsHeader.Children.Add(edit);
        outputsHeader.Children.Add(Ui.Text(Ui.T("Cooling outputs", "Jäähdytyksen ohjaus"), 18, bold: true)); layout.Children.Add(outputsHeader);
        var outputs = new UniformGrid { Columns = rules.Length == 1 ? 1 : rules.Length == 3 ? 3 : 2, Margin = new(-6, 0, -6, 0) };
        foreach (var rule in rules)
        {
            var info = state.Catalog.Outputs.FirstOrDefault(o => o.Id == rule.OutputId);
            var meter = new OutputInstrument(rule, info, state.Catalog); outputInstruments.Add(meter); outputs.Children.Add(meter);
        }
        if (rules.Length == 0) layout.Children.Add(Ui.Text(Ui.T("No control curves yet. Sensor monitoring remains available.", "Säätökäyriä ei vielä ole. Antureita voi silti seurata."), 14, "#596763"));
        else layout.Children.Add(outputs);
        var historyHeader = new DockPanel { Margin = new(0, 22, 0, 12) };
        var ranges = Ui.Row();
        foreach (int minutes in new[] { 5, 15, 60 })
        {
            var button = Ui.Button(minutes == 60 ? "1 h" : minutes + " min", () => { monitorRange = TimeSpan.FromMinutes(minutes); Render(); });
            if (monitorRange.TotalMinutes == minutes) { button.Background = Ui.Color("#43564F"); button.BorderBrush = Ui.Accent; }
            ranges.Children.Add(button);
        }
        ranges.Children.Add(Ui.Button(Ui.T("All history", "Koko historia"), () => { page = "history"; Render(); }));
        DockPanel.SetDock(ranges, Dock.Right); historyHeader.Children.Add(ranges);
        historyHeader.Children.Add(Ui.Text(Ui.T("Live trends", "Mittauskäyrät"), 18, bold: true)); layout.Children.Add(historyHeader);
        temperatureTraces = new() { Height = 190, Unit = "°C" };
        speedTraces = new() { Height = 190, Unit = "rpm" };
        var colors = new[] { "#6DC5EA", "#E4C584", "#B8ABEA", "#72D3AE" };
        temperatureTraces.Series = temperatures.Select((s, i) => new MonitorTrace(s.Id, instruments.FirstOrDefault(m => m.Sensor.Id == s.Id)?.Title ?? s.Name, colors[i % colors.Length])).ToArray();
        speedTraces.Series = outputInstruments.Where(o => o.RpmId != null).Select((o, i) => new MonitorTrace(o.RpmId!, o.Rule.Name, colors[i % colors.Length])).ToArray();
        var chartPanels = new List<Border>();
        FrameworkElement Chart(MultiTracePlot plot)
        {
            var legend = new WrapPanel { Margin = new(0, 0, 0, 6) };
            foreach (var series in plot.Series)
            {
                var label = Ui.Text(series.Name, 12, series.Color); label.Margin = new(0, 0, 18, 4); legend.Children.Add(label);
            }
            var panel = Ui.Panel(Ui.Stack(legend, plot), new Thickness(14)); chartPanels.Add(panel); return panel;
        }
        layout.Children.Add(Ui.Columns((0, Chart(temperatureTraces)), (12, new Border()), (0, Chart(speedTraces))));
        monitorHistoryStatus = Ui.Text("", 11, "#596763"); monitorHistoryStatus.Margin = new(0, 7, 0, 14); layout.Children.Add(monitorHistoryStatus);
        readingGrid = Ui.Table((Ui.T("Sensor", "Anturi"), "Name", 0), (Ui.T("Device", "Laite"), "Device", 0), (Ui.T("Value", "Arvo"), "Value", 95), ("", "Unit", 65));
        readingGrid.ItemsSource = rows; readingGrid.MaxHeight = 300;
        layout.Children.Add(new Expander { Header = Ui.T("All sensor readings", "Kaikki anturilukemat"), Content = readingGrid });
        // Keep the monitoring instruments and both traces in the first viewport on short desktop windows.
        // Reflow density in place so resizing never rebuilds live history or interrupts monitoring.
        bool? compact = null;
        void FitViewport()
        {
            double windowHeight = ActualHeight > 0 ? ActualHeight : Height;
            bool next = windowHeight < 900;
            temperatureTraces.Height = speedTraces.Height = next ? 120 : Math.Clamp(windowHeight - 750, 150, 190);
            if (compact == next) return; compact = next;
            foreach (var instrument in instruments) instrument.SetCompact(next);
            foreach (var instrument in outputInstruments) instrument.SetCompact(next);
            outputsHeader.Margin = historyHeader.Margin = next ? new Thickness(0, 10, 0, 8) : new Thickness(0, 22, 0, 12);
            foreach (var button in ranges.Children.OfType<Button>().Append(edit))
            {
                button.MinHeight = next ? 28 : 34; button.Padding = next ? new Thickness(10, 5, 10, 5) : new Thickness(13, 8, 13, 8);
            }
            foreach (var panel in chartPanels) panel.Padding = new Thickness(next ? 10 : 14);
        }
        SizeChangedEventHandler resized = (_, _) => FitViewport();
        layout.Loaded += (_, _) => { SizeChanged += resized; FitViewport(); };
        layout.Unloaded += (_, _) => SizeChanged -= resized;
        FitViewport(); RefreshMonitor(); return layout;
    }

    private async Task LoadMonitorHistory()
    {
        int generation = ++monitorGeneration;
        var ids = (temperatureTraces?.Series ?? []).Concat(speedTraces?.Series ?? []).Select(s => s.Id).Distinct().ToArray();
        var end = DateTimeOffset.UtcNow; var range = monitorRange;
        try
        {
            var data = await Task.Run(() => ids.ToDictionary(id => id, id => demo ? Demo.History(id, range)
                : new HistoryStore(Path.Combine(ControlMessages.DataDirectory, "history")).Query(id, end - range, end, 600)));
            if (generation != monitorGeneration || page != "overview") return;
            monitorHistory.Clear(); foreach (var pair in data) monitorHistory[pair.Key] = pair.Value;
            RefreshMonitor();
            if (monitorHistoryStatus != null) monitorHistoryStatus.Text = demo ? Ui.T("Demo · synthetic history", "Esittely · simuloitu historia")
                : Ui.T("Local history + live measurements · gaps remain visible", "Paikallinen historia + reaaliaikaiset mittaukset · katkokset säilyvät näkyvinä");
        }
        catch (Exception e) { if (monitorHistoryStatus != null) monitorHistoryStatus.Text = Ui.T("History unavailable: ", "Historia ei saatavilla: ") + e.Message; }
    }
    private void RefreshMonitor()
    {
        if (page != "overview") return;
        var now = DateTimeOffset.UtcNow;
        foreach (var meter in instruments) meter.Update(state, now);
        foreach (var meter in outputInstruments) meter.Update(state, now);
        foreach (var chart in new[] { temperatureTraces, speedTraces }.OfType<MultiTracePlot>())
        {
            chart.From = now - monitorRange; chart.To = now;
            foreach (var series in chart.Series)
            {
                var saved = monitorHistory.GetValueOrDefault(series.Id) ?? [];
                var live = recent.Where(f => f.Time >= chart.From).Select(f => new HistoryPoint(f.Time, f.Values.FirstOrDefault(v => v.Id == series.Id && v.IsFresh(f.Time))?.Value));
                series.Points = saved.Concat(live).Where(p => p.Time >= chart.From && p.Time <= now).GroupBy(p => p.Time).Select(g => g.Last()).OrderBy(p => p.Time).ToArray();
            }
            chart.InvalidateVisual();
        }
    }
}

internal sealed class LiveInstrument : Border
{
    public SensorInfo Sensor { get; }
    public string Title { get; }
    private readonly Dial dial;
    private readonly TextBlock value, stateLabel, heading;
    private readonly Grid face;
    private readonly double flowLowerLimit;
    public LiveInstrument(SensorInfo sensor, string title, string color, double maximum, double? threshold, string note, double flowLowerLimit = 0)
    {
        MonitorPreferences.ValidateLimit(flowLowerLimit); this.flowLowerLimit = sensor.Kind == SensorKind.Flow ? flowLowerLimit : 0;
        Sensor = sensor; Title = title; Background = Ui.Surface; Padding = new(16); Margin = new(6, 0, 6, 0);
        heading = Ui.Text(title, 17, bold: true);
        dial = new() { Width = 110, Height = 114, Maximum = maximum, Threshold = threshold, Trace = Ui.Color(color), Margin = new(0, 0, 12, 0) };
        value = Ui.Text("—", 38, color); value.TextWrapping = TextWrapping.NoWrap; value.Typography.NumeralAlignment = FontNumeralAlignment.Tabular;
        var valueHost = new Viewbox { Child = value, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = HorizontalAlignment.Left };
        var reading = Ui.Stack(valueHost, Ui.Text(sensor.Unit, 13, "#596763")); reading.VerticalAlignment = VerticalAlignment.Center;
        face = Ui.Columns((122, dial), (0, reading)); face.Margin = new(0, 8, 0, 4);
        stateLabel = Ui.Text(note, 12, "#596763"); stateLabel.MinHeight = 32;
        Child = Ui.Stack(heading, face, stateLabel); ToolTip = sensor.Name + " · " + sensor.DeviceId;
        Tag = note;
    }
    public void SetCompact(bool compact)
    {
        Padding = new(compact ? 12 : 16); heading.FontSize = compact ? 16 : 17;
        dial.Width = compact ? 72 : 110; dial.Height = compact ? 76 : 114;
        face.ColumnDefinitions[0].Width = new GridLength(compact ? 84 : 122);
        face.Margin = compact ? new Thickness(0, 4, 0, 0) : new Thickness(0, 8, 0, 4);
        value.FontSize = compact ? 32 : 38; stateLabel.MinHeight = compact ? 0 : 32;
    }
    public void Update(RuntimeState state, DateTimeOffset now)
    {
        double? number = state.Values.FirstOrDefault(v => v.Id == Sensor.Id && v.IsFresh(now))?.Value;
        bool belowLimit = number.HasValue && flowLowerLimit > 0 && number.Value < flowLowerLimit;
        value.Text = belowLimit ? "< " + flowLowerLimit.ToString("0.##") : number?.ToString(Sensor.Kind == SensorKind.Flow ? "0.#" : "0.0") ?? "—";
        bool alarm = state.Alarms.Any(a => a.SensorId == Sensor.Id);
        stateLabel.Text = !number.HasValue ? Ui.T("Sensor unavailable", "Anturi ei saatavilla") : alarm ? Ui.T("Temperature alarm", "Lämpötilahälytys")
            : belowLimit ? Ui.T("Below measurement range", "Alle mittausrajan") : (string)Tag;
        stateLabel.Foreground = alarm || !number.HasValue ? Ui.Warning : Ui.Muted;
        // Below-range readings are not a precise measurement: never draw a fabricated value on the dial.
        dial.Value = belowLimit ? null : number; dial.InvalidateVisual();
        ToolTip = Sensor.Name + " · " + Sensor.DeviceId + (flowLowerLimit > 0 ? "\n" + Ui.T("Display limit: ", "Näyttöraja: ") + flowLowerLimit.ToString("0.##") + " " + Sensor.Unit
            + "\n" + Ui.T("Raw values, including zero, remain in All sensor readings and History.", "Raaka-arvot, myös nolla, säilyvät kaikissa anturilukemissa ja historiassa.") : "");
        AutomationProperties.SetName(this, Title + ": " + value.Text + " " + Sensor.Unit + ". " + stateLabel.Text);
    }
}

internal sealed class OutputInstrument : Border
{
    public ControlRule Rule { get; }
    public string? RpmId { get; }
    private readonly string? dutyId;
    private readonly TextBlock rpm, duty, detail, heading, unit;
    private readonly ProgressBar bar;
    public OutputInstrument(ControlRule rule, OutputInfo? output, Discovery catalog)
    {
        Rule = rule;
        // Only QUADRO has a proven RPM-to-output mapping. SuperIO indices need not correspond.
        bool quadro = output?.DeviceId.StartsWith("quadro/", StringComparison.Ordinal) == true;
        RpmId = quadro ? output!.DeviceId + "/rpm/" + output.Channel : null;
        dutyId = quadro ? output!.DeviceId + "/duty/" + output.Channel : catalog.Sensors.Any(s => s.Id == rule.OutputId && s.Kind == SensorKind.Duty) ? rule.OutputId : null;
        Background = Ui.Surface; Padding = new(16); Margin = new(6, 0, 6, 10);
        var head = new DockPanel(); duty = Ui.Text("— %", 17, "#72D3AE", true); DockPanel.SetDock(duty, Dock.Right); head.Children.Add(duty); heading = Ui.Text(rule.Name, 16, bold: true); head.Children.Add(heading);
        rpm = Ui.Text("—", 34); rpm.Margin = new(0, 6, 0, 0); rpm.Typography.NumeralAlignment = FontNumeralAlignment.Tabular;
        unit = Ui.Text(RpmId == null ? Ui.T("RPM not mapped", "RPM ei yhdistetty") : "rpm", 12, "#596763"); unit.Margin = new(10, 0, 0, 7); unit.VerticalAlignment = VerticalAlignment.Bottom;
        bar = new() { Minimum = 0, Maximum = 100, Height = 5, BorderThickness = new(0), Background = Ui.Line, Foreground = Ui.Accent, Margin = new(0, 8, 0, 8) };
        detail = Ui.Text("", 12, "#596763"); Child = Ui.Stack(head, Ui.Row(rpm, unit), bar, detail);
        ToolTip = (output?.Name ?? rule.OutputId) + " · " + (catalog.Sensors.FirstOrDefault(s => s.Id == rule.SensorId)?.Name ?? rule.SensorId);
    }
    public void SetCompact(bool compact)
    {
        Padding = new(compact ? 12 : 16); Margin = new(6, 0, 6, compact ? 6 : 10);
        heading.FontSize = compact ? 14 : 16; duty.FontSize = compact ? 15 : 17;
        rpm.FontSize = compact ? 28 : 34; rpm.Margin = new(0, compact ? 2 : 6, 0, 0);
        unit.Margin = new(10, 0, 0, compact ? 4 : 7); bar.Margin = new(0, compact ? 4 : 8, 0, compact ? 4 : 8);
    }
    public void Update(RuntimeState state, DateTimeOffset now)
    {
        double? Read(string? id) => state.Values.FirstOrDefault(v => v.Id == id && v.IsFresh(now))?.Value;
        var speed = Read(RpmId); var output = Read(dutyId); var input = Read(Rule.SensorId);
        rpm.Text = speed?.ToString("0") ?? "—"; duty.Text = output?.ToString("0.#") + (output.HasValue ? " %" : "— %");
        bar.Value = output ?? 0; bar.Opacity = output.HasValue ? 1 : .25;
        string start = Rule.StartupTemperature.ToString("0.#") + " °C";
        detail.Text = !output.HasValue || !input.HasValue ? Ui.T("Measurement unavailable", "Mittaus ei saatavilla")
            : output == 0 ? Ui.T("Stopped · starts at ", "Pysähtynyt · käynnistys ") + start
            : Rule.HoldMinimum && Math.Abs(output.Value - Rule.Minimum) < .4 ? Ui.T("Minimum output · rises above ", "Minimiteho · nousu yli ") + start
            : Ui.T("Input ", "Lähde ") + input.Value.ToString("0.#") + " °C · " + (state.Mode == "Running" ? Ui.T("Automatic", "Automaattinen") : Ui.T("Monitoring", "Seuranta"));
        if (state.Errors.Length > 0) detail.Text = Ui.T("Check service status", "Tarkista palvelun tila");
        AutomationProperties.SetName(this, Rule.Name + ": " + rpm.Text + " rpm, " + duty.Text + ". " + detail.Text);
    }
}

internal sealed class Dial : FrameworkElement
{
    public double? Value { get; set; }
    public double Maximum { get; set; } = 100;
    public double? Threshold { get; set; }
    public Brush Trace { get; set; } = Brushes.Gray;
    protected override void OnRender(DrawingContext dc)
    {
        Point center = new(ActualWidth / 2, ActualHeight / 2 - 3); double radius = Math.Min(ActualWidth, ActualHeight) / 2 - 8;
        Point At(double fraction, double r) { double angle = (135 + fraction * 270) * Math.PI / 180; return new(center.X + Math.Cos(angle) * r, center.Y + Math.Sin(angle) * r); }
        void Arc(double fraction, Brush brush, double thickness)
        {
            if (fraction <= 0) return;
            var g = new StreamGeometry(); using (var context = g.Open()) { context.BeginFigure(At(0, radius), false, false); context.ArcTo(At(fraction, radius), new(radius, radius), 0, fraction > 2.0 / 3, SweepDirection.Clockwise, true, false); }
            dc.DrawGeometry(null, new Pen(brush, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, g);
        }
        Arc(1, Ui.Line, 7); if (Value.HasValue) Arc(Math.Clamp(Value.Value / Maximum, 0, 1), Trace, 7);
        if (Threshold is { } t && t >= 0 && t <= Maximum) dc.DrawLine(new Pen(Ui.Ink, 2), At(t / Maximum, radius - 6), At(t / Maximum, radius + 6));
        for (int i = 0; i <= 8; i++) dc.DrawLine(new Pen(Ui.Line, 1), At(i / 8.0, radius - 10), At(i / 8.0, radius - 14));
        var text = new FormattedText("0 – " + Maximum.ToString("0"), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 10, Ui.Muted, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(text, new(center.X - text.Width / 2, center.Y + radius - 3));
    }
}

internal sealed record MonitorTrace(string Id, string Name, string Color)
{
    public HistoryPoint[] Points { get; set; } = [];
}
internal sealed class MultiTracePlot : FrameworkElement
{
    public MonitorTrace[] Series { get; set; } = [];
    public string Unit { get; set; } = "";
    public DateTimeOffset From { get; set; }
    public DateTimeOffset To { get; set; }
    protected override void OnRender(DrawingContext dc)
    {
        Rect area = new(42, 14, Math.Max(1, ActualWidth - 57), Math.Max(1, ActualHeight - 42));
        var values = Series.SelectMany(s => s.Points).Where(p => p.Value.HasValue && double.IsFinite(p.Value.Value)).Select(p => p.Value!.Value).ToArray();
        double step = Unit == "rpm" ? 500 : 5;
        double min = values.Length == 0 || Unit == "rpm" ? 0 : Math.Max(0, Math.Floor(values.Min() / step) * step - step);
        double max = values.Length == 0 ? Unit == "rpm" ? 4000 : 60 : Math.Max(min + step * 2, Math.Ceiling(values.Max() / step) * step + step);
        void Text(string text, double x, double y, Brush? color = null) => dc.DrawText(new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 10, color ?? Ui.Muted, VisualTreeHelper.GetDpi(this).PixelsPerDip), new(x, y));
        for (int i = 0; i <= 3; i++)
        {
            double y = area.Bottom - area.Height * i / 3; dc.DrawLine(new Pen(Ui.Line, .6), new(area.Left, y), new(area.Right, y)); Text((min + (max - min) * i / 3).ToString("0.#"), 0, y - 6);
        }
        Text(Unit, 0, 0); double seconds = Math.Max(1, (To - From).TotalSeconds);
        for (int i = 0; i <= 3; i++) Text(From.AddSeconds(seconds * i / 3).ToLocalTime().ToString("HH:mm"), area.Left + area.Width * i / 3 - (i == 3 ? 28 : 8), area.Bottom + 9);
        if (values.Length < 2) { Text(Ui.T("Waiting for measurements…", "Odotetaan mittauksia…"), area.Left + 8, area.Top + 28); return; }
        dc.PushClip(new RectangleGeometry(area));
        foreach (var series in Series)
        {
            Point? previous = null; DateTimeOffset last = default; var pen = new Pen(Ui.Color(series.Color), 2);
            foreach (var sample in series.Points)
            {
                if (!sample.Value.HasValue || !double.IsFinite(sample.Value.Value)) { previous = null; continue; }
                Point current = new(area.Left + (sample.Time - From).TotalSeconds / seconds * area.Width, area.Bottom - (sample.Value.Value - min) / (max - min) * area.Height);
                if (previous.HasValue && sample.Time - last <= TimeSpan.FromSeconds(15)) dc.DrawLine(pen, previous.Value, current);
                previous = current; last = sample.Time;
            }
            if (previous.HasValue && To - last < TimeSpan.FromSeconds(10)) dc.DrawEllipse(pen.Brush, null, previous.Value, 3, 3);
        }
        dc.Pop();
    }
}
