namespace Lutra.Core;

/// <summary>Alarm timers use sample timestamps; stale readings never masquerade as a safe temperature.</summary>
public sealed class AlarmEngine
{
    private sealed record Entry(AlarmRule Rule, DateTimeOffset? Pending, DateTimeOffset? Active);
    private readonly Dictionary<string, Entry> entries = [];
    public AlarmEvent[] Evaluate(IEnumerable<AlarmRule> rules, IEnumerable<SensorValue> values, DateTimeOffset now)
    {
        var current = values.GroupBy(v => v.Id).ToDictionary(g => g.Key, g => g.OrderByDescending(v => v.Time).First());
        var enabled = rules.Where(r => r.Enabled).ToArray();
        foreach (var id in entries.Keys.Except(enabled.Select(r => r.Id)).ToArray()) entries.Remove(id);
        var result = new List<AlarmEvent>();
        foreach (var rule in enabled)
        {
            if (!entries.TryGetValue(rule.Id, out var state) || state.Rule != rule) state = new(rule, null, null);
            current.TryGetValue(rule.SensorId, out var reading);
            if (reading == null || !reading.IsFresh(now))
            {
                // A missing input is a separate fault; it does not silently acknowledge a live alarm.
                entries[rule.Id] = state with { Pending = null };
                result.Add(new(rule.Id, rule.Name, rule.SensorId, state.Active ?? now, null, true)); continue;
            }
            double value = reading.Value!.Value;
            bool triggered = rule.Direction == AlarmDirection.Above ? value >= rule.Threshold : value <= rule.Threshold;
            bool reset = rule.Direction == AlarmDirection.Above ? value < rule.Threshold - rule.Hysteresis : value > rule.Threshold + rule.Hysteresis;
            if (state.Active != null && reset) state = new(rule, null, null);
            if (state.Active == null)
            {
                if (!triggered) state = state with { Pending = null };
                else
                {
                    state = state with { Pending = state.Pending ?? now };
                    if (now - state.Pending >= TimeSpan.FromSeconds(rule.DelaySeconds)) state = state with { Active = now };
                }
            }
            entries[rule.Id] = state;
            if (state.Active is { } since) result.Add(new(rule.Id, rule.Name, rule.SensorId, since, value, false));
        }
        return result.ToArray();
    }
}
