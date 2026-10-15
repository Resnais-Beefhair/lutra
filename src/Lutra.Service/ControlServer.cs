using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using Lutra.Core;

namespace Lutra.Service;

// Transport failures must not stop cooling. Only Controller processes queued device changes.
internal sealed class ControlServer(string directory, Func<ServiceRequest, CancellationToken, Task<ServiceReply>> handle,
    Action<string> log, string pipeName = ControlMessages.PipeName)
{
    private const int Connections = 4;
    private readonly string?[] errors = new string?[Connections];
    private string? startupError = "IPC: starting.";
    public string? Error => Volatile.Read(ref startupError) ?? Enumerable.Range(0, Connections)
        .Select(i => Volatile.Read(ref errors[i])).FirstOrDefault(e => e != null);

    public async Task RunAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            PipeSecurity security;
            try
            {
                var owner = new SecurityIdentifier(File.ReadAllText(Path.Combine(directory, "owner.sid")).Trim());
                security = new PipeSecurity();
                security.AddAccessRule(new(new SecurityIdentifier(WellKnownSidType.NetworkSid, null), PipeAccessRights.FullControl, AccessControlType.Deny));
                foreach (var sid in new[] { owner, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                    new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null) })
                    security.AddAccessRule(new(sid, PipeAccessRights.FullControl, AccessControlType.Allow));
            }
            catch (Exception e)
            {
                string message = "IPC unavailable: cannot read a valid owner.sid. " + e.GetBaseException().Message;
                if (Volatile.Read(ref startupError) != message) log(message);
                Volatile.Write(ref startupError, message);
                try { await Task.Delay(1000, token); } catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
                continue;
            }
            Volatile.Write(ref startupError, null);
            await Task.WhenAll(Enumerable.Range(0, Connections).Select(i => Serve(i, security, token)));
        }
    }

    private async Task Serve(int index, PipeSecurity security, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                using var pipe = NamedPipeServerStreamAcl.Create(pipeName, PipeDirection.InOut, Connections,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 4096, 4096, security);
                Volatile.Write(ref errors[index], null);
                await pipe.WaitForConnectionAsync(token);
                try
                {
                    // A stalled header/body must not occupy a connection for the apply deadline.
                    ServiceRequest request;
                    using (var read = CancellationTokenSource.CreateLinkedTokenSource(token))
                    {
                        read.CancelAfter(TimeSpan.FromSeconds(3));
                        request = await ControlMessages.ReadAsync<ServiceRequest>(pipe, read.Token);
                    }
                    using var operation = CancellationTokenSource.CreateLinkedTokenSource(token);
                    operation.CancelAfter(TimeSpan.FromSeconds(25));
                    var reply = await handle(request, operation.Token);
                    await ControlMessages.WriteAsync(pipe, reply, operation.Token);
                }
                catch (Exception e) when (e is IOException or OperationCanceledException or System.Text.Json.JsonException or InvalidDataException)
                {
                    // Invalid/disconnected clients are isolated; the protocol still enforces 1 MiB.
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
            catch (Exception e)
            {
                string message = "IPC connection unavailable: " + e.GetBaseException().Message;
                if (Volatile.Read(ref errors[index]) != message) log(message);
                Volatile.Write(ref errors[index], message);
                try { await Task.Delay(1000, token); } catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
            }
        }
    }
}
