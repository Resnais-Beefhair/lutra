using LibreHardwareMonitor.Hardware;
using System.Reflection;
using Lutra.Core;
using SensorValue = Lutra.Core.SensorValue;

namespace Lutra.Hardware;

/// <summary>One owner, one update thread. No drivers are installed and no outputs are written during discovery.</summary>
public sealed class HardwareSession : IDisposable
{
    private static readonly string LibraryVersion = typeof(Computer).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
        .InformationalVersion.Split('+')[0];
    private readonly Computer computer;
    private readonly List<QuadroConnection> quadros = [];
    private readonly Dictionary<string, IControl> boardOutputs = [];
    private readonly Dictionary<string, SoftwareControl> evaluators = [];
    private readonly HashSet<string> ownedBoardOutputs = [];
    private readonly bool writable;
    private readonly bool motherboard;
    private readonly DateTimeOffset opened = DateTimeOffset.UtcNow;
    private ControlRule[] boardRules = [];
    private readonly List<string> discoveryErrors = [];
    public Discovery Catalog { get; private set; } = new([], [], []);
    public string[] Errors { get; private set; } = [];
    public bool NeedsReconnect => DateTimeOffset.UtcNow - opened > TimeSpan.FromSeconds(12)
        && quadros.Any(q => q.Latest == null || DateTimeOffset.UtcNow - q.Latest.Time > TimeSpan.FromSeconds(10));
    public HardwareSession(bool writable, bool motherboard)
    {
        this.writable = writable; this.motherboard = motherboard;
        computer = new() { IsGpuEnabled = true, IsCpuEnabled = motherboard, IsMotherboardEnabled = motherboard,
            IsControllerEnabled = false, IsStorageEnabled = false, IsMemoryEnabled = false, IsNetworkEnabled = false };
        try { computer.Open(); } catch (Exception e) { discoveryErrors.Add("Hardware discovery: " + e.Message); }
        foreach (var descriptor in NativeHid.EnumerateQuadros())
        {
            try { quadros.Add(new(descriptor, writable)); }
            catch (Exception e) { discoveryErrors.Add("QUADRO: " + e.Message); }
        }
    }
    public SensorValue[] Update()
    {
        var sensors = new List<SensorInfo>(); var devices = new List<DeviceInfo>(); var outputs = new List<OutputInfo>();
        var readings = new List<SensorValue>(); var errors = new List<string>(discoveryErrors);
        var now = DateTimeOffset.UtcNow;
        foreach (var q in quadros)
        {
            string id = q.Id; var data = q.Latest;
            string? limit = q.CanControl ? null : "Writes verified only on QUADRO firmware 1033, settings structure 3.";
            devices.Add(new(id, "Aqua Computer QUADRO", q.Hid.Description.Serial, data?.Firmware ?? 0, "Windows HID / WinUSB", q.CanControl, limit));
            for (int i = 0; i < 4; i++)
            {
                Add(id + "/temperature/" + i, "Temperature " + (i + 1), SensorKind.Temperature, "°C", data?.Temperatures[i], i);
                Add(id + "/rpm/" + (i + 1), "Fan " + (i + 1), SensorKind.Speed, "rpm", data?.Fans[i].Rpm);
                Add(id + "/duty/" + (i + 1), "Output " + (i + 1), SensorKind.Duty, "%", data?.Fans[i].Output);
                outputs.Add(new(id + "/output/" + (i + 1), id, i + 1, "QUADRO · Fan " + (i + 1), q.CanControl, limit));
            }
            Add(id + "/flow", "Flow", SensorKind.Flow, "l/h", data?.FlowLitresPerHour);
            if (q.Error != null) errors.Add("QUADRO: " + q.Error);
            void Add(string sensorId, string name, SensorKind kind, string unit, double? value, int? index = null)
            {
                sensors.Add(new(sensorId, name, id, kind, unit, index));
                readings.Add(new(sensorId, value, data?.Time ?? now.AddMinutes(-1)));
            }
        }
        boardOutputs.Clear();
        foreach (var hardware in computer.Hardware) Visit(hardware);
        if (motherboard && boardOutputs.Count == 0)
            errors.Add("No controllable motherboard headers found. Check board support, administrator access and the signed PawnIO driver.");
        Catalog = new(devices.ToArray(), sensors.ToArray(), outputs.ToArray());
        Errors = errors.Distinct().ToArray(); return readings.ToArray();

        void Visit(IHardware hardware)
        {
            string id = "lhm" + hardware.Identifier;
            bool fresh = true;
            try { hardware.Update(); } catch (Exception e) { errors.Add(hardware.Name + ": " + e.Message); fresh = false; }
            bool board = hardware.HardwareType == HardwareType.SuperIO;
            bool canControl = board && hardware.Sensors.Any(s => s.Control != null);
            devices.Add(new(id, hardware.Name, "", 0, "LibreHardwareMonitor " + LibraryVersion, canControl));
            foreach (var sensor in hardware.Sensors)
            {
                SensorKind? kind = sensor.SensorType switch { SensorType.Temperature => SensorKind.Temperature, SensorType.Fan => SensorKind.Speed,
                    SensorType.Flow => SensorKind.Flow, SensorType.Control => SensorKind.Duty, _ => null };
                if (!kind.HasValue) continue;
                string sid = "lhm" + sensor.Identifier;
                string unit = kind switch { SensorKind.Temperature => "°C", SensorKind.Speed => "rpm", SensorKind.Flow => "l/h", _ => "%" };
                sensors.Add(new(sid, sensor.Name, id, kind.Value, unit));
                readings.Add(new(sid, fresh && sensor.Value.HasValue && float.IsFinite(sensor.Value.Value) ? sensor.Value.Value : null, now));
                if (board && sensor.Control is { } control)
                {
                    bool supported = control.MinSoftwareValue <= 0 && control.MaxSoftwareValue >= 100;
                    outputs.Add(new(sid, id, sensor.Index, hardware.Name + " · " + sensor.Name, supported,
                        supported ? null : "Control does not expose the expected 0–100% PWM range."));
                    if (supported) boardOutputs[sid] = control;
                }
            }
            foreach (var child in hardware.SubHardware) Visit(child);
        }
    }
    /// <summary>Recreate GPU handles after driver changes without releasing QUADRO or motherboard control.</summary>
    public void RefreshGpuSensors()
    {
        computer.IsGpuEnabled = false;
        computer.IsGpuEnabled = true;
    }
    public async Task Configure(AppConfiguration config, string backups, CancellationToken token)
    {
        if (!writable) throw new InvalidOperationException("Monitoring cannot change hardware.");
        Curves.Validate(config, Catalog);
        var enabled = config.Curves.Where(r => r.Enabled).ToArray();
        // Preflight all outputs before the first device write.
        foreach (var r in enabled)
            if (!Catalog.Outputs.Any(o => o.Id == r.OutputId && o.CanControl)) throw new InvalidDataException("Output is unavailable: " + r.Name);
        var applied = new List<(QuadroConnection Device, ControlRule[] Before)>();
        try
        {
            foreach (var q in quadros)
            {
                var next = enabled.Where(r => Catalog.Outputs.Single(o => o.Id == r.OutputId).DeviceId == q.Id).ToArray();
                if (next.Length == 0 && q.Rules.Length == 0) continue;
                var previous = q.Rules;
                await q.Configure(next, Catalog, Path.Combine(backups, q.Hid.Description.Serial), token);
                applied.Add((q, previous));
            }
        }
        catch (Exception original)
        {
            var failures = new List<Exception> { original };
            foreach (var previous in applied.AsEnumerable().Reverse())
            {
                try { using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                    await previous.Device.Configure(previous.Before, Catalog, backups, deadline.Token); }
                catch (Exception e) { failures.Add(e); }
            }
            throw new AggregateException("Configuration was not fully applied. Check device status before retrying.", failures);
        }
        var nextBoard = enabled.Where(r => boardOutputs.ContainsKey(r.OutputId)).ToArray();
        foreach (var removed in ownedBoardOutputs.Except(nextBoard.Select(r => r.OutputId)).ToArray())
        {
            if (boardOutputs.TryGetValue(removed, out var control)) control.SetDefault();
            ownedBoardOutputs.Remove(removed); evaluators.Remove(removed);
        }
        boardRules = nextBoard;
    }
    public string[] Control(SensorValue[] values)
    {
        if (!writable) return [];
        var now = DateTimeOffset.UtcNow; var map = values.ToDictionary(v => v.Id); var errors = new List<string>();
        foreach (var q in quadros)
            try { var error = q.Feed(map, now); if (error != null) errors.Add(error); }
            catch (Exception e) { errors.Add("QUADRO feed: " + e.Message); }
        foreach (var rule in boardRules)
        {
            try
            {
                if (!boardOutputs.TryGetValue(rule.OutputId, out var control)) { errors.Add("Output disappeared: " + rule.Name); continue; }
                if (!evaluators.TryGetValue(rule.OutputId, out var evaluator)) evaluators[rule.OutputId] = evaluator = new();
                control.SetSoftware((float)evaluator.Evaluate(rule, map, now)); ownedBoardOutputs.Add(rule.OutputId);
                if (!SoftwareControl.Safe(rule, map, now)) errors.Add(rule.Name + ": using fallback because a temperature is unavailable or over its safety limit.");
            }
            catch (Exception e) { errors.Add(rule.Name + ": " + e.Message); }
        }
        return errors.ToArray();
    }
    public void Dispose()
    {
        foreach (var id in ownedBoardOutputs)
            try { if (boardOutputs.TryGetValue(id, out var control)) control.SetDefault(); } catch (Exception) { }
        foreach (var q in quadros) q.Dispose();
        computer.Close();
    }
}

