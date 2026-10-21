using Lutra.Core;

namespace Lutra.Service;

internal enum GpuRecoveryAction { None, RefreshSensors, RestartProcess }
internal record GpuRecoveryCheckpoint(bool RestartUsed = false, DateTimeOffset? LastRestart = null);

/// <summary>Only missing configured GPU temperature inputs trigger recovery; real heat must retain fallback.</summary>
internal sealed class GpuRecovery(GpuRecoveryCheckpoint? checkpoint = null, bool allowProcessRestart = true)
{
    public GpuRecoveryCheckpoint Checkpoint { get; private set; } = checkpoint ?? new();
    private string[] inputs = [];
    private DateTimeOffset? missingSince, nextAttempt;
    private int refreshes, healthySamples;

    public GpuRecoveryAction Evaluate(AppConfiguration configuration, SensorValue[] values, DateTimeOffset now)
    {
        var watched = configuration.Curves.Where(r => r.Enabled).SelectMany(r => new[] { r.SensorId, r.SafetySensorId })
            .Where(id => id?.StartsWith("lhm/gpu-", StringComparison.Ordinal) == true && id.Contains("/temperature/", StringComparison.Ordinal))
            .Cast<string>().Distinct().Order(StringComparer.Ordinal).ToArray();
        if (!inputs.SequenceEqual(watched)) { inputs = watched; ResetAttempt(); }
        if (inputs.Length == 0) return GpuRecoveryAction.None;
        bool valid = inputs.All(id => values.Any(v => v.Id == id && v.IsFresh(now) && v.Value is >= 0 and <= 120));
        if (valid)
        {
            if (++healthySamples >= 3)
            {
                ResetAttempt();
                Checkpoint = Checkpoint with { RestartUsed = false };
            }
            return GpuRecoveryAction.None;
        }
        healthySamples = 0;
        missingSince ??= now;
        if (now - missingSince < TimeSpan.FromSeconds(15) || nextAttempt.HasValue && now < nextAttempt.Value)
            return GpuRecoveryAction.None;
        if (refreshes < 2)
        {
            refreshes++;
            nextAttempt = now.AddSeconds(30);
            return GpuRecoveryAction.RefreshSensors;
        }
        if (allowProcessRestart && !Checkpoint.RestartUsed &&
            (!Checkpoint.LastRestart.HasValue || now - Checkpoint.LastRestart.Value >= TimeSpan.FromMinutes(15)))
        {
            Checkpoint = new(true, now);
            nextAttempt = now.AddMinutes(5);
            return GpuRecoveryAction.RestartProcess;
        }
        // An absent GPU must not repeatedly hand CPU cooling back to firmware or restart the process.
        nextAttempt = now.AddMinutes(5);
        return GpuRecoveryAction.RefreshSensors;
    }

    private void ResetAttempt() { missingSince = nextAttempt = null; refreshes = healthySamples = 0; }
}
