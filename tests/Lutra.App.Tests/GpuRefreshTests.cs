using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using Lutra.App;
using Lutra.Core;

internal static class GpuRefreshTests
{
    internal static void Run(Action<string, Action> test)
    {
        test("GPU discovery is available without a detected GPU, including service fault states", () => WithWindow(false, window =>
        {
            Assert(!Button(window).IsEnabled);
            Set(window, "canApply", true);
            var state = (RuntimeState)Get(window, "state")!;
            Set(window, "state", state with { Errors = ["GPU sensor unavailable"] });
            Invoke(window, "Refresh");
            Assert(Button(window).IsEnabled && state.Catalog.Devices.Length == 0);
            Assert(AutomationProperties.GetName(Button(window)) == "Rediscover GPU sensors");
        }));
        test("GPU discovery never contacts hardware in demo or disconnected mode", () =>
        {
            foreach (bool demo in new[] { true, false }) WithWindow(demo, window =>
            {
                int sent = 0;
                window.RefreshGpuSensorsAsync(_ => { sent++; return Task.FromResult(new ServiceReply(true, "")); }).GetAwaiter().GetResult();
                Assert(sent == 0 && !Button(window).IsEnabled);
            });
        });
        test("GPU discovery preserves drafts, serializes requests and survives page rebuilds", () => WithWindow(false, window =>
        {
            Set(window, "canApply", true);
            var configuration = Demo.Configuration(); Set(window, "configuration", configuration); Set(window, "dirty", true);
            int captured = 0, sent = 0;
            Set(window, "captureForm", (Action)(() => captured++));
            var completion = new TaskCompletionSource<ServiceReply>();
            var operation = window.RefreshGpuSensorsAsync(request =>
            {
                sent++; Assert(request.Action == "refresh-gpu" && request.Configuration == null);
                return completion.Task;
            });
            Assert(!Button(window).IsEnabled && (bool)Get(window, "busy")!);
            window.RefreshGpuSensorsAsync(_ => { sent++; return Task.FromResult(new ServiceReply(true, "")); }).GetAwaiter().GetResult();
            Ui.Language = "fi"; Invoke(window, "Render");
            Assert(!Button(window).IsEnabled && (string)Button(window).Content == "Käynnistetään tunnistusta…");
            completion.SetResult(new(true, "Discovery restarted")); Wait(operation);
            Assert(sent == 1 && captured == 0 && ReferenceEquals(configuration, Get(window, "configuration")) && (bool)Get(window, "dirty")!);
            Assert(Button(window).IsEnabled && Text(window).Contains("Tunnistus käynnistetty") && Text(window).Contains("varateho"));
            Ui.Language = "en"; Invoke(window, "Render");
            Assert(Text(window).Contains("Discovery restarted") && Text(window).Contains("while a sensor is missing"));
        }));
        test("GPU discovery reports refusal, disconnect and timeout without changing settings", () => WithWindow(false, window =>
        {
            Set(window, "canApply", true); var configuration = Get(window, "configuration");
            foreach (Func<ServiceRequest, Task<ServiceReply>> send in new Func<ServiceRequest, Task<ServiceReply>>[] {
                _ => Task.FromResult(new ServiceReply(false, "Conflicting cooling service")),
                _ => throw new IOException("Pipe disconnected"),
                _ => throw new OperationCanceledException()
            })
            {
                window.RefreshGpuSensorsAsync(send).GetAwaiter().GetResult();
                Assert(Button(window).IsEnabled && Text(window).Contains("Discovery could not restart") && !(bool)Get(window, "busy")!);
                Assert(ReferenceEquals(configuration, Get(window, "configuration")));
            }
            Assert(Text(window).Contains("did not reply in time"));
        }));
        test("GPU discovery cannot overlap applying settings or an application update", () => WithWindow(false, window =>
        {
            Set(window, "canApply", true);
            foreach (string field in new[] { "busy", "updateBusy" })
            {
                Set(window, field, true); Invoke(window, "Refresh"); Assert(!Button(window).IsEnabled);
                int sent = 0;
                window.RefreshGpuSensorsAsync(_ => { sent++; return Task.FromResult(new ServiceReply(true, "")); }).GetAwaiter().GetResult();
                Assert(sent == 0); Set(window, field, false);
            }
        }));
    }

    private static void WithWindow(bool demo, Action<MainWindow> test)
    {
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        var window = new MainWindow((demo ? new[] { "--demo" } : Array.Empty<string>()).Concat(new[] { "--capture", "unused.png", "--page", "devices", "--language", "en" }).ToArray());
        try { test(window); }
        finally { ((IDisposable)Get(window, "tray")!).Dispose(); SynchronizationContext.SetSynchronizationContext(previous); Ui.Language = "en"; }
    }
    private static void Wait(Task task)
    {
        var frame = new DispatcherFrame();
        var timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        timeout.Tick += (_, _) => { frame.Continue = false; };
        _ = task.ContinueWith(_ => Dispatcher.CurrentDispatcher.BeginInvoke(() => frame.Continue = false), TaskScheduler.FromCurrentSynchronizationContext());
        timeout.Start(); if (!task.IsCompleted) Dispatcher.PushFrame(frame); timeout.Stop();
        Assert(task.IsCompleted); task.GetAwaiter().GetResult();
    }
    private static FieldInfo Field(string name) => typeof(MainWindow).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static object? Get(MainWindow window, string name) => Field(name).GetValue(window);
    private static void Set(MainWindow window, string name, object value) => Field(name).SetValue(window, value);
    private static void Invoke(MainWindow window, string name) => typeof(MainWindow).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)!.Invoke(window, null);
    private static Button Button(MainWindow window) => (Button)Get(window, "gpuRefreshButton")!;
    private static string Text(MainWindow window) => ((TextBlock)Get(window, "gpuRefreshStatus")!).Text;
    private static void Assert(bool value) { if (!value) throw new Exception("Assertion failed"); }
}
