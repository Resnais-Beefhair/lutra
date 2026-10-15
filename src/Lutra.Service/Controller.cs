using System.Collections.Concurrent;
using System.Diagnostics;
using Lutra.Core;
using Lutra.Hardware;

namespace Lutra.Service;

internal sealed class Controller
{
    private sealed record Pending(ServiceRequest Request, TaskCompletionSource<ServiceReply> Reply, CancellationToken Token);
    private readonly ConcurrentQueue<Pending> requests = new();
    private readonly string directory = ControlMessages.DataDirectory;
    private RuntimeState state = new(DateTimeOffset.UtcNow, "Starting", new([], [], []), [], [], [], new());
    private long lastTick = Environment.TickCount64;
    private string lastLog = "";
    private readonly object logGate = new();
    public async Task RunAsync(CancellationToken token)
    {
        using var single = new Mutex(false, @"Global\Lutra.Control.Owner", out bool first);
        if (!first) throw new IOException("Another Lutra controller is already running.");
        using var ipcStop = CancellationTokenSource.CreateLinkedTokenSource(token);
        var ipc = new ControlServer(directory, HandleRequest, Log);
        var server = ipc.RunAsync(ipcStop.Token);
        // If a vendor read blocks the control thread, SCM restarts the process. QUADRO uses its own watchdog.
        using var watchdog = new Timer(_ => { if (Environment.TickCount64 - Interlocked.Read(ref lastTick) > 20000) Environment.Exit(2); }, null, 5000, 5000);
        HardwareSession? hardware = null;
        var configPath = Path.Combine(directory, "configuration.json");
        var configuration = File.Exists(configPath) ? JsonFiles.Read<AppConfiguration>(configPath) ?? new() : new AppConfiguration();
        Curves.Validate(configuration);
        var history = new HistoryStore(Path.Combine(directory, "history"));
        var alarms = new AlarmEngine();
        bool configured = false;
        DateTimeOffset retryAfter = default;
        try
        {
            while (!token.IsCancellationRequested)
            {
                Interlocked.Exchange(ref lastTick, Environment.TickCount64);
                var errors = new List<string>(); SensorValue[] values = [];
                try
                {
                    hardware ??= new(true, configuration.MotherboardEnabled);
                    values = hardware.Update();
                    errors.AddRange(hardware.Errors);
                    if (hardware.NeedsReconnect) throw new IOException("Device disconnected; rediscovering hardware.");
                    bool competing = HasOtherController();
                    if (!configured && !competing && DateTimeOffset.UtcNow >= retryAfter)
                    {
                        try
                        {
                            Curves.Validate(configuration, hardware.Catalog);
                            await hardware.Configure(configuration, Path.Combine(directory, "backups"), token);
                            configured = true;
                        }
                        catch (Exception e) { errors.Add(e.GetBaseException().Message); retryAfter = DateTimeOffset.UtcNow.AddSeconds(10); }
                    }
                    if (competing)
                    {
                        errors.Add("Another cooling service is running. Lutra will not take control.");
                        // Do not write even to release a QUADRO when another writer appeared.
                        if (configured) { hardware.Dispose(); hardware = null; configured = false; }
                    }
                    else if (configured) errors.AddRange(hardware.Control(values));
                    if (requests.TryDequeue(out var pending))
                    {
                        if (pending.Token.IsCancellationRequested) pending.Reply.TrySetCanceled(pending.Token);
                        else try
                        {
                            if (competing) throw new IOException("Stop the other cooling service before applying Lutra controls.");
                            var proposed = pending.Request.Configuration ?? throw new InvalidDataException("Configuration is missing.");
                            Curves.Validate(proposed);
                            if (proposed.MotherboardEnabled != configuration.MotherboardEnabled)
                            {
                                hardware!.Dispose(); hardware = new(true, proposed.MotherboardEnabled);
                                await Task.Delay(600, token); hardware.Update(); configured = false;
                            }
                            Curves.Validate(proposed, hardware!.Catalog);
                            // Preflight persistence before touching devices; the directory ACL is set by the installer.
                            string staged = Path.Combine(directory, "configuration.pending.json");
                            JsonFiles.Save(staged, proposed);
                            try
                            {
                                await hardware.Configure(proposed, Path.Combine(directory, "backups"), pending.Token);
                                File.Move(staged, configPath, true);
                                configuration = proposed; configured = true;
                                pending.Reply.TrySetResult(new(true, "Configuration applied and verified."));
                            }
                            catch
                            {
                                configured = false; retryAfter = default;
                                throw;
                            }
                            finally { if (File.Exists(staged)) File.Delete(staged); }
                        }
                        catch (Exception e) { pending.Reply.TrySetResult(new(false, e.GetBaseException().Message)); }
                    }
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    errors.Add(e.GetBaseException().Message);
                    hardware?.Dispose(); hardware = null; configured = false;
                }
                if (ipc.Error is { } ipcError) errors.Add(ipcError);
                var now = DateTimeOffset.UtcNow;
                var events = alarms.Evaluate(configuration.Alarms, values, now);
                state = new(now, configured ? "Running" : "Monitoring", hardware?.Catalog ?? state.Catalog, values, events, errors.Distinct().ToArray(), configuration);
                Publish(state);
                try { history.Append(new(now, values), configuration.RetentionDays); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Log("History: " + e.Message); }
                Interlocked.Exchange(ref lastTick, Environment.TickCount64);
                await Task.Delay(750, token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        finally
        {
            hardware?.Dispose(); ipcStop.Cancel();
            try { await server; } catch (OperationCanceledException) { }
            Publish(state with { Time = DateTimeOffset.UtcNow, Mode = "Stopped", Errors = ["Service stopped. Motherboard controls were returned to firmware. QUADRO software curves fall back after their watchdog timeout."] });
            // The mutex is thread-affine; disposal releases the process-owned handle.
        }
    }
    private static bool HasOtherController()
    {
        foreach (string name in new[] { "AquaComputerService", "Vesi.Service" })
        {
            var found = Process.GetProcessesByName(name);
            bool present = found.Length > 0;
            foreach (var p in found) p.Dispose();
            if (present) return true;
        }
        return false;
    }
    private async Task<ServiceReply> HandleRequest(ServiceRequest request, CancellationToken token)
    {
        if (request.Action == "state") return new(true, "", Volatile.Read(ref state));
        if (request.Action != "apply") return new(false, "Unknown command.");
        var completion = new TaskCompletionSource<ServiceReply>(TaskCreationOptions.RunContinuationsAsynchronously);
        requests.Enqueue(new(request, completion, token));
        return await completion.Task.WaitAsync(token);
    }
    private void Publish(RuntimeState next)
    {
        try { JsonFiles.Save(Path.Combine(directory, "state.json"), next); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Log("State: " + e.Message); }
        string summary = next.Mode + " " + string.Join("; ", next.Errors) + " " + string.Join("; ", next.Alarms.Select(a => a.Name));
        if (summary != lastLog) { lastLog = summary; Log(summary); }
    }
    private void Log(string message)
    {
        lock (logGate)
        {
            try
            {
                string path = Path.Combine(directory, "service.log");
                if (File.Exists(path) && new FileInfo(path).Length > 2_000_000) File.Move(path, path + ".1", true);
                File.AppendAllText(path, DateTimeOffset.UtcNow.ToString("O") + " " + message + Environment.NewLine);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }
}

