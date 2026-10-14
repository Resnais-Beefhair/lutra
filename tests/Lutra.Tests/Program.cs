using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Lutra.Core;
using Lutra.Hardware;

if (args.Contains("--probe"))
{
    using var hardware = new HardwareSession(false, false);
    await Task.Delay(1800);
    var values = hardware.Update(); var catalog = hardware.Catalog;
    Console.WriteLine(JsonSerializer.Serialize(new { catalog, values, hardware.Errors }, JsonFiles.Options));
    return catalog.Devices.Any(d => d.Driver == "Windows HID / WinUSB") && values.Any(v => v.Id.Contains("gpu-nvidia") && v.Value.HasValue) ? 0 : 2;
}
int passed = 0, failed = 0;
void Test(string name, Action test)
{
    try { test(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e.Message); }
}
static void Assert(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
static void Near(double expected, double actual, double tolerance = .001) => Assert(Math.Abs(expected - actual) <= tolerance, $"Expected {expected}; got {actual}");
static void Reject(Action action) { try { action(); } catch (InvalidDataException) { return; } throw new Exception("Invalid configuration was accepted"); }
var now = DateTimeOffset.UtcNow;
var fan = new ControlRule { Id = "fan", Name = "Radiator", SensorId = "water", OutputId = "out/2", StartupTemperature = 40, Points = Curves.Straighten(new(39.25, 0), new(45, 100)) };
var pump = fan with { Id = "pump", Name = "Pump", SensorId = "gpu", OutputId = "out/1", IsPump = true, Minimum = 20, HoldMinimum = true, Points = Curves.Straighten(new(40, 0), new(60, 100)) };
Dictionary<string, SensorValue> Values(double? temperature, DateTimeOffset? time = null) => new() { ["water"] = new("water", temperature, time ?? now), ["gpu"] = new("gpu", temperature, time ?? now) };

Test("Fan stays stopped below 40 despite nonzero curve response", () => {
    var engine = new SoftwareControl();
    Near(0, engine.Evaluate(fan, Values(35), now));
    Near(0, engine.Evaluate(fan, Values(39.9), now));
    Assert(engine.Evaluate(fan, Values(40), now) > 0);
    Assert(engine.Evaluate(fan, Values(39.5), now) > 0, "Running fan should retain latch");
    Near(0, engine.Evaluate(fan, Values(39.25), now));
});
Test("Pump holds minimum below 40 and rises after 40", () => {
    var engine = new SoftwareControl();
    Near(20, engine.Evaluate(pump, Values(38), now)); Near(20, engine.Evaluate(pump, Values(40), now));
    Near(60, engine.Evaluate(pump, Values(50), now)); Near(100, engine.Evaluate(pump, Values(100), now));
});
Test("Missing, stale, future and non-finite temperatures fail to fallback", () => {
    foreach (var reading in new[] { new SensorValue("gpu", null, now), new("gpu", 38, now.AddSeconds(-6)), new("gpu", 38, now.AddSeconds(10)), new("gpu", double.NaN, now) })
        Near(100, new SoftwareControl().Evaluate(pump, new Dictionary<string, SensorValue> { ["gpu"] = reading }, now));
});
Test("Safety sensor missing or too hot triggers fallback", () => {
    var guarded = pump with { SafetySensorId = "water", SafetyTemperature = 45 };
    var values = Values(38); values["water"] = new("water", 45, now);
    Near(100, new SoftwareControl().Evaluate(guarded, values, now));
    values.Remove("water"); Near(100, new SoftwareControl().Evaluate(guarded, values, now));
});
Test("Boost ends after two seconds and cannot defeat missing input fallback", () => {
    var boosted = fan with { StartBoost = true, Fallback = 70 }; var engine = new SoftwareControl();
    Near(100, engine.Evaluate(boosted, Values(41), now));
    Assert(engine.Evaluate(boosted, Values(41, now.AddSeconds(3)), now.AddSeconds(3)) < 100);
    Near(70, engine.Evaluate(boosted, Values(null), now));
});
Test("Pump validation rejects zero minimum and disabled hold", () => {
    Reject(() => Curves.Validate(pump with { Minimum = 0 }));
    Reject(() => Curves.Validate(pump with { HoldMinimum = false }));
    Reject(() => Curves.Validate(pump with { Fallback = 25 }));
});
Test("Duplicate outputs cannot be activated", () => {
    Reject(() => Curves.Validate(new AppConfiguration { Curves = [fan, fan with { Id = "other" }] }));
    Curves.Validate(new AppConfiguration { Curves = [fan, fan with { Id = "other", Enabled = false }] });
});
Test("Unavailable input/output and safety sensors rejected", () => {
    var catalog = new Discovery([], [new("water", "Water", "device", SensorKind.Temperature, "°C")], [new("out/2", "device", 2, "Fan", true)]);
    Curves.Validate(new AppConfiguration { Curves = [fan] }, catalog);
    Reject(() => Curves.Validate(new AppConfiguration { Curves = [fan with { SensorId = "missing" }] }, catalog));
    Reject(() => Curves.Validate(new AppConfiguration { Curves = [fan with { SafetySensorId = "missing" }] }, catalog));
    Reject(() => Curves.Validate(new AppConfiguration { Curves = [fan] }, catalog with { Outputs = [catalog.Outputs[0] with { CanControl = false }] }));
});
Test("Straightening quantizes temperatures without duplicate nodes", () => {
    var random = new Random(412);
    for (int sample = 0; sample < 2000; sample++)
    {
        double first = random.NextDouble() * 70, last = first + .3 + random.NextDouble() * 29;
        var points = Curves.Straighten(new(first, 0), new(last, 100));
        Curves.Validate(fan with { Points = points, StartupTemperature = points[0].Temperature });
        for (int i = 1; i < 16; i++) Assert(points[i].Temperature > points[i - 1].Temperature && points[i].Power >= points[i - 1].Power);
        Near(0, points[0].Power); Near(100, points[^1].Power);
    }
});
Test("Invalid curves, null rules and unknown enum values rejected", () => {
    Reject(() => Curves.Validate(fan with { Points = [new(35, 0)] }));
    Reject(() => Curves.Validate(fan with { Maximum = double.NaN }));
    Reject(() => Curves.Validate(fan with { StartupTemperature = 45 }));
    Reject(() => Curves.Validate(new AppConfiguration { Curves = [null!] }));
    Reject(() => Curves.Validate(new AppConfiguration { Alarms = [new() { SensorId = "water", Direction = (AlarmDirection)7 }] }));
});
Test("QUADRO CRC vector and corruption detection", () => {
    Assert(QuadroProtocol.Crc(Encoding.ASCII.GetBytes("123456789")) == 0xB4C8);
    var bytes = QuadroControl.SoftwareTemperatures([38.25]); Assert(QuadroProtocol.ValidCrc(bytes)); bytes[3] ^= 1; Assert(!QuadroProtocol.ValidCrc(bytes));
});
Test("Software sensor report encodes multiple slots and leaves others unavailable", () => {
    var bytes = QuadroControl.SoftwareTemperatures([38.25, 45.75, 66]);
    Assert(bytes.Length == 67 && bytes[0] == 4 && bytes[33] == 3 && bytes[34] == 3);
    Assert(QuadroProtocol.U16(bytes, 1) == 3825 && QuadroProtocol.U16(bytes, 3) == 4575 && QuadroProtocol.U16(bytes, 7) == 0x7FFF);
    Assert(QuadroProtocol.ValidCrc(bytes)); Reject(() => QuadroControl.SoftwareTemperatures([double.NaN]));
});
Test("Settings encoder changes only requested channel bytes plus CRC", () => {
    byte[] baseline = new byte[961]; new Random(8).NextBytes(baseline); baseline[0] = 3; QuadroProtocol.Put16(baseline, 1, 3); QuadroProtocol.Seal(baseline);
    var result = QuadroControl.Build(baseline, [(2, 0, fan)]);
    var changed = Enumerable.Range(0, baseline.Length).Where(i => baseline[i] != result[i]).ToArray();
    Assert(changed.All(i => i == 0x8B || i >= 0x8E && i <= 0x8F || i >= 0x9E && i <= 0xDF || i >= 0x1B && i <= 0x21 || i >= 959));
    Assert(result[6] == baseline[6] && result[7] == baseline[7], "Flow calibration must remain unchanged");
    var settings = QuadroProtocol.ReadSettings(result); Near(40, settings.Channels[1].StartupTemperature);
    Near(39.25, settings.Channels[1].Points[0].Temperature); Near(100, settings.Channels[1].Fallback);
    baseline[1] = 1; QuadroProtocol.Seal(baseline);
    try { QuadroControl.Build(baseline, [(2, 0, fan)]); throw new Exception("Unknown firmware schema accepted"); } catch (NotSupportedException) { }
});
Test("Status sentinel is missing, true flow zero remains zero", () => {
    byte[] bytes = new byte[220]; bytes[0] = 1; QuadroProtocol.Put16(bytes, 0x34, 0x7FFF); QuadroProtocol.Put16(bytes, 0x6E, 0);
    var status = QuadroProtocol.ReadStatus(bytes); Assert(status.Temperatures[0] == null && status.FlowLitresPerHour == 0);
    QuadroProtocol.Put16(bytes, 0x6E, 0x7FFF); Assert(QuadroProtocol.ReadStatus(bytes).FlowLitresPerHour == null);
});
Test("Alarm delay, hysteresis and missing sensor behavior", () => {
    var alarm = new AlarmRule { Id = "a", SensorId = "water", Threshold = 45, DelaySeconds = 5, Hysteresis = 2 };
    var engine = new AlarmEngine();
    Assert(engine.Evaluate([alarm], Values(45).Values, now).Length == 0);
    Assert(engine.Evaluate([alarm], Values(45, now.AddSeconds(4)).Values, now.AddSeconds(4)).Length == 0);
    Assert(engine.Evaluate([alarm], Values(45, now.AddSeconds(5)).Values, now.AddSeconds(5)).Length == 1);
    Assert(engine.Evaluate([alarm], Values(44, now.AddSeconds(6)).Values, now.AddSeconds(6)).Length == 1);
    Assert(engine.Evaluate([alarm], Values(null).Values, now.AddSeconds(7)).Single().SensorUnavailable);
    Assert(engine.Evaluate([alarm], Values(42, now.AddSeconds(8)).Values, now.AddSeconds(8)).Length == 0);
});
Test("History reduction retains high, low and missing reading", () => {
    var points = Enumerable.Range(0, 2000).Select(i => new HistoryPoint(now.AddSeconds(i * 5), 35)).ToArray();
    points[133] = points[133] with { Value = 90 }; points[137] = points[137] with { Value = -4 }; points[140] = points[140] with { Value = null };
    var reduced = HistoryStore.Reduce(points, 100);
    Assert(reduced.Length <= 100 && reduced.Any(p => p.Value == 90) && reduced.Any(p => p.Value == -4) && reduced.Any(p => !p.Value.HasValue));
});
Test("History survives restart, records gaps and prunes only owned daily logs", () => {
    string temp = Path.Combine(Path.GetTempPath(), "Lutra-test-" + Guid.NewGuid().ToString("N"));
    try
    {
        var store = new HistoryStore(temp);
        store.Append(new(now, [new("water", 38, now)]), 7);
        store.Append(new(now.AddSeconds(5), [new("water", 39, now.AddSeconds(5))]), 7);
        store.Append(new(now.AddMinutes(2), [new("water", 44, now.AddMinutes(2))]), 7);
        var loaded = new HistoryStore(temp).Query("water", now.AddMinutes(-1), now.AddMinutes(3));
        Assert(loaded.Any(p => p.Value == 38) && loaded.Any(p => p.Value == 44) && loaded.Any(p => !p.Value.HasValue));
        File.WriteAllText(Path.Combine(temp, "notes.jsonl"), "keep");
        File.WriteAllText(Path.Combine(temp, "2000-01-01.jsonl"), "old");
        store.Prune(now, 1); Assert(File.Exists(Path.Combine(temp, "notes.jsonl")) && !File.Exists(Path.Combine(temp, "2000-01-01.jsonl")));
    }
    finally { Directory.Delete(temp, true); }
});
Test("Configuration roundtrip preserves limits and selections", () => {
    var config = new AppConfiguration { Language = "fi", MotherboardEnabled = true, RetentionDays = 30, Curves = [pump, fan] };
    var restored = JsonSerializer.Deserialize<AppConfiguration>(JsonSerializer.Serialize(config, JsonFiles.Options), JsonFiles.Options)!;
    Curves.Validate(restored); Assert(restored.Curves[0].SensorId == "gpu" && restored.Curves[0].Minimum == 20 && restored.RetentionDays == 30);
});
Test("IPC rejects oversized payload before allocation", () => {
    byte[] header = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(header, int.MaxValue);
    using var stream = new MemoryStream(header);
    Reject(() => ControlMessages.ReadAsync<ServiceRequest>(stream, default).GetAwaiter().GetResult());
});
AlarmTests.Run(Test);
UpdateTests.Run(Test);
Console.WriteLine($"{passed} passed; {failed} failed.");
return failed == 0 ? 0 : 1;

