using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lutra.Core;

public enum SensorKind { Temperature, Speed, Flow, Duty }
public record SensorInfo(string Id, string Name, string DeviceId, SensorKind Kind, string Unit, int? NativeIndex = null);
public record SensorValue(string Id, double? Value, DateTimeOffset Time)
{
    public bool IsFresh(DateTimeOffset now) => Value.HasValue && double.IsFinite(Value.Value) && now - Time <= TimeSpan.FromSeconds(5) && Time <= now.AddSeconds(2);
}
public record OutputInfo(string Id, string DeviceId, int Channel, string Name, bool CanControl, string? Limitation = null);
public record DeviceInfo(string Id, string Name, string Serial, int Firmware, string Driver, bool CanControl, string? Limitation = null);
public record Discovery(DeviceInfo[] Devices, SensorInfo[] Sensors, OutputInfo[] Outputs);
public record CurvePoint(double Temperature, double Power);
public record ControlRule
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = "Cooling curve";
    public string OutputId { get; init; } = "";
    public string SensorId { get; init; } = "";
    public bool Enabled { get; init; } = true;
    public bool IsPump { get; init; }
    public double Minimum { get; init; }
    public double Maximum { get; init; } = 100;
    public double Fallback { get; init; } = 100;
    public bool HoldMinimum { get; init; }
    public bool StartBoost { get; init; }
    public string? SafetySensorId { get; init; }
    public double SafetyTemperature { get; init; } = 45;
    public double StartupTemperature { get; init; } = 30;
    public CurvePoint[] Points { get; init; } = Curves.Straighten(new(25, 0), new(50, 100));
}
public enum AlarmDirection { Above, Below }
public record AlarmRule
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = "Temperature alarm";
    public string SensorId { get; init; } = "";
    public bool Enabled { get; init; } = true;
    public AlarmDirection Direction { get; init; }
    public double Threshold { get; init; } = 45;
    public double Hysteresis { get; init; } = 2;
    public int DelaySeconds { get; init; } = 5;
    public bool Notify { get; init; } = true;
}
public record AlarmEvent(string RuleId, string Name, string SensorId, DateTimeOffset Since, double? Value, bool SensorUnavailable);
public record AppConfiguration
{
    public int Version { get; init; } = 1;
    public string Language { get; init; } = "en";
    public int RetentionDays { get; init; } = 7;
    public bool MotherboardEnabled { get; init; }
    public ControlRule[] Curves { get; init; } = [];
    public AlarmRule[] Alarms { get; init; } = [];
}
public record TelemetryFrame(DateTimeOffset Time, SensorValue[] Values);
public record RuntimeState(DateTimeOffset Time, string Mode, Discovery Catalog, SensorValue[] Values,
    AlarmEvent[] Alarms, string[] Errors, AppConfiguration Configuration);
public record ServiceRequest(string Action, AppConfiguration? Configuration = null);
public record ServiceReply(bool Success, string Message, RuntimeState? State = null);
public static class JsonFiles
{
    public static readonly JsonSerializerOptions Options = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    public static T? Read<T>(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return JsonSerializer.Deserialize<T>(stream, Options);
    }
    public static void Save<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, JsonSerializer.Serialize(value, Options)); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
