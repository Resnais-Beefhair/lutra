namespace Lutra.Core;

public static class Curves
{
    public static CurvePoint[] Straighten(CurvePoint first, CurvePoint last)
    {
        if (!Valid(first.Temperature, 0, 100) || !Valid(last.Temperature, 0, 100) || !Valid(first.Power, 0, 100)
            || !Valid(last.Power, first.Power, 100)) throw new InvalidDataException("Curve endpoints must be between 0 and 100 and rise from left to right.");
        first = new(Math.Round(first.Temperature, 2), Math.Round(first.Power, 2));
        last = new(Math.Round(last.Temperature, 2), Math.Round(last.Power, 2));
        int start = (int)Math.Round(first.Temperature * 100), end = (int)Math.Round(last.Temperature * 100);
        if (end - start < 15) throw new InvalidDataException("The temperature span must be at least 0.15 °C.");
        return Enumerable.Range(0, 16).Select(i =>
        {
            int t = start + (int)Math.Round((end - start) * i / 15.0, MidpointRounding.AwayFromZero);
            return new CurvePoint(t / 100.0, Math.Round(first.Power + (last.Power - first.Power) * (t - start) / (end - start), 2));
        }).ToArray();
    }
    public static double Interpolate(IReadOnlyList<CurvePoint> points, double temperature)
    {
        if (!double.IsFinite(temperature) || points.Count == 0) throw new ArgumentException("Invalid temperature or empty curve.");
        if (temperature <= points[0].Temperature) return points[0].Power;
        for (int i = 1; i < points.Count; i++)
            if (temperature <= points[i].Temperature)
            {
                var a = points[i - 1]; var b = points[i];
                return a.Power + (b.Power - a.Power) * (temperature - a.Temperature) / (b.Temperature - a.Temperature);
            }
        return points[^1].Power;
    }
    public static double Output(ControlRule rule, double temperature)
    {
        double raw = Interpolate(rule.Points, temperature);
        return raw == 0 && !rule.HoldMinimum ? 0 : rule.Minimum + raw / 100 * (rule.Maximum - rule.Minimum);
    }
    public static bool Valid(double value, double minimum, double maximum) => double.IsFinite(value) && value >= minimum && value <= maximum;
    public static void Validate(ControlRule rule)
    {
        if (string.IsNullOrWhiteSpace(rule.Id) || string.IsNullOrWhiteSpace(rule.Name) || rule.Name.Length > 100)
            throw new InvalidDataException("Give the curve a name (1–100 characters).");
        if (string.IsNullOrWhiteSpace(rule.OutputId) || string.IsNullOrWhiteSpace(rule.SensorId)) throw new InvalidDataException("Select an input sensor and an output channel.");
        if (!Valid(rule.Minimum, 0, 100) || !Valid(rule.Maximum, rule.Minimum, 100) || !Valid(rule.Fallback, rule.Minimum, 100)
            || !Valid(rule.StartupTemperature, 0, 100)) throw new InvalidDataException("Output limits must be between 0 and 100%; fallback must be at least the minimum.");
        if (rule.IsPump && (!rule.HoldMinimum || rule.Minimum <= 0 || rule.Fallback < 50))
            throw new InvalidDataException("A pump must hold a non-zero minimum and use at least 50% fallback. Check its manufacturer's minimum PWM duty.");
        if (!Valid(rule.SafetyTemperature, 0, 120)) throw new InvalidDataException("Invalid over-temperature limit.");
        if (rule.Points is not { Length: 16 }) throw new InvalidDataException("A curve needs exactly 16 points.");
        double previousT = -1, previousPower = -1;
        foreach (var point in rule.Points)
        {
            if (point is null || !Valid(point.Temperature, 0, 100) || !Valid(point.Power, 0, 100)
                || Math.Round(point.Temperature * 100) <= previousT || point.Power < previousPower)
                throw new InvalidDataException("Curve temperatures must increase by at least 0.01 °C; output cannot decrease.");
            previousT = Math.Round(point.Temperature * 100); previousPower = point.Power;
        }
        if (rule.Points[^1].Power == 0) throw new InvalidDataException("The curve must rise above zero at its warm end.");
        if (rule.StartupTemperature >= rule.Points[^1].Temperature) throw new InvalidDataException("Startup must be below the last curve temperature.");
    }
    public static void Validate(AppConfiguration configuration, Discovery? catalog = null)
    {
        if (configuration.Version != 1 || configuration.Language is not ("en" or "fi") || configuration.RetentionDays is < 1 or > 30)
            throw new InvalidDataException("Unsupported configuration version, language or history retention (1–30 days).");
        if (configuration.Curves is null || configuration.Alarms is null || configuration.Curves.Length > 64 || configuration.Alarms.Length > 64)
            throw new InvalidDataException("Too many rules or missing rule lists.");
        if (configuration.Curves.Any(c => c is null) || configuration.Alarms.Any(a => a is null)) throw new InvalidDataException("Rule cannot be null.");
        if (configuration.Curves.Select(c => c.Id).Distinct().Count() != configuration.Curves.Length || configuration.Alarms.Select(a => a.Id).Distinct().Count() != configuration.Alarms.Length)
            throw new InvalidDataException("Rule identifiers must be unique.");
        if (configuration.Curves.Where(c => c.Enabled).Select(c => c.OutputId).Distinct().Count() != configuration.Curves.Count(c => c.Enabled))
            throw new InvalidDataException("Only one active curve may control each output channel.");
        foreach (var rule in configuration.Curves)
        {
            Validate(rule);
            if (catalog != null && rule.Enabled)
            {
                if (!catalog.Outputs.Any(o => o.Id == rule.OutputId && o.CanControl)) throw new InvalidDataException("The selected output is unavailable or read-only.");
                if (!catalog.Sensors.Any(s => s.Id == rule.SensorId && s.Kind == SensorKind.Temperature)) throw new InvalidDataException("The selected temperature input is unavailable.");
                if (!string.IsNullOrEmpty(rule.SafetySensorId) && !catalog.Sensors.Any(s => s.Id == rule.SafetySensorId && s.Kind == SensorKind.Temperature)) throw new InvalidDataException("The safety temperature input is unavailable.");
            }
        }
        foreach (var alarm in configuration.Alarms)
        {
            if (string.IsNullOrWhiteSpace(alarm.Id) || string.IsNullOrWhiteSpace(alarm.Name) || alarm.Name.Length > 100 || string.IsNullOrWhiteSpace(alarm.SensorId)
                || !Valid(alarm.Threshold, -50, 150) || !Valid(alarm.Hysteresis, 0, 50) || alarm.DelaySeconds is < 0 or > 600
                || !Enum.IsDefined(alarm.Direction)) throw new InvalidDataException("Invalid alarm: check its sensor, threshold, delay and reset margin.");
            if (catalog != null && alarm.Enabled && !catalog.Sensors.Any(s => s.Id == alarm.SensorId && s.Kind == SensorKind.Temperature))
                throw new InvalidDataException("The alarm requires an available temperature sensor.");
        }
    }
}
