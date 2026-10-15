using System;
using System.Globalization;
using System.Windows;
using System.Windows.Automation.Peers;
using Lutra.App;
using Lutra.Core;

internal static class ChartTests
{
    public static void Run(Action<string, Action> test)
    {
        foreach (string language in new[] { "en", "fi" })
            test("History charts expose range, sensor and valid statistics in " + language, () =>
            {
                string oldLanguage = Ui.Language; var oldCulture = CultureInfo.CurrentCulture;
                try
                {
                    Ui.Language = language; CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(language == "fi" ? "fi-FI" : "en-GB");
                    var end = new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
                    var chart = new HistoryPlot { SensorName = "Coolant", Unit = "°C", From = end.AddMinutes(-5), To = end,
                        Points = [new(end.AddMinutes(-10), 99), new(end.AddMinutes(-2), 0), new(end.AddMinutes(-1), 38.5), new(end, null)] };
                    chart.UpdateAccessibility();
                    var peer = UIElementAutomationPeer.CreatePeerForElement(chart)!;
                    Assert(peer.IsControlElement() && peer.IsContentElement(), "Chart is absent from the accessibility tree");
                    Assert(peer.GetName() == (language == "fi" ? "Historia: Coolant" : "History: Coolant"), "Missing localized chart name");
                    string summary = peer.GetHelpText();
                    Assert(summary.Contains(language == "fi" ? "Aikaväli:" : "Time range:"), "Missing time range");
                    Assert(summary.Contains(language == "fi" ? "Minimi 0 °C, maksimi 38,5 °C" : "Minimum 0 °C, maximum 38.5 °C"), "Wrong in-range statistics");
                    Assert(summary.Contains(language == "fi" ? "Viimeisin näyte ei saatavilla" : "Latest sample unavailable"), "Gap was concealed");
                    chart.Points = [new(end, double.NaN)]; chart.UpdateAccessibility();
                    Assert(peer.GetHelpText().Contains(language == "fi" ? "ei kelvollisia mittauksia" : "no valid measurements"), "Invalid sample was treated as data");

                    var trace = new MonitorTrace("rpm", "Pump", "#72D3AE") { Points = [new(end.AddMinutes(-1), 800), new(end, 900)] };
                    var trends = new MultiTracePlot { Unit = "rpm", From = end.AddMinutes(-5), To = end, Series = [trace] };
                    trends.UpdateAccessibility(); var trendsPeer = UIElementAutomationPeer.CreatePeerForElement(trends)!;
                    Assert(trendsPeer.GetName() == (language == "fi" ? "Kierrosnopeuskäyrät" : "Rotation speed trends"), "Wrong trend name");
                    Assert(trendsPeer.GetHelpText().Contains("Pump") && trendsPeer.GetHelpText().Contains("900 rpm"), "Missing trace data");
                    trends.From = end.AddSeconds(-30); trends.UpdateAccessibility();
                    Assert(!trendsPeer.GetHelpText().Contains("800 rpm"), "Summary retained data outside the new range");
                    trends.Unit = "°C"; trends.UpdateAccessibility();
                    Assert(trendsPeer.GetName() == (language == "fi" ? "Lämpötilakäyrät" : "Temperature trends"), "Temperature chart is unnamed");
                }
                finally { Ui.Language = oldLanguage; CultureInfo.CurrentCulture = oldCulture; }
            });
        test("Dial is decorative because the parent exposes its reading", () =>
        {
            var peer = UIElementAutomationPeer.CreatePeerForElement(new Dial())!;
            Assert(!peer.IsControlElement() && !peer.IsContentElement(), "Duplicate decorative reading in accessibility tree");
        });
    }
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
}
