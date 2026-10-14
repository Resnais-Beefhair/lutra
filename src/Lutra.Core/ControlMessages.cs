using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text.Json;

namespace Lutra.Core;

public static class ControlMessages
{
    public const string PipeName = "Lutra.Control.v1";
    public static string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Lutra");
    public static async Task WriteAsync<T>(Stream stream, T value, CancellationToken token)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(value, JsonFiles.Options);
        if (payload.Length > 1024 * 1024) throw new InvalidDataException("Control message exceeds 1 MB.");
        byte[] length = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(length, payload.Length);
        await stream.WriteAsync(length, token).ConfigureAwait(false);
        await stream.WriteAsync(payload, token).ConfigureAwait(false); await stream.FlushAsync(token).ConfigureAwait(false);
    }
    public static async Task<T> ReadAsync<T>(Stream stream, CancellationToken token)
    {
        byte[] header = new byte[4]; await stream.ReadExactlyAsync(header, token).ConfigureAwait(false);
        int length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length is <= 0 or > 1024 * 1024) throw new InvalidDataException("Invalid control message size.");
        var data = new byte[length]; await stream.ReadExactlyAsync(data, token).ConfigureAwait(false);
        return JsonSerializer.Deserialize<T>(data, JsonFiles.Options) ?? throw new InvalidDataException("Empty control message.");
    }
    public static async Task<ServiceReply> RequestAsync(ServiceRequest request, CancellationToken token = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromSeconds(30));
        using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous, System.Security.Principal.TokenImpersonationLevel.Identification);
        await pipe.ConnectAsync(1500, deadline.Token).ConfigureAwait(false);
        await WriteAsync(pipe, request, deadline.Token).ConfigureAwait(false);
        return await ReadAsync<ServiceReply>(pipe, deadline.Token).ConfigureAwait(false);
    }
    public static RuntimeState? ReadState()
    {
        try { return JsonFiles.Read<RuntimeState>(Path.Combine(DataDirectory, "state.json")); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }
}
