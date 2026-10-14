using System.Windows.Automation;

namespace Lutra.App;

internal sealed class CurveForm
{
    public FrameworkElement View { get; }
    public CurvePlot Plot { get; } = new();
    public string InputSensorId => Id(input);
    public bool HasPendingCoordinates => pointPending || endpointsPending;
    private bool pointPending, endpointsPending, syncing;
    private readonly TextBlock coordinateNotice;
    private readonly Button updatePoint, straighten;
    private readonly ControlRule rule;
    private readonly TextBox name, minimum, maximum, fallback, startup, safetyTemperature;
    private readonly ComboBox input, output, safety;
    private readonly CheckBox pump, hold, boost, enabled;
    private readonly TextBox firstT, firstP, lastT, lastP, pointT, pointP;
    private readonly ComboBox pointSelection;
    public event Action? Changed;
    public CurveForm(ControlRule rule, Discovery catalog, Action<Action> guard)
    {
        this.rule = rule;
        name = Ui.Input(rule.Name); minimum = Ui.Number(rule.Minimum); maximum = Ui.Number(rule.Maximum); fallback = Ui.Number(rule.Fallback);
        startup = Ui.Number(rule.StartupTemperature); safetyTemperature = Ui.Number(rule.SafetyTemperature);
        input = Select(catalog.Sensors.Where(s => s.Kind == SensorKind.Temperature).Select(s => (s.Id, DeviceName(catalog, s.DeviceId) + " · " + s.Name)), rule.SensorId);
        output = Select(catalog.Outputs.Where(o => o.CanControl).Select(o => (o.Id, o.Name)), rule.OutputId);
        safety = Select(new[] { ("", Ui.T("None", "Ei käytössä")) }.Concat(catalog.Sensors.Where(s => s.Kind == SensorKind.Temperature).Select(s => (s.Id, DeviceName(catalog, s.DeviceId) + " · " + s.Name))), rule.SafetySensorId ?? "");
        pump = Check(Ui.T("Pump output", "Pumppulähtö"), rule.IsPump); hold = Check(Ui.T("Hold minimum below start", "Minimiteho käynnistysrajan alla"), rule.HoldMinimum);
        boost = Check(Ui.T("Boost at startup", "Käynnistyspulssi"), rule.StartBoost); enabled = Check(Ui.T("Enable this curve", "Ota käyrä käyttöön"), rule.Enabled);
        Plot.Set(rule.Points); Plot.Startup = rule.StartupTemperature;
        firstT = Ui.Number(rule.Points[0].Temperature); firstP = Ui.Number(rule.Points[0].Power);
        lastT = Ui.Number(rule.Points[^1].Temperature); lastP = Ui.Number(rule.Points[^1].Power);
        pointT = Ui.Number(rule.Points[0].Temperature); pointP = Ui.Number(rule.Points[0].Power);
        coordinateNotice = Ui.Text("", 12, Ui.WarningHex);
        pointSelection = Select(Enumerable.Range(0, 16).Select(i => (i.ToString(), Ui.T("Point ", "Piste ") + (i + 1) + " / 16")), "0");
        Plot.SelectionChanged += () => { syncing = true; pointSelection.SelectedValue = Plot.Selected.ToString(); pointT.Text = Plot.Points[Plot.Selected].Temperature.ToString("0.##"); pointP.Text = Plot.Points[Plot.Selected].Power.ToString("0.##"); syncing = false; pointPending = false; CoordinateState(); };
        pointSelection.SelectionChanged += (_, _) => { if (!syncing) { Plot.Selected = int.Parse(Id(pointSelection), CultureInfo.InvariantCulture); syncing = true; pointT.Text = Plot.Points[Plot.Selected].Temperature.ToString("0.##"); pointP.Text = Plot.Points[Plot.Selected].Power.ToString("0.##"); syncing = false; pointPending = false; CoordinateState(); Plot.InvalidateVisual(); } };
        Plot.Edited += () => { SyncEndpoints(); Changed?.Invoke(); };
        var graph = Ui.Stack(Ui.Text(Ui.T("Curve response", "Käyrän vaste"), 17, bold: true),
            Ui.Text(Ui.T("Drag a point or enter its coordinates. Response is scaled to the output limits.", "Vedä pistettä tai anna sen koordinaatit. Vaste skaalataan lähtörajoihin."), 12, Ui.MutedHex), Plot);
        var selected = Ui.Columns((0, Ui.Field(Ui.T("Point temperature · °C", "Pisteen lämpötila · °C"), pointT)),
            (12, new Border()), (0, Ui.Field(Ui.T("Response · %", "Vaste · %"), pointP)),
            (12, new Border()), (138, updatePoint = Ui.Button(Ui.T("Update point", "Muuta pistettä"), () => guard(() => {
                if (endpointsPending) throw new InvalidDataException(Ui.T("Use Straighten to finish the endpoint edit first.", "Vahvista päätepisteiden muutos ensin Suorista-painikkeella."));
                Plot.Change(Ui.Number(pointT), Ui.Number(pointP)); }))));
        graph.Children.Add(Ui.Field(Ui.T("Selected curve point", "Valittu käyräpiste"), pointSelection)); graph.Children.Add(selected);
        var endpointGrid = Ui.Columns((0, Ui.Field(Ui.T("First · °C", "Alku · °C"), firstT)), (10, new Border()),
            (0, Ui.Field(Ui.T("First · %", "Alku · %"), firstP)), (10, new Border()),
            (0, Ui.Field(Ui.T("Last · °C", "Loppu · °C"), lastT)), (10, new Border()),
            (0, Ui.Field(Ui.T("Last · %", "Loppu · %"), lastP)));
        graph.Children.Add(endpointGrid);
        graph.Children.Add(straighten = Ui.Button(Ui.T("Straighten between endpoints", "Suorista alku- ja loppupisteen välille"), () => guard(() =>
        {
            if (pointPending) throw new InvalidDataException(Ui.T("Use Update point to finish the selected point edit first.", "Vahvista valitun pisteen muutos ensin Muuta pistettä -painikkeella."));
            Plot.Set(Curves.Straighten(new(Ui.Number(firstT), Ui.Number(firstP)), new(Ui.Number(lastT), Ui.Number(lastP)))); SyncEndpoints();
            syncing = true; pointT.Text = Plot.Points[Plot.Selected].Temperature.ToString("0.##"); pointP.Text = Plot.Points[Plot.Selected].Power.ToString("0.##"); syncing = false;
            pointPending = false; CoordinateState(); Changed?.Invoke();
        })));
        graph.Children.Add(coordinateNotice);
        graph.Children.Add(Ui.Text(Ui.T("Below the start temperature, output stays off or at its minimum. A running fan stops at the curve’s last zero point.",
            "Käynnistysrajan alla lähtö on pois päältä tai minimissä. Käynnistynyt tuuletin pysähtyy käyrän viimeisessä nollapisteessä."), 12, Ui.MutedHex));
        var parameters = Ui.Stack(Ui.Field(Ui.T("Curve name", "Käyrän nimi"), name), Ui.Field(Ui.T("Start temperature · °C", "Käynnistysraja · °C"), startup),
            Ui.Columns((0, Ui.Field(Ui.T("Minimum · %", "Minimi · %"), minimum)), (10, new Border()), (0, Ui.Field(Ui.T("Maximum · %", "Maksimi · %"), maximum))),
            Ui.Field(Ui.T("Fallback output · %", "Varateho · %"), fallback), enabled, pump, hold, boost,
            Ui.Text(Ui.T("Over-temperature protection", "Ylikuumenemissuoja"), 14, bold: true),
            Ui.Field(Ui.T("Additional safety sensor", "Lisäturva-anturi"), safety), Ui.Field(Ui.T("Fallback at · °C", "Varateholle · °C"), safetyTemperature));
        parameters.Margin = new(22, 0, 0, 0);
        var main = Ui.Columns((0, graph), (230, parameters));
        var selectors = Ui.Columns((0, Ui.Field(Ui.T("Input temperature", "Lämpötilan lähde"), input)), (18, new Border()), (0, Ui.Field(Ui.T("Output device / channel", "Lähtölaite / kanava"), output)));
        View = Ui.Stack(selectors, main);
        foreach (var field in new[] { name, minimum, maximum, fallback, startup, safetyTemperature })
            field.TextChanged += (_, _) => { if (double.TryParse(startup.Text, out var t)) { Plot.Startup = t; Plot.InvalidateVisual(); } Changed?.Invoke(); };
        foreach (var combo in new[] { input, output, safety }) combo.SelectionChanged += (_, _) => Changed?.Invoke();
        foreach (var check in new[] { pump, hold, boost, enabled }) check.Click += (_, _) => Changed?.Invoke();
        pump.Click += (_, _) => { if (pump.IsChecked == true) { hold.IsChecked = true; if (Ui.Number(minimum) == 0) minimum.Text = "20"; } };
        foreach (var box in new[] { firstT, firstP, lastT, lastP }) box.TextChanged += (_, _) => { if (!syncing) { endpointsPending = true; CoordinateState(); Changed?.Invoke(); } };
        foreach (var box in new[] { pointT, pointP }) box.TextChanged += (_, _) => { if (!syncing) { pointPending = true; CoordinateState(); Changed?.Invoke(); } };
    }
    public ControlRule Take()
    {
        if (HasPendingCoordinates) throw new InvalidDataException(Ui.T("Use Update point or Straighten to finish the coordinate edit before applying or leaving this curve.", "Vahvista koordinaattimuutos Muuta pistettä- tai Suorista-painikkeella ennen käyttöönottoa tai käyrästä poistumista."));
        var result = rule with { Name = name.Text.Trim(), SensorId = Id(input), OutputId = Id(output), Minimum = Ui.Number(minimum),
            Maximum = Ui.Number(maximum), Fallback = Ui.Number(fallback), StartupTemperature = Ui.Number(startup),
            HoldMinimum = hold.IsChecked == true, IsPump = pump.IsChecked == true, StartBoost = boost.IsChecked == true, Enabled = enabled.IsChecked == true,
            SafetySensorId = string.IsNullOrEmpty(Id(safety)) ? null : Id(safety), SafetyTemperature = Ui.Number(safetyTemperature), Points = Plot.Points.ToArray() };
        Curves.Validate(result); return result;
    }
    private void SyncEndpoints()
    {
        syncing = true;
        firstT.Text = Plot.Points[0].Temperature.ToString("0.##"); firstP.Text = Plot.Points[0].Power.ToString("0.##");
        lastT.Text = Plot.Points[^1].Temperature.ToString("0.##"); lastP.Text = Plot.Points[^1].Power.ToString("0.##");
        syncing = false; endpointsPending = false; CoordinateState();
    }
    private void CoordinateState()
    {
        coordinateNotice.Text = pointPending ? Ui.T("Point edit pending: choose Update point before Apply.", "Pistemuutos odottaa: paina Muuta pistettä ennen käyttöönottoa.")
            : endpointsPending ? Ui.T("Endpoint edit pending: choose Straighten before Apply.", "Päätepisteiden muutos odottaa: paina Suorista ennen käyttöönottoa.") : "";
        updatePoint.IsEnabled = !endpointsPending; straighten.IsEnabled = !pointPending;
        foreach (var box in new[] { firstT, firstP, lastT, lastP }) box.IsEnabled = !pointPending;
        pointT.IsEnabled = pointP.IsEnabled = !endpointsPending;
        pointSelection.IsEnabled = Plot.IsEnabled = !HasPendingCoordinates;
        Changed?.Invoke();
    }
    public static ComboBox Select(IEnumerable<(string Id, string Name)> options, string selected)
    {
        var combo = new ComboBox { DisplayMemberPath = "Name", SelectedValuePath = "Id" };
        var all = options.Select(o => new Choice(o.Id, o.Name)).ToList();
        if (!all.Any(o => o.Id == selected) && !string.IsNullOrWhiteSpace(selected)) all.Add(new(selected, Ui.T("Unavailable: ", "Ei saatavilla: ") + selected));
        combo.ItemsSource = all; combo.SelectedValue = selected; return combo;
    }
    public static string Id(ComboBox combo) => (combo.SelectedItem as Choice)?.Id ?? "";
    public static string DeviceName(Discovery catalog, string id) => catalog.Devices.FirstOrDefault(d => d.Id == id)?.Name ?? id;
    public static CheckBox Check(string label, bool value) { var result = new CheckBox { Content = label, IsChecked = value }; AutomationProperties.SetName(result, label); return result; }
    private sealed record Choice(string Id, string Name);
}

