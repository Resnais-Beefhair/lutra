namespace Lutra.App;

internal static class OverviewSensors
{
    internal static bool IsCpu(string id) => id.StartsWith("lhm/amdcpu/", StringComparison.Ordinal)
        || id.StartsWith("lhm/intelcpu/", StringComparison.Ordinal) || id.StartsWith("lhm/cpu-demo/", StringComparison.Ordinal);

    internal static SensorInfo[] Temperatures(Discovery catalog, ControlRule[] rules)
    {
        var available = catalog.Sensors.Where(s => s.Kind == SensorKind.Temperature).ToArray();
        var candidates = rules.SelectMany(r => new[] { r.SafetySensorId, r.SensorId }).Where(id => id != null).Distinct()
            .Select(id => available.FirstOrDefault(s => s.Id == id)).OfType<SensorInfo>().ToArray();
        string? cpuId = rules.Select(r => r.SensorId).FirstOrDefault(IsCpu);
        SensorInfo? cpu;
        if (cpuId != null)
        {
            // Keep the configured identity when discovery temporarily loses that sensor.
            // Its instrument will show unavailable instead of silently switching to another CPU reading.
            int separator = cpuId.LastIndexOf("/temperature/", StringComparison.Ordinal);
            cpu = available.FirstOrDefault(s => s.Id == cpuId)
                ?? new(cpuId, "CPU", separator >= 0 ? cpuId[..separator] : cpuId, SensorKind.Temperature, "°C");
        }
        else
        {
            cpu = available.Where(s => IsCpu(s.Id) && (s.Name.Contains("Tctl/Tdie", StringComparison.OrdinalIgnoreCase)
                    || s.Name.Contains("Package", StringComparison.OrdinalIgnoreCase)))
                .OrderBy(s => s.Name.Contains("Tctl/Tdie", StringComparison.OrdinalIgnoreCase) ? 0 : 1).FirstOrDefault();
        }
        var coolant = candidates.FirstOrDefault(s => rules.Any(r => r.IsPump && r.SafetySensorId == s.Id));
        var pumpInput = candidates.FirstOrDefault(s => rules.Any(r => r.IsPump && r.SensorId == s.Id));
        return new[] { coolant, pumpInput, cpu }.OfType<SensorInfo>()
            .Concat(candidates.Length > 0 ? candidates : available).DistinctBy(s => s.Id).Take(3).ToArray();
    }
}
