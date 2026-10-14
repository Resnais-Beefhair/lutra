namespace Lutra.App;

internal static class Demo
{
    public const string Device = "quadro/demo";
    public const string Water = Device + "/temperature/0";
    public const string Gpu = "lhm/gpu-demo/temperature/0";
    public const string Cpu = "lhm/cpu-demo/temperature/0";
    public const string Board = "lhm/board-demo/control/0";
    public static Discovery Catalog { get; } = new(
        [new(Device, "Aqua Computer QUADRO", "DEMO", 1033, "Windows HID / WinUSB", true),
         new("lhm/gpu-demo", "Graphics card", "", 0, "LibreHardwareMonitor 0.9.6", false),
         new("lhm/cpu-demo", "Processor", "", 0, "LibreHardwareMonitor 0.9.6", false),
         new("lhm/board-demo", "Motherboard", "", 0, "LibreHardwareMonitor 0.9.6", true)],
        [new(Water, "Coolant", Device, SensorKind.Temperature, "°C", 0),
         new(Gpu, "GPU core", "lhm/gpu-demo", SensorKind.Temperature, "°C"),
         new(Cpu, "CPU package", "lhm/cpu-demo", SensorKind.Temperature, "°C"),
         new(Device + "/rpm/1", "Water pump", Device, SensorKind.Speed, "rpm"),
         new(Device + "/rpm/2", "Radiator fans", Device, SensorKind.Speed, "rpm"),
         new(Device + "/flow", "Coolant flow", Device, SensorKind.Flow, "l/h"),
         new(Device + "/duty/1", "Pump output", Device, SensorKind.Duty, "%"),
         new(Device + "/duty/2", "Radiator output", Device, SensorKind.Duty, "%"),
         new("lhm/board-demo/fan/0", "Case fan", "lhm/board-demo", SensorKind.Speed, "rpm"),
         new(Board, "Case fan output", "lhm/board-demo", SensorKind.Duty, "%")],
        [new(Device + "/output/1", Device, 1, "QUADRO · Fan 1", true),
         new(Device + "/output/2", Device, 2, "QUADRO · Fan 2", true),
         new(Device + "/output/3", Device, 3, "QUADRO · Fan 3", true),
         new(Device + "/output/4", Device, 4, "QUADRO · Fan 4", true),
         new(Board, "lhm/board-demo", 0, "Motherboard · System fan", true)]);
    public static AppConfiguration Configuration() => new() { MotherboardEnabled = true,
        Curves = [
            new() { Id = "pump", Name = "Water pump", OutputId = Device + "/output/1", SensorId = Gpu, IsPump = true,
                Minimum = 20, Maximum = 100, Fallback = 100, HoldMinimum = true, StartupTemperature = 40,
                SafetySensorId = Water, Points = Curves.Straighten(new(40, 0), new(60, 100)) },
            new() { Id = "radiator", Name = "Radiator fans", OutputId = Device + "/output/2", SensorId = Water,
                StartupTemperature = 40, Points = Curves.Straighten(new(39.25, 0), new(45, 100)) },
            new() { Id = "case", Name = "Case airflow", OutputId = Board, SensorId = Cpu,
                Minimum = 25, HoldMinimum = true, StartupTemperature = 35, Points = Curves.Straighten(new(35, 0), new(80, 100)) }],
        Alarms = [new() { Id = "coolant", Name = "Coolant temperature", SensorId = Water, Threshold = 45, Hysteresis = 2, DelaySeconds = 5 }] };
    public static RuntimeState State(AppConfiguration config)
    {
        var time = DateTimeOffset.UtcNow;
        double[] values = [38.2, 46, 52, 2380, 0, 88.6, 44, 0, 720, 53.3];
        return new(time, "Demo", Catalog, Catalog.Sensors.Select((s, i) => new SensorValue(s.Id, values[i], time)).ToArray(), [], [], config);
    }
    public static HistoryPoint[] History(string sensor, TimeSpan range)
    {
        var end = DateTimeOffset.UtcNow; double baseValue = State(Configuration()).Values.FirstOrDefault(v => v.Id == sensor)?.Value ?? 0;
        double Shape(int i) => Math.Sin(i / 37.0) * .035 + Math.Exp(-Math.Pow((i - 170) / 38.0, 2)) * .08;
        double tail = Shape(299);
        return Enumerable.Range(0, 300).Select(i => new HistoryPoint(end - range + TimeSpan.FromTicks(range.Ticks * i / 299),
            baseValue + (Shape(i) - tail) * baseValue)).ToArray();
    }
}

