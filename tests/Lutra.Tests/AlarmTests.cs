using System.Text.Json;
using Lutra.Core;
using Lutra.Hardware;

internal static class AlarmTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);
    private static AlarmRule Rule => new() { Id = "water", SensorId = "water", Threshold = 45, Hysteresis = 2, DelaySeconds = 5 };
    private static SensorValue Reading(double? value, double seconds, string sensor = "water") => new(sensor, value, Start.AddSeconds(seconds));
    private static AlarmEvent[] At(AlarmEngine engine, AlarmRule rule, double value, double seconds)
        => engine.Evaluate([rule], [Reading(value, seconds, rule.SensorId)], Start.AddSeconds(seconds));
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Near(double expected, double actual) => Assert(Math.Abs(expected - actual) <= .001, $"Expected {expected}; got {actual}");

    public static void Run(Action<string, Action> test)
    {
        test("Alarm delay restarts after a below-threshold interruption", () =>
        {
            var engine = new AlarmEngine(); var rule = Rule;
            Assert(At(engine, rule, 45, 0).Length == 0, "Alarm must wait for its delay");
            Assert(At(engine, rule, 44.99, 4).Length == 0, "Below-threshold reading must interrupt pending alarm");
            Assert(At(engine, rule, 45, 5).Length == 0, "The original timer must not survive interruption");
            Assert(At(engine, rule, 45, 9.99).Length == 0, "The replacement timer must run for the full delay");
            Assert(At(engine, rule, 45, 10).Single().Since == Start.AddSeconds(10), "Alarm should activate at the new deadline");
        });
        test("Above alarm includes threshold and retains exact reset-margin boundary", () =>
        {
            var engine = new AlarmEngine(); var rule = Rule with { DelaySeconds = 0 };
            Assert(At(engine, rule, 44.99, 0).Length == 0, "A value below threshold must not trigger");
            var active = At(engine, rule, 45, 1).Single();
            Assert(!active.SensorUnavailable, "Exact threshold must trigger a temperature event");
            Assert(At(engine, rule, 43, 2).Single().Since == active.Since, "Exact reset boundary must retain the alarm");
            Assert(At(engine, rule, 42.99, 3).Length == 0, "Crossing the reset boundary must clear the alarm");
        });
        test("Stale pending input restarts delay while stale active input retains alarm", () =>
        {
            var rule = Rule; var engine = new AlarmEngine();
            At(engine, rule, 45, 0);
            var missing = engine.Evaluate([rule], [Reading(45, 0)], Start.AddSeconds(6)).Single();
            Assert(missing.SensorUnavailable && missing.Value == null, "Stale high temperature is a missing-input fault, not a valid trigger");
            Assert(At(engine, rule, 45, 7).Length == 0, "Fresh input must start a new timer after a stale pending input");
            Assert(At(engine, rule, 45, 11.99).Length == 0, "The restarted delay must be complete");
            var active = At(engine, rule, 45, 12).Single();
            var stale = engine.Evaluate([rule], [Reading(45, 12)], Start.AddSeconds(18)).Single();
            Assert(stale.SensorUnavailable && stale.Since == active.Since, "An active alarm must survive stale input");
            Assert(At(engine, rule, 44, 19).Single().Since == active.Since, "Recovery inside hysteresis must retain prior activation");
            Assert(At(engine, rule, 42.99, 20).Length == 0, "Only a valid safe reading may clear the active alarm");
        });
        test("Below alarm mirrors delay and exact hysteresis boundaries", () =>
        {
            var engine = new AlarmEngine(); var rule = Rule with { Direction = AlarmDirection.Below, Threshold = 10, Hysteresis = 2 };
            Assert(At(engine, rule, 10.01, 0).Length == 0, "A value above a low threshold must not trigger");
            Assert(At(engine, rule, 10, 1).Length == 0, "Exact low threshold starts the pending timer");
            Assert(At(engine, rule, 10, 5.99).Length == 0, "Low-temperature delay must not expire early");
            var active = At(engine, rule, 10, 6).Single();
            Assert(At(engine, rule, 12, 7).Single().Since == active.Since, "Exact upper reset boundary must retain low alarm");
            Assert(At(engine, rule, 12.01, 8).Length == 0, "Above upper reset boundary must clear low alarm");
        });
        test("Disabling or removing an alarm discards active and pending state", () =>
        {
            foreach (bool remove in new[] { false, true })
            foreach (bool activeFirst in new[] { false, true })
            {
                var engine = new AlarmEngine(); var rule = Rule;
                At(engine, rule, 45, 0);
                if (activeFirst) Assert(At(engine, rule, 45, 5).Length == 1, "Setup must activate the alarm");
                AlarmRule[] replacement = remove ? [] : [rule with { Enabled = false }];
                Assert(engine.Evaluate(replacement, [Reading(45, 6)], Start.AddSeconds(6)).Length == 0, "Disabled or removed rules must not emit events");
                Assert(At(engine, rule, 45, 7).Length == 0, "Re-enabled or re-added rule must start fresh");
                Assert(At(engine, rule, 45, 11.99).Length == 0, "Old pending time must not leak into replacement");
                Assert(At(engine, rule, 45, 12).Length == 1, "Replacement rule must trigger after its full delay");
            }
        });
        test("Changing an alarm rule resets both active and pending timers", () =>
        {
            foreach (bool activeFirst in new[] { false, true })
            {
                var engine = new AlarmEngine(); var rule = Rule;
                At(engine, rule, 46, 0);
                if (activeFirst) Assert(At(engine, rule, 46, 5).Length == 1, "Setup must activate the alarm");
                rule = rule with { Threshold = 46, DelaySeconds = 10 };
                Assert(At(engine, rule, 46, 6).Length == 0, "Changed rule must not inherit old timer or active state");
                Assert(At(engine, rule, 46, 15.99).Length == 0, "Changed delay must run in full");
                Assert(At(engine, rule, 46, 16).Single().Since == Start.AddSeconds(16), "Changed rule must use its new activation time");
            }
        });
        test("Quiet-loop temperature warnings use independent delays and recovery margins", () =>
        {
            AlarmRule[] rules =
            [
                new() { Id = "water-warning", SensorId = "water", Threshold = 43, DelaySeconds = 60, Hysteresis = 2 },
                new() { Id = "water-high", SensorId = "water", Threshold = 45, DelaySeconds = 5, Hysteresis = 2 },
                new() { Id = "gpu-warning", SensorId = "gpu", Threshold = 75, DelaySeconds = 15, Hysteresis = 5 },
                new() { Id = "gpu-high", SensorId = "gpu", Threshold = 85, DelaySeconds = 5, Hysteresis = 5 }
            ];
            Curves.Validate(new AppConfiguration { Alarms = rules });
            var idle = new AlarmEngine();
            foreach (int seconds in new[] { 0, 60, 600 })
                Assert(idle.Evaluate(rules, [Reading(38, seconds), Reading(39, seconds, "gpu")], Start.AddSeconds(seconds)).Length == 0,
                    "Quiet idle temperatures must not trigger any configured alarm");
            foreach (var rule in rules)
            {
                var engine = new AlarmEngine();
                Assert(At(engine, rule, rule.Threshold, 0).Length == 0, "Profile alarm must wait for its configured delay");
                Assert(At(engine, rule, rule.Threshold, rule.DelaySeconds - .01).Length == 0, "Profile delay must not expire early");
                Assert(At(engine, rule, rule.Threshold, rule.DelaySeconds).Single().RuleId == rule.Id, "Each profile alarm must trigger at its own deadline");
                Assert(At(engine, rule, rule.Threshold - rule.Hysteresis, rule.DelaySeconds + 1).Length == 1, "Profile reset margin must retain exact boundary");
                Assert(At(engine, rule, rule.Threshold - rule.Hysteresis - .01, rule.DelaySeconds + 2).Length == 0, "Profile reset margin must clear after crossing boundary");
            }
            var simultaneous = new AlarmEngine();
            AlarmEvent[] BothHot(int seconds) => simultaneous.Evaluate(rules,
                [Reading(45, seconds), Reading(85, seconds, "gpu")], Start.AddSeconds(seconds));
            Assert(BothHot(0).Length == 0, "Independent alarms begin pending");
            Assert(BothHot(5).Select(a => a.RuleId).Order().SequenceEqual(new[] { "gpu-high", "water-high" }), "High alarms must not wait for warning delays");
            Assert(BothHot(15).Length == 3, "GPU warning uses its own 15-second delay");
            Assert(BothHot(60).Length == 4, "Water warning uses its own 60-second delay");
        });
        test("Quiet-loop pump reaches configured maximum while native fan curve stays unchanged", () =>
        {
            var pump = new ControlRule { Id = "pump", Name = "Pump", SensorId = "gpu", OutputId = "out/1", IsPump = true,
                Minimum = 20, Maximum = 79.83, Fallback = 79.98, HoldMinimum = true, StartBoost = false,
                StartupTemperature = 40, SafetySensorId = "water", SafetyTemperature = 45,
                Points = Curves.Straighten(new(40, 0), new(50, 100)) };
            var fan = new ControlRule { Id = "fan", Name = "Radiator", SensorId = "water", OutputId = "out/2",
                StartupTemperature = 40, Points = Curves.Straighten(new(39.25, 0), new(45, 100)) };
            Curves.Validate(new AppConfiguration { Curves = [pump, fan] });
            var engine = new SoftwareControl();
            double PumpAt(double gpu) => engine.Evaluate(pump, new Dictionary<string, SensorValue>
                { ["gpu"] = Reading(gpu, 0, "gpu"), ["water"] = Reading(38, 0) }, Start);
            foreach (double gpu in new[] { 0d, 38, 39, 40 }) Near(20, PumpAt(gpu));
            Near(49.915, PumpAt(45)); Near(79.83, PumpAt(50)); Near(79.83, PumpAt(85));
            Near(20, PumpAt(39));
            var fans = new SoftwareControl();
            double FansAt(double water) => fans.Evaluate(fan, new Dictionary<string, SensorValue> { ["water"] = Reading(water, 0) }, Start);
            Near(0, FansAt(38)); Near(0, FansAt(39.99));
            Assert(FansAt(40) > 0, "Fans must start at 40 degrees");
            Assert(FansAt(39.5) > 0, "Running fans retain their existing stop hysteresis");
            Near(0, FansAt(39.25)); Near(100, FansAt(45));
            byte[] baseline = new byte[QuadroProtocol.SettingsLength]; baseline[0] = 3;
            QuadroProtocol.Put16(baseline, 1, 3); QuadroProtocol.Seal(baseline);
            var existing = QuadroControl.Build(baseline, [(2, 0, fan)]);
            var revised = QuadroControl.Build(existing, [(1, 5, pump)]);
            var beforeFan = QuadroProtocol.ReadSettings(existing).Channels[1];
            var afterFan = QuadroProtocol.ReadSettings(revised).Channels[1];
            Assert(JsonSerializer.Serialize(beforeFan) == JsonSerializer.Serialize(afterFan), "Pump update must leave all native fan settings intact");
        });
    }
}
