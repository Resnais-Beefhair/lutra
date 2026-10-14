using Lutra.Core;

namespace Lutra.Hardware;

public static class QuadroControl
{
    public static byte[] Build(byte[] baseline, IReadOnlyList<(int Channel, int Source, ControlRule Rule)> rules)
    {
        var settings = QuadroProtocol.ReadSettings(baseline);
        if (settings.Structure != 3) throw new NotSupportedException("This QUADRO settings layout is read-only.");
        var bytes = baseline.ToArray();
        foreach (var (channel, source, rule) in rules)
        {
            Curves.Validate(rule);
            if (channel is < 1 or > 4 || source is < 0 or > 12 || source == 4) throw new InvalidDataException("Unsupported QUADRO channel or source.");
            int b = QuadroProtocol.ControllerOffsets[channel - 1], limits = 0x12 + 9 * (channel - 1);
            bytes[b] = 2;
            QuadroProtocol.Put16(bytes, b + 3, source);
            bytes[limits] = (byte)((bytes[limits] & ~3) | (rule.HoldMinimum ? 1 : 0) | (rule.StartBoost ? 2 : 0));
            QuadroProtocol.Put16(bytes, limits + 1, Percent(rule.Minimum));
            QuadroProtocol.Put16(bytes, limits + 3, Percent(rule.Maximum));
            QuadroProtocol.Put16(bytes, limits + 5, Percent(rule.Fallback));
            QuadroProtocol.Put16(bytes, b + 0x13, Percent(rule.StartupTemperature));
            for (int i = 0; i < 16; i++)
            {
                QuadroProtocol.Put16(bytes, b + 0x15 + 2 * i, Percent(rule.Points[i].Temperature));
                QuadroProtocol.Put16(bytes, b + 0x35 + 2 * i, Percent(rule.Points[i].Power));
            }
        }
        QuadroProtocol.Seal(bytes); return bytes;
    }
    private static int Percent(double v) => checked((int)Math.Round(v * 100, MidpointRounding.AwayFromZero));
    public static byte[] SoftwareTemperatures(IReadOnlyList<double> temperatures)
    {
        if (temperatures.Count > 8 || temperatures.Any(t => !double.IsFinite(t) || t < 0 || t > 120))
            throw new InvalidDataException("Software temperatures must be 0–120 °C; at most eight inputs per QUADRO.");
        byte[] bytes = new byte[67]; bytes[0] = 4;
        for (int i = 0; i < 16; i++) { QuadroProtocol.Put16(bytes, 1 + 2 * i, 0x7FFF); bytes[49 + i] = 100; }
        for (int i = 0; i < temperatures.Count; i++)
        {
            QuadroProtocol.Put16(bytes, 1 + 2 * i, Percent(temperatures[i])); bytes[33 + i] = 3;
        }
        QuadroProtocol.Seal(bytes); return bytes;
    }
}

internal sealed class QuadroConnection : IDisposable
{
    public NativeHid Hid { get; }
    public string Id => "quadro/" + Hid.Description.Serial;
    public QuadroReading? Latest => Volatile.Read(ref latest);
    public bool CanControl => Latest?.Firmware == 1033 && Structure == 3;
    public int Structure { get; }
    public string? Error => Volatile.Read(ref error);
    private QuadroReading? latest;
    private string? error;
    private readonly CancellationTokenSource stop = new();
    private readonly Task reader;
    private NativeWinUsb? output;
    public ControlRule[] Rules { get; private set; } = [];
    private string[] softwareInputs = [];
    public QuadroConnection(HidDescription descriptor, bool writable)
    {
        Hid = new(descriptor, writable);
        try { Structure = QuadroProtocol.ReadSettings(Hid.GetFeature(3, QuadroProtocol.SettingsLength)).Structure; }
        catch { Hid.Dispose(); throw; }
        reader = Task.Run(async () =>
        {
            while (!stop.IsCancellationRequested)
            {
                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop.Token); timeout.CancelAfter(3000);
                    var reading = QuadroProtocol.ReadStatus(await Hid.ReadAsync(timeout.Token));
                    if (reading.Serial != Hid.Description.Serial) throw new IOException("Device identity changed.");
                    Volatile.Write(ref latest, reading); Volatile.Write(ref error, null);
                }
                catch (OperationCanceledException) when (stop.IsCancellationRequested) { break; }
                catch (Exception e)
                {
                    Volatile.Write(ref error, e.Message);
                    try { await Task.Delay(1000, stop.Token); } catch (OperationCanceledException) { break; }
                }
            }
        });
    }
    public async Task Configure(ControlRule[] rules, Discovery catalog, string backupDirectory, CancellationToken token)
    {
        if (!CanControl) throw new NotSupportedException("QUADRO writes require verified firmware 1033 and settings structure 3.");
        // Ownership is acquired even for native-only rules, so a second writer cannot race settings.
        output ??= NativeWinUsb.Open(Hid.Description.Serial);
        string[] inputs = rules.Where(r => !IsNative(r, catalog)).Select(r => r.SensorId).Distinct().ToArray();
        var channels = new List<(int, int, ControlRule)>();
        foreach (var rule in rules)
        {
            var sensor = catalog.Sensors.Single(s => s.Id == rule.SensorId);
            int source = IsNative(rule, catalog) ? sensor.NativeIndex!.Value : 5 + Array.IndexOf(inputs, rule.SensorId);
            channels.Add((catalog.Outputs.Single(o => o.Id == rule.OutputId).Channel, source, rule));
        }
        foreach (var removed in Rules.Where(old => !rules.Any(r => r.OutputId == old.OutputId)))
        {
            // Releasing a QUADRO output parks it at its last configured fallback, never at zero.
            var parked = removed with { Minimum = removed.Fallback, Maximum = removed.Fallback, HoldMinimum = true,
                StartupTemperature = 0, Points = Curves.Straighten(new(0, 100), new(100, 100)) };
            channels.Add((int.Parse(removed.OutputId.Split('/')[^1], System.Globalization.CultureInfo.InvariantCulture), 0, parked));
        }
        var before = Hid.GetFeature(3, QuadroProtocol.SettingsLength);
        var candidate = QuadroControl.Build(before, channels);
        await Hid.ApplySettingsAsync(before, candidate, backupDirectory, token);
        Rules = rules; softwareInputs = inputs;
    }
    private bool IsNative(ControlRule rule, Discovery catalog) => string.IsNullOrEmpty(rule.SafetySensorId)
        && catalog.Sensors.Any(s => s.Id == rule.SensorId && s.DeviceId == Id && s.NativeIndex is >= 0 and < 4);
    public string? Feed(IReadOnlyDictionary<string, SensorValue> values, DateTimeOffset now)
    {
        if (softwareInputs.Length == 0) return null;
        if (Latest == null || now - Latest.Time > TimeSpan.FromSeconds(5) ||
            Rules.Where(r => softwareInputs.Contains(r.SensorId)).Any(r => !SoftwareControl.Safe(r, values, now)))
            return "QUADRO: software input unavailable or over its safety limit. Feed stopped; device fallback follows within about 15 seconds.";
        output!.Send(QuadroControl.SoftwareTemperatures(softwareInputs.Select(id => values[id].Value!.Value).ToArray()));
        return null;
    }
    public void Dispose()
    {
        stop.Cancel(); output?.Dispose(); Hid.Dispose();
        try { reader.GetAwaiter().GetResult(); } catch (Exception) { /* Reader is already stopped; disposal must release every device. */ }
        stop.Dispose();
    }
}

