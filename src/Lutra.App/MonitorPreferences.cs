using System.Text.Json;

namespace Lutra.App;

// Display-only, per-user preferences. Never included in the controller configuration.
internal sealed record MonitorPreferences
{
    public const double MaximumFlowLowerLimit = 1000;
    public Dictionary<string, double> FlowLowerLimits { get; init; } = new(StringComparer.Ordinal);

    public double FlowLowerLimit(string sensorId) => FlowLowerLimits.GetValueOrDefault(sensorId);

    public static void ValidateLimit(double limit)
    {
        if (!double.IsFinite(limit) || limit < 0 || limit > MaximumFlowLowerLimit)
            throw new InvalidDataException(Ui.T("Enter a flow display limit from 0 to 1000 l/h.", "Anna virtauksen näyttöraja väliltä 0–1000 l/h."));
        if (limit != Math.Round(limit, 2))
            throw new InvalidDataException(Ui.T("Use at most two decimal places for the flow display limit.", "Käytä virtauksen näyttörajassa enintään kahta desimaalia."));
    }

    private void Validate()
    {
        if (FlowLowerLimits == null || FlowLowerLimits.Any(p => string.IsNullOrWhiteSpace(p.Key)))
            throw new InvalidDataException(Ui.T("The flow display settings contain an invalid sensor.", "Virtauksen näyttöasetuksissa on virheellinen anturi."));
        foreach (var limit in FlowLowerLimits.Values) ValidateLimit(limit);
    }

    public static MonitorPreferences Load(string path)
    {
        if (!File.Exists(path)) return new();
        var settings = JsonFiles.Read<MonitorPreferences>(path) ?? throw new InvalidDataException("Empty monitor preferences.");
        settings.Validate(); return settings;
    }

    public void Save(string path) { Validate(); JsonFiles.Save(path, this); }
}

public sealed partial class MainWindow
{
    private MonitorPreferences? monitorPreferences;
    private string? monitorPreferencesError;
    private readonly Dictionary<string, string> flowLimitDrafts = new(StringComparer.Ordinal);
    private string MonitorPreferencesPath => Path.Combine(local, "monitor-preferences.json");
    private MonitorPreferences DisplayPreferences
    {
        get
        {
            if (monitorPreferences != null) return monitorPreferences;
            try { monitorPreferences = demo ? new() : MonitorPreferences.Load(MonitorPreferencesPath); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
            {
                monitorPreferences = new(); monitorPreferencesError = e.Message;
            }
            return monitorPreferences;
        }
    }

    private FrameworkElement FlowDisplaySettingsView()
    {
        var settings = DisplayPreferences;
        var content = Ui.Stack(Ui.Text(Ui.T("Flow meter display", "Virtausmittarin näyttö"), 19, bold: true),
            Ui.Text(Ui.T("Set the lower limit of your sensor’s reliable measurement range. Smaller readings, including zero, appear as < limit on the Overview meter.",
                "Aseta anturin luotettavan mittausalueen alaraja. Sitä pienemmät lukemat, myös nolla, näytetään yleiskuvan mittarissa muodossa < raja."), 13, "#596763"));
        var sensors = state.Catalog.Sensors.Where(s => s.Kind == SensorKind.Flow).ToArray();
        var status = Ui.Text(monitorPreferencesError == null ? "" : Ui.T("Could not load display settings: ", "Näyttöasetuksia ei voitu lukea: ") + monitorPreferencesError, 12, "#9B352C");
        foreach (var sensor in sensors)
        {
            var input = Ui.Input(flowLimitDrafts.GetValueOrDefault(sensor.Id) ?? settings.FlowLowerLimit(sensor.Id).ToString("0.##", CultureInfo.CurrentCulture));
            input.MaxLength = 12;
            input.TextChanged += (_, _) =>
            {
                flowLimitDrafts[sensor.Id] = input.Text;
                status.Text = Ui.T("Display settings not saved yet.", "Näyttöasetuksia ei ole vielä tallennettu."); status.Foreground = Ui.Warning;
            };
            var field = Ui.Field(sensor.Name + " · " + Ui.T("lower limit · l/h", "alaraja · l/h"), input); field.Margin = new(0, 14, 0, 3);
            content.Children.Add(field);
            content.Children.Add(Ui.Text(CurveForm.DeviceName(state.Catalog, sensor.DeviceId), 12, "#596763"));
        }
        if (sensors.Length == 0) content.Children.Add(Ui.Text(Ui.T("Connect a flow sensor to configure its display limit.", "Liitä virtausanturi, jotta voit asettaa sen näyttörajan."), 13, "#596763"));
        else
        {
            var hint = Ui.Text(Ui.T("0 disables the limit · range 0–1000 l/h. Raw readings, history, alarms and cooling control are unchanged. This display cannot confirm flow below the limit.",
                "0 poistaa rajan · sallittu alue 0–1000 l/h. Raakalukemat, historia, hälytykset ja jäähdytyksen ohjaus säilyvät ennallaan. Näyttö ei vahvista virtausta rajan alapuolella."), 12, "#596763");
            hint.Margin = new(0, 12, 0, 12); content.Children.Add(hint);
            var save = Ui.Button(Ui.T("Save display settings", "Tallenna näyttöasetukset"), () =>
            {
                try
                {
                    var limits = new Dictionary<string, double>(DisplayPreferences.FlowLowerLimits, StringComparer.Ordinal);
                    foreach (var draft in flowLimitDrafts)
                    {
                        double limit = Ui.Number(Ui.Input(draft.Value)); MonitorPreferences.ValidateLimit(limit);
                        if (limit == 0) limits.Remove(draft.Key); else limits[draft.Key] = limit;
                    }
                    var next = new MonitorPreferences { FlowLowerLimits = limits };
                    if (!demo) next.Save(MonitorPreferencesPath);
                    monitorPreferences = next; monitorPreferencesError = null; flowLimitDrafts.Clear();
                    status.Text = demo ? Ui.T("Demo display updated. No preferences were saved.", "Esittelyn näyttö päivitetty. Asetuksia ei tallennettu.")
                        : Ui.T("Saved for this Windows user. Cooling settings are unchanged.", "Tallennettu tälle Windows-käyttäjälle. Jäähdytysasetukset säilyivät ennallaan.");
                    status.Foreground = Ui.Accent;
                }
                catch (Exception e) { status.Text = Ui.T("Display settings were not saved: ", "Näyttöasetuksia ei tallennettu: ") + e.GetBaseException().Message; status.Foreground = Ui.Color("#9B352C"); }
            });
            content.Children.Add(save);
        }
        if (flowLimitDrafts.Count > 0 && monitorPreferencesError == null) { status.Text = Ui.T("Display settings not saved yet.", "Näyttöasetuksia ei ole vielä tallennettu."); status.Foreground = Ui.Warning; }
        status.Margin = new(0, 8, 0, 0); content.Children.Add(status);
        var panel = Ui.Panel(content); panel.Margin = new(0, 18, 0, 0); return panel;
    }
}
