using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using Lutra.App;
using Lutra.Core;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        var app = new App(); app.InitializeComponent(); int passed = 0, failed = 0;
        void Test(string name, Action action) { try { action(); passed++; Console.WriteLine("PASS " + name); } catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e.GetBaseException().Message); } }
        static void Assert(bool value) { if (!value) throw new Exception("Assertion failed"); }
        var form = new CurveForm(Demo.Configuration().Curves[0], Demo.Catalog, action => action());
        Test("Input selection updates the live cursor source without applying", () => {
            var input = Find<ComboBox>(form.View, "Input temperature"); input.SelectedValue = Demo.Water; Assert(form.InputSensorId == Demo.Water);
        });
        Test("Uncommitted endpoint edits cannot silently be applied", () => {
            Find<TextBox>(form.View, "Last · °C").Text = "70";
            Assert(form.HasPendingCoordinates);
            try { form.Take(); throw new Exception("Pending edit was accepted"); } catch (System.IO.InvalidDataException) { }
            var wrong = FindButton(form.View, "Update point"); Assert(!wrong.IsEnabled);
            try { wrong.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); throw new Exception("Wrong commit action was accepted"); } catch (System.IO.InvalidDataException) { }
            Assert(Find<TextBox>(form.View, "Last · °C").Text == "70" && form.HasPendingCoordinates);
            FindButton(form.View, "Straighten between endpoints").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert(!form.HasPendingCoordinates && form.Take().Points[^1].Temperature == 70);
        });
        Test("Point coordinates are protected until Update point", () => {
            Find<TextBox>(form.View, "Response · %").Text = "3";
            Assert(form.HasPendingCoordinates);
            var wrong = FindButton(form.View, "Straighten between endpoints"); Assert(!wrong.IsEnabled);
            try { wrong.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); throw new Exception("Wrong commit action was accepted"); } catch (System.IO.InvalidDataException) { }
            Assert(Find<TextBox>(form.View, "Response · %").Text == "3" && form.HasPendingCoordinates);
            FindButton(form.View, "Update point").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert(!form.HasPendingCoordinates && form.Take().Points[0].Power == 3);
        });
        Test("All curve points are reachable through an accessible selector", () => {
            var selector = Find<ComboBox>(form.View, "Selected curve point"); selector.SelectedValue = "15";
            Assert(form.Plot.Selected == 15);
        });
        Test("Curve automation peer exposes selected numeric values", () => {
            var peer = UIElementAutomationPeer.CreatePeerForElement(form.Plot)!;
            var provider = (IValueProvider)peer.GetPattern(PatternInterface.Value)!;
            Assert(provider.Value.Contains("Point 16") && provider.Value.Contains("70"));
        });
        Test("Live alarm labels update without leaving the alarm page", () => {
            var window = new MainWindow(["--demo", "--capture", "unused.png", "--page", "alarms", "--language", "en"]);
            var flags = (Dictionary<string, TextBlock>)Field(window, "alarmLabels").GetValue(window)!;
            var state = Demo.State(Demo.Configuration());
            Field(window, "state").SetValue(window, state with { Alarms = [new("coolant", "Coolant", Demo.Water, DateTimeOffset.UtcNow, 46, false)] });
            typeof(MainWindow).GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
            Assert(flags["coolant"].Text == "Active alarm");
            var status = (TextBlock)Field(window, "status").GetValue(window)!;
            Assert(status.Text == "1 active alarm(s)" && status.ToolTip as string == "Coolant");
            Field(window, "state").SetValue(window, state);
            typeof(MainWindow).GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
            Assert(flags["coolant"].Text == "Not triggered");
            Assert(!status.Text.Contains("active alarm") && status.ToolTip == null);
            ((IDisposable)Field(window, "tray").GetValue(window)!).Dispose();
        });
        Test("Live instruments distinguish zero from an unavailable reading", () => {
            var sensor = Demo.Catalog.Sensors.Single(s => s.Kind == SensorKind.Flow);
            var instrument = new LiveInstrument(sensor, "Flow", "#B8ABEA", 200, null, "Instantaneous flow");
            var state = Demo.State(Demo.Configuration()); var now = DateTimeOffset.UtcNow;
            instrument.Update(state with { Values = [new(sensor.Id, 0, now)] }, now);
            Assert(AutomationProperties.GetName(instrument).Contains("0 l/h"));
            instrument.Update(state with { Values = [new(sensor.Id, 15, now.AddSeconds(-10))] }, now);
            Assert(AutomationProperties.GetName(instrument).Contains("— l/h") && AutomationProperties.GetName(instrument).Contains("unavailable"));
        });
        Test("Flow display floor groups low and zero readings without changing raw values", () => {
            var sensor = Demo.Catalog.Sensors.Single(s => s.Kind == SensorKind.Flow);
            var instrument = new LiveInstrument(sensor, "Flow", "#B8ABEA", 200, null, "Instantaneous flow", 40);
            var state = Demo.State(Demo.Configuration()); var now = DateTimeOffset.UtcNow;
            foreach (double raw in new[] { 0, 14.2, 39.999 })
            {
                var values = new[] { new SensorValue(sensor.Id, raw, now) };
                instrument.Update(state with { Values = values }, now);
                Assert(AutomationProperties.GetName(instrument).Contains("< 40 l/h. Below measurement range"));
                Assert(((Dial)Field(instrument, "dial").GetValue(instrument)!).Value == null);
                Assert(values.Single().Value == raw);
            }
            foreach (double raw in new[] { 40, 40.1, 102.3 })
            {
                instrument.Update(state with { Values = [new(sensor.Id, raw, now)] }, now);
                Assert(!AutomationProperties.GetName(instrument).Contains("< "));
                Assert(((Dial)Field(instrument, "dial").GetValue(instrument)!).Value == raw);
            }
        });
        Test("Flow display floor never conceals missing, stale or invalid readings", () => {
            var sensor = Demo.Catalog.Sensors.Single(s => s.Kind == SensorKind.Flow);
            var instrument = new LiveInstrument(sensor, "Flow", "#B8ABEA", 200, null, "Instantaneous flow", 40);
            var state = Demo.State(Demo.Configuration()); var now = DateTimeOffset.UtcNow;
            foreach (var values in new SensorValue[][] { [], [new(sensor.Id, null, now)], [new(sensor.Id, 0, now.AddSeconds(-6))], [new(sensor.Id, double.NaN, now)] })
            {
                instrument.Update(state with { Values = values }, now);
                Assert(AutomationProperties.GetName(instrument).Contains("— l/h. Sensor unavailable"));
                Assert(((Dial)Field(instrument, "dial").GetValue(instrument)!).Value == null);
            }
        });
        Test("Flow display preferences persist per sensor and reject invalid limits", () => {
            string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lutra-display-test-" + Guid.NewGuid().ToString("N"));
            string path = System.IO.Path.Combine(directory, "monitor-preferences.json");
            try
            {
                Assert(MonitorPreferences.Load(path).FlowLowerLimit("a") == 0);
                new MonitorPreferences { FlowLowerLimits = new() { ["a"] = 40, ["b"] = 0, ["c"] = 12.5 } }.Save(path);
                var saved = MonitorPreferences.Load(path);
                Assert(saved.FlowLowerLimit("a") == 40 && saved.FlowLowerLimit("b") == 0 && saved.FlowLowerLimit("c") == 12.5 && saved.FlowLowerLimit("other") == 0);
                foreach (double invalid in new[] { -1, 1000.01, 0.001, double.NaN, double.PositiveInfinity })
                {
                    try { new MonitorPreferences { FlowLowerLimits = new() { ["a"] = invalid } }.Save(path); throw new Exception("Invalid limit saved"); }
                    catch (System.IO.InvalidDataException) { }
                    Assert(MonitorPreferences.Load(path).FlowLowerLimit("a") == 40);
                }
                foreach (string malformed in new[] { "{", "null", "{\"FlowLowerLimits\":null}", "{\"FlowLowerLimits\":{\"a\":-1}}" })
                {
                    System.IO.File.WriteAllText(path, malformed); bool rejected = false;
                    try { MonitorPreferences.Load(path); } catch (Exception e) when (e is System.IO.InvalidDataException or System.Text.Json.JsonException) { rejected = true; }
                    Assert(rejected);
                }
            }
            finally { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); if (System.IO.Directory.Exists(directory)) System.IO.Directory.Delete(directory); }
        });
        Test("Display settings save independently from controller configuration", () => {
            var window = new MainWindow(["--demo", "--capture", "unused.png", "--page", "settings", "--language", "en"]);
            try
            {
                var before = Field(window, "configuration").GetValue(window);
                var input = Find<TextBox>(window, "Coolant flow · lower limit · l/h"); input.Text = "40";
                FindButton(window, "Save display settings").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var preferences = (MonitorPreferences)Field(window, "monitorPreferences").GetValue(window)!;
                Assert(preferences.FlowLowerLimit(Demo.Device + "/flow") == 40);
                Assert(ReferenceEquals(before, Field(window, "configuration").GetValue(window)) && !(bool)Field(window, "dirty").GetValue(window)!);
                input.Text = "-1"; FindButton(window, "Save display settings").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert(((MonitorPreferences)Field(window, "monitorPreferences").GetValue(window)!).FlowLowerLimit(Demo.Device + "/flow") == 40);
                Assert(Descendants(window).OfType<TextBlock>().Any(t => t.Text.Contains("Display settings were not saved")));
            }
            finally { ((IDisposable)Field(window, "tray").GetValue(window)!).Dispose(); }
        });
        Test("Output instruments show actual stopped RPM and fresh PWM", () => {
            var rule = Demo.Configuration().Curves.Single(r => r.Id == "radiator");
            var output = Demo.Catalog.Outputs.Single(o => o.Id == rule.OutputId);
            var instrument = new OutputInstrument(rule, output, Demo.Catalog);
            instrument.Update(Demo.State(Demo.Configuration()), DateTimeOffset.UtcNow);
            Assert(AutomationProperties.GetName(instrument).Contains("0 rpm, 0 %") && AutomationProperties.GetName(instrument).Contains("Stopped"));
            instrument.Update(Demo.State(Demo.Configuration()) with { Values = [] }, DateTimeOffset.UtcNow);
            Assert(AutomationProperties.GetName(instrument).Contains("— rpm, — %"));
        });
        Test("Motherboard RPM is not guessed from its control index", () => {
            var rule = Demo.Configuration().Curves.Single(r => r.Id == "case");
            var instrument = new OutputInstrument(rule, Demo.Catalog.Outputs.Single(o => o.Id == rule.OutputId), Demo.Catalog);
            Assert(instrument.RpmId == null);
        });
        GpuRefreshTests.Run(Test);
        OverviewTests.Run(Test);
        ChartTests.Run(Test);
        Console.WriteLine($"{passed} UI tests passed; {failed} failed."); app.Shutdown(); return failed == 0 ? 0 : 1;
    }
    private static FieldInfo Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        yield return root;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var item in Descendants(child)) yield return item;
    }
    private static T Find<T>(DependencyObject root, string name) where T : DependencyObject
        => Descendants(root).OfType<T>().Single(x => AutomationProperties.GetName(x) == name);
    private static Button FindButton(DependencyObject root, string content) => Descendants(root).OfType<Button>().Single(b => b.Content as string == content);
}
