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
            Field(window, "state").SetValue(window, state);
            typeof(MainWindow).GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
            Assert(flags["coolant"].Text == "Not triggered");
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
