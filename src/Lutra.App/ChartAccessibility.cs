using System.Windows.Automation;
using System.Windows.Automation.Peers;

namespace Lutra.App;

internal static class ChartAccessibility
{
    public static void Update(FrameworkElement chart, string name, DateTimeOffset from, DateTimeOffset to,
        IEnumerable<(string Name, HistoryPoint[] Points)> series, string unit)
    {
        var descriptions = series.Select(s => Describe(s.Name, s.Points.Where(p => p.Time >= from && p.Time <= to).OrderBy(p => p.Time).ToArray(), unit));
        AutomationProperties.SetName(chart, name);
        AutomationProperties.SetHelpText(chart, Ui.T("Time range: ", "Aikaväli: ") + Time(from) + " – " + Time(to) + ". " + string.Join(" ", descriptions));
    }

    private static string Time(DateTimeOffset time) => time.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
    private static string Describe(string name, HistoryPoint[] points, string unit)
    {
        var valid = points.Where(p => p.Value.HasValue && double.IsFinite(p.Value.Value)).ToArray();
        if (valid.Length == 0) return name + Ui.T(": no valid measurements in this range.", ": ei kelvollisia mittauksia tällä aikavälillä.");
        string Value(double value) => value.ToString("0.##", CultureInfo.CurrentCulture) + " " + unit;
        var last = valid[^1];
        bool missingEnd = points[^1] != last;
        return name + Ui.T(": last recorded ", ": viimeisin tallennettu ") + Value(last.Value!.Value) + " (" + Time(last.Time) + "). "
            + Ui.T("Minimum ", "Minimi ") + Value(valid.Min(p => p.Value!.Value)) + Ui.T(", maximum ", ", maksimi ") + Value(valid.Max(p => p.Value!.Value)) + ". "
            + (missingEnd ? Ui.T("Latest sample unavailable. ", "Viimeisin näyte ei saatavilla. ") : "")
            + (points.Length > valid.Length ? Ui.T("The data contains gaps.", "Aineistossa on katkoksia.") : "");
    }
}

internal sealed class ChartAutomationPeer(FrameworkElement owner) : FrameworkElementAutomationPeer(owner)
{
    protected override string GetClassNameCore() => "LutraHistoryChart";
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;
    protected override bool IsControlElementCore() => true;
    protected override bool IsContentElementCore() => true;
}

// The parent instrument already exposes the reading, units and availability as text.
internal sealed class DecorativeDialPeer(FrameworkElement owner) : FrameworkElementAutomationPeer(owner)
{
    protected override bool IsControlElementCore() => false;
    protected override bool IsContentElementCore() => false;
}
