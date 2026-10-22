using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Automation;
using Lutra.App;
using Lutra.Core;

internal static class OverviewTests
{
    internal static void Run(Action<string, Action> test)
    {
        test("Overview keeps CPU, coolant, GPU and flow visible in both languages", () =>
        {
            foreach (string language in new[] { "en", "fi" })
            {
                var window = new MainWindow(["--demo", "--capture", "unused.png", "--language", language]);
                try
                {
                    var before = Get(window, "configuration");
                    var meters = (List<LiveInstrument>)Get(window, "instruments");
                    Assert(meters.Count == 4 && new[] { Demo.Water, Demo.Gpu, Demo.Cpu, Demo.Device + "/flow" }.All(id => meters.Any(m => m.Sensor.Id == id)));
                    var cpu = meters.Single(m => m.Sensor.Id == Demo.Cpu);
                    Assert(cpu.Title == (language == "fi" ? "CPU:n lämpötila" : "CPU temperature"));
                    Assert(AutomationProperties.GetName(cpu).Contains("52") && AutomationProperties.GetName(cpu).Contains("°C"));
                    var traces = ((MultiTracePlot)Get(window, "temperatureTraces")).Series;
                    Assert(traces.Single(s => s.Id == Demo.Cpu).Color == cpu.TraceColor && cpu.TraceColor == Ui.AccentHex);
                    Assert(ReferenceEquals(before, Get(window, "configuration")) && !(bool)Get(window, "dirty"));
                }
                finally { ((IDisposable)Get(window, "tray")).Dispose(); }
            }
            Ui.Language = "en";
        });
        test("Overview preserves the configured CPU identity when discovery loses it", () =>
        {
            var rules = Demo.Configuration().Curves;
            var other = new SensorInfo("lhm/cpu-demo/temperature/1", "CPU Package", "lhm/cpu-demo", SensorKind.Temperature, "°C");
            var catalog = Demo.Catalog with { Sensors = Demo.Catalog.Sensors.Where(s => s.Id != Demo.Cpu).Append(other).ToArray() };
            var selected = OverviewSensors.Temperatures(catalog, rules);
            Assert(selected.Any(s => s.Id == Demo.Cpu) && selected.All(s => s.Id != other.Id));
            var cpu = new LiveInstrument(selected.Single(s => s.Id == Demo.Cpu), "CPU temperature", Ui.AccentHex, 100, 35, "");
            var state = Demo.State(Demo.Configuration()) with { Values = [new(other.Id, 65, DateTimeOffset.UtcNow)] };
            cpu.Update(state, DateTimeOffset.UtcNow);
            Assert(AutomationProperties.GetName(cpu).Contains("—") && AutomationProperties.GetName(cpu).Contains("unavailable"));
        });
        test("Overview prioritizes CPU over extra curve sensors and detects an unconfigured package", () =>
        {
            var config = Demo.Configuration();
            var board = new SensorInfo("lhm/board-demo/temperature/0", "System", "lhm/board-demo", SensorKind.Temperature, "°C");
            var catalog = Demo.Catalog with { Sensors = [.. Demo.Catalog.Sensors, board] };
            var extra = config.Curves[2] with { Id = "extra", SensorId = board.Id };
            var selected = OverviewSensors.Temperatures(catalog, [config.Curves[0], config.Curves[1], extra, config.Curves[2]]);
            Assert(selected.Select(s => s.Id).SequenceEqual(new[] { Demo.Water, Demo.Gpu, Demo.Cpu }));
            var core = new SensorInfo("lhm/intelcpu/0/temperature/0", "CPU Core #1", "lhm/intelcpu/0", SensorKind.Temperature, "°C");
            var package = core with { Id = "lhm/intelcpu/0/temperature/8", Name = "CPU Package" };
            catalog = catalog with { Sensors = [.. catalog.Sensors.Where(s => s.Id != Demo.Cpu), core, package] };
            selected = OverviewSensors.Temperatures(catalog, config.Curves.Take(2).ToArray());
            Assert(selected.Any(s => s.Id == package.Id) && selected.All(s => s.Id != core.Id));
        });
        test("Four overview gauges use compact faces at narrow desktop widths", () =>
        {
            foreach (int width in new[] { 1120, 1440 })
            {
                var window = new MainWindow(["--demo", "--capture", "unused.png", "--width", width.ToString(), "--height", "920"]);
                try
                {
                    var meters = (List<LiveInstrument>)Get(window, "instruments");
                    Assert(meters.Count == 4 && meters.All(m => m.Padding.Left == (width < 1360 ? 12 : 16)));
                }
                finally { ((IDisposable)Get(window, "tray")).Dispose(); }
            }
        });
    }
    private static object Get(MainWindow window, string name) => typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
    private static void Assert(bool value) { if (!value) throw new Exception("Assertion failed"); }
}
