using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Security.Principal;
using Lutra.Core;
using Lutra.Service;

internal static class ControlServerTests
{
    public static void Run(Action<string, Action> test)
    {
        test("Missing and malformed owner SID are reported; repairing the file restores IPC", () => RunAsync(async () =>
        {
            await using var fixture = new Fixture(owner: false);
            await Until(() => fixture.Server.Error?.Contains("owner.sid") == true);
            Assert(fixture.Logs.Any(s => s.Contains("owner.sid")), "Missing owner was not logged");
            File.WriteAllText(fixture.OwnerPath, "invalid SID");
            await Until(() => fixture.Logs.Count >= 2);
            Assert(fixture.Server.Error != null, "Invalid owner accepted");
            File.WriteAllText(fixture.OwnerPath, WindowsIdentity.GetCurrent().User!.Value);
            await Until(() => fixture.Server.Error == null);
            Assert((await fixture.Request("state")).Success, "IPC did not recover");
        }));
        test("A silent client does not block state or apply on another connection", () => RunAsync(async () =>
        {
            await using var fixture = new Fixture();
            using var silent = await fixture.Connect();
            Assert((await fixture.Request("state")).Success, "State blocked by silent client");
            Assert((await fixture.Request("apply")).Success, "Apply blocked by silent client");
        }));
        test("All silent clients are disconnected by the read deadline", () => RunAsync(async () =>
        {
            await using var fixture = new Fixture();
            var clients = new List<NamedPipeClientStream>();
            try
            {
                for (int i = 0; i < 4; i++) clients.Add(await fixture.Connect());
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(6));
                foreach (var client in clients)
                    Assert(await client.ReadAsync(new byte[1], deadline.Token) == 0, "Silent connection stayed open");
                Assert((await fixture.Request("state")).Success, "Connections did not become available");
            }
            finally { foreach (var client in clients) client.Dispose(); }
        }));
        test("A valid apply can outlast the three-second request-read deadline", () => RunAsync(async () =>
        {
            await using var fixture = new Fixture(async (request, token) =>
            {
                if (request.Action == "apply") await Task.Delay(3500, token);
                return new(true, request.Action);
            });
            var apply = fixture.Request("apply");
            Assert((await fixture.Request("state")).Success, "Apply blocked state");
            Assert((await apply).Success, "Read deadline cancelled an accepted apply");
        }));
        test("Unexpected client handler failures are logged and isolated", () => RunAsync(async () =>
        {
            await using var fixture = new Fixture((request, _) => request.Action == "fault"
                ? throw new UnauthorizedAccessException("Synthetic connection failure") : Task.FromResult(new ServiceReply(true, request.Action)));
            try { await fixture.Request("fault"); throw new Exception("Fault unexpectedly returned success"); }
            catch (IOException) { }
            await Until(() => fixture.Logs.Any(s => s.Contains("Synthetic connection failure")));
            Assert((await fixture.Request("state")).Success, "Other connections stopped");
            await Until(() => fixture.Server.Error == null);
        }));
        test("An oversized IPC message is rejected without losing the listener", () => RunAsync(async () =>
        {
            await using var fixture = new Fixture();
            using var client = await fixture.Connect();
            await client.WriteAsync(BitConverter.GetBytes(1024 * 1024 + 1));
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            Assert(await client.ReadAsync(new byte[1], deadline.Token) == 0, "Oversized client was not closed");
            Assert((await fixture.Request("state")).Success, "Listener stopped");
        }));
    }
    private static void RunAsync(Func<Task> action) => action().GetAwaiter().GetResult();
    private static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    private static async Task Until(Func<bool> predicate)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        while (!predicate()) await Task.Delay(20, deadline.Token);
    }
    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "lutra-ipc-test-" + Guid.NewGuid().ToString("N"));
        private readonly string pipe = "Lutra.Test." + Guid.NewGuid().ToString("N");
        private readonly CancellationTokenSource stop = new();
        private readonly Task running;
        public string OwnerPath => Path.Combine(directory, "owner.sid");
        public ConcurrentQueue<string> Logs { get; } = new();
        public ControlServer Server { get; }
        public Fixture(Func<ServiceRequest, CancellationToken, Task<ServiceReply>>? handler = null, bool owner = true)
        {
            Directory.CreateDirectory(directory);
            if (owner) File.WriteAllText(OwnerPath, WindowsIdentity.GetCurrent().User!.Value);
            Server = new(directory, handler ?? ((request, _) => Task.FromResult(new ServiceReply(true, request.Action))), Logs.Enqueue, pipe);
            running = Server.RunAsync(stop.Token);
        }
        public async Task<NamedPipeClientStream> Connect()
        {
            var client = new NamedPipeClientStream(".", pipe, PipeDirection.InOut, PipeOptions.Asynchronous);
            try { await client.ConnectAsync(1500); return client; }
            catch { client.Dispose(); throw; }
        }
        public async Task<ServiceReply> Request(string action)
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            using var client = await Connect();
            await ControlMessages.WriteAsync(client, new ServiceRequest(action), deadline.Token);
            return await ControlMessages.ReadAsync<ServiceReply>(client, deadline.Token);
        }
        public async ValueTask DisposeAsync()
        {
            stop.Cancel(); await running.WaitAsync(TimeSpan.FromSeconds(5)); stop.Dispose();
            Directory.Delete(directory, true);
        }
    }
}
