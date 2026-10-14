namespace Lutra.Core;

/// <summary>Startup latch matches the native curve: stop at its last zero point, start at the chosen threshold.</summary>
public sealed class SoftwareControl
{
    private ControlRule? previous;
    private bool running;
    private DateTimeOffset boostUntil;
    public static bool Safe(ControlRule rule, IReadOnlyDictionary<string, SensorValue> values, DateTimeOffset now)
        => values.TryGetValue(rule.SensorId, out var input) && input.IsFresh(now) && input.Value is >= 0 and <= 120
        && (string.IsNullOrEmpty(rule.SafetySensorId) || values.TryGetValue(rule.SafetySensorId, out var safety)
            && safety.IsFresh(now) && safety.Value < rule.SafetyTemperature);
    public double Evaluate(ControlRule rule, IReadOnlyDictionary<string, SensorValue> values, DateTimeOffset now)
    {
        if (previous != rule) { running = false; boostUntil = default; previous = rule; }
        if (!Safe(rule, values, now)) { running = false; return rule.Fallback; }
        double t = values[rule.SensorId].Value!.Value;
        double raw = Curves.Interpolate(rule.Points, t);
        if (raw <= 0) running = false;
        if (!running && raw > 0 && (t >= rule.StartupTemperature || rule.Points[0].Power > 0))
        { running = true; if (rule.StartBoost) boostUntil = now.AddSeconds(2); }
        if (!running) return rule.HoldMinimum ? rule.Minimum : 0;
        return now < boostUntil ? rule.Maximum : Curves.Output(rule, t);
    }
}
