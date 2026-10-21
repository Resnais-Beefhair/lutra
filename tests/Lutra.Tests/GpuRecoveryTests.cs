using System.Text.Json;
using Lutra.Core;
using Lutra.Service;

internal static class GpuRecoveryTests
{
    public static void Run(Action<string, Action> test)
    {
        const string gpu = "lhm/gpu-nvidia/0/temperature/0", water = "quadro/test/temperature/0";
        var start = new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);
        var rule = new ControlRule { SensorId = gpu, OutputId = "quadro/test/output/1", IsPump = true,
            Minimum = 20, HoldMinimum = true, Fallback = 80, SafetySensorId = water };
        var config = new AppConfiguration { Curves = [rule] };
        SensorValue[] Healthy(DateTimeOffset time, double temperature = 39) => [new(gpu, temperature, time), new(water, 38, time)];
        void Expect(GpuRecoveryAction expected, GpuRecoveryAction actual) { if (actual != expected) throw new Exception($"Expected {expected}; got {actual}"); }
        void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }

        test("GPU loss triggers two GPU-only refreshes before one clean-process recovery", () =>
        {
            var recovery = new GpuRecovery();
            Expect(GpuRecoveryAction.None, recovery.Evaluate(config, [], start));
            Expect(GpuRecoveryAction.None, recovery.Evaluate(config, [], start.AddSeconds(14.9)));
            Expect(GpuRecoveryAction.RefreshSensors, recovery.Evaluate(config, [], start.AddSeconds(15)));
            Expect(GpuRecoveryAction.None, recovery.Evaluate(config, [], start.AddSeconds(44)));
            Expect(GpuRecoveryAction.RefreshSensors, recovery.Evaluate(config, [], start.AddSeconds(45)));
            Expect(GpuRecoveryAction.RestartProcess, recovery.Evaluate(config, [], start.AddSeconds(75)));
            Assert(recovery.Checkpoint.RestartUsed && recovery.Checkpoint.LastRestart == start.AddSeconds(75), "Restart budget was not consumed before restart");
        });
        test("A permanently absent GPU cannot create a restart loop across processes", () =>
        {
            var recovery = new GpuRecovery();
            foreach (int seconds in new[] { 0, 15, 45, 75 }) recovery.Evaluate(config, [], start.AddSeconds(seconds));
            var saved = JsonSerializer.Deserialize<GpuRecoveryCheckpoint>(JsonSerializer.Serialize(recovery.Checkpoint, JsonFiles.Options), JsonFiles.Options)!;
            var nextProcess = new GpuRecovery(saved);
            int refreshes = 0;
            for (int seconds = 80; seconds < 4000; seconds++)
            {
                var action = nextProcess.Evaluate(config, [], start.AddSeconds(seconds));
                Assert(action != GpuRecoveryAction.RestartProcess, "Same outage caused another process restart");
                if (action == GpuRecoveryAction.RefreshSensors) refreshes++;
            }
            Assert(refreshes < 17, "Persistent loss caused rapid GPU refreshes");
        });
        test("GPU recovery requires three valid samples and retains the cross-outage restart cooldown", () =>
        {
            var recovery = new GpuRecovery(new(true, start));
            recovery.Evaluate(config, Healthy(start.AddSeconds(10)), start.AddSeconds(10));
            recovery.Evaluate(config, Healthy(start.AddSeconds(11)), start.AddSeconds(11));
            Assert(recovery.Checkpoint.RestartUsed, "Transient good readings erased the outage budget");
            recovery.Evaluate(config, Healthy(start.AddSeconds(12)), start.AddSeconds(12));
            Assert(!recovery.Checkpoint.RestartUsed && recovery.Checkpoint.LastRestart == start, "Stable recovery must retain last-restart time");
            foreach (int seconds in new[] { 20, 35, 65, 95 })
                Assert(recovery.Evaluate(config, [], start.AddSeconds(seconds)) != GpuRecoveryAction.RestartProcess, "Cooldown was bypassed");
            Expect(GpuRecoveryAction.RestartProcess, recovery.Evaluate(config, [], start.AddSeconds(1000)));
        });
        test("GPU stale, missing, future, non-finite and out-of-range inputs trigger recovery", () =>
        {
            foreach (var reading in new[] { new SensorValue(gpu, null, start), new(gpu, 39, start.AddSeconds(-6)),
                new(gpu, 39, start.AddMinutes(1)), new(gpu, double.NaN, start), new(gpu, -1, start), new(gpu, 121, start) })
            {
                var recovery = new GpuRecovery();
                recovery.Evaluate(config, [reading], start);
                Expect(GpuRecoveryAction.RefreshSensors, recovery.Evaluate(config, [reading], start.AddSeconds(15)));
            }
        });
        test("A different GPU temperature cannot silently replace the selected missing input", () =>
        {
            var recovery = new GpuRecovery();
            recovery.Evaluate(config, [new("lhm/gpu-nvidia/0/temperature/9", 39, start)], start);
            Expect(GpuRecoveryAction.RefreshSensors, recovery.Evaluate(config,
                [new("lhm/gpu-nvidia/0/temperature/9", 39, start.AddSeconds(15))], start.AddSeconds(15)));
        });
        test("Actual high GPU or coolant temperature preserves safety fallback without reconnecting", () =>
        {
            foreach (double temperature in new[] { 39.0, 95.0, 120.0 })
            {
                var recovery = new GpuRecovery();
                for (int seconds = 0; seconds < 100; seconds++)
                {
                    var time = start.AddSeconds(seconds);
                    SensorValue[] values = [new(gpu, temperature, time), new(water, 48, time)];
                    Expect(GpuRecoveryAction.None, recovery.Evaluate(config, values, time));
                    Assert(new SoftwareControl().Evaluate(rule, values.ToDictionary(v => v.Id), time) == rule.Fallback,
                        "Recovery weakened the hot-water fallback");
                }
            }
        });
        test("Disabled GPU rules and alarm-only monitoring do not restart cooling control", () =>
        {
            var inactive = config with { Curves = [rule with { Enabled = false }], Alarms = [new() { SensorId = gpu }] };
            var recovery = new GpuRecovery();
            for (int seconds = 0; seconds < 150; seconds++) Expect(GpuRecoveryAction.None, recovery.Evaluate(inactive, [], start.AddSeconds(seconds)));
        });
        test("Changing the selected GPU input starts a fresh loss grace period", () =>
        {
            var recovery = new GpuRecovery();
            recovery.Evaluate(config, [], start);
            recovery.Evaluate(config, [], start.AddSeconds(15));
            var changed = config with { Curves = [rule with { SensorId = "lhm/gpu-nvidia/1/temperature/0" }] };
            Expect(GpuRecoveryAction.None, recovery.Evaluate(changed, [], start.AddSeconds(45)));
            Expect(GpuRecoveryAction.RefreshSensors, recovery.Evaluate(changed, [], start.AddSeconds(60)));
        });
        test("Console recovery and a future restart timestamp never cause an early process exit", () =>
        {
            foreach (var recovery in new[] { new GpuRecovery(allowProcessRestart: false), new GpuRecovery(new(false, start.AddDays(1))) })
                for (int seconds = 0; seconds < 500; seconds++)
                    Assert(recovery.Evaluate(config, [], start.AddSeconds(seconds)) != GpuRecoveryAction.RestartProcess, "Unexpected process exit");
        });
        test("GPU loss keeps the pump at fallback while discovery is retried", () =>
        {
            var recovery = new GpuRecovery();
            var control = new SoftwareControl();
            for (int seconds = 0; seconds <= 75; seconds++)
            {
                var time = start.AddSeconds(seconds);
                SensorValue[] values = [new(water, 38, time)];
                recovery.Evaluate(config, values, time);
                Assert(control.Evaluate(rule, values.ToDictionary(v => v.Id), time) == 80, "Pump left fallback without a valid selected GPU input");
            }
        });
    }
}
