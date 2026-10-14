using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Lutra.Hardware;

public sealed record HidDescription(string Path, string Product, string Serial, int InputLength, int OutputLength, int FeatureLength);

/// <summary>Uses the Windows HID driver already installed for the device. No kernel driver installation.</summary>
public sealed class NativeHid : IDisposable
{
    private readonly SafeFileHandle handle;
    private readonly FileStream stream;
    private readonly bool writable;
    public HidDescription Description { get; }
    public NativeHid(HidDescription description, bool writable = false)
    {
        Description = description; this.writable = writable;
        handle = CreateFile(description.Path, writable ? 0xC0000000u : 0x80000000u, 3, IntPtr.Zero, 3, 0x40000000, IntPtr.Zero);
        if (handle.IsInvalid) { handle.Dispose(); throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot open the QUADRO USB connection."); }
        stream = new FileStream(handle, writable ? FileAccess.ReadWrite : FileAccess.Read, description.InputLength, true);
    }
    public async Task<byte[]> ReadAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[Description.InputLength];
        int count = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        if (count == 0) throw new IOException("QUADRO disconnected.");
        return buffer[..count];
    }
    public byte[] GetFeature(byte id, int length)
    {
        var buffer = new byte[Math.Max(length, Description.FeatureLength)]; buffer[0] = id;
        if (!HidD_GetFeature(handle, buffer, buffer.Length)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot read QUADRO settings.");
        return buffer[..length];
    }
    public async Task<byte[]> ApplySettingsAsync(byte[] before, byte[] candidate, string backupDirectory, CancellationToken token)
    {
        if (!writable) throw new InvalidOperationException("This connection is read-only.");
        if (QuadroProtocol.ReadSettings(before).Structure != 3 || QuadroProtocol.ReadSettings(candidate).Structure != 3)
            throw new InvalidDataException("Unsupported QUADRO settings structure.");
        if (candidate.SequenceEqual(before)) return before;
        Directory.CreateDirectory(backupDirectory);
        var backup = Path.Combine(backupDirectory, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(before)) + ".bin");
        if (!File.Exists(backup)) File.WriteAllBytes(backup, before);
        if (!GetFeature(3, QuadroProtocol.SettingsLength).SequenceEqual(before))
            throw new IOException("Device settings changed during preparation. Nothing was written.");
        token.ThrowIfCancellationRequested();
        async Task Write(byte[] bytes, CancellationToken cancellation)
        {
            if (!HidD_SetFeature(handle, bytes, bytes.Length)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Writing QUADRO settings failed.");
            await Task.Delay(500, cancellation).ConfigureAwait(false);
            byte[] commit = [2, 0, 0, 0, 2, 0, 0, 0, 0, 0x34, 0xC6];
            await stream.WriteAsync(commit, cancellation).ConfigureAwait(false);
            await Task.Delay(500, cancellation).ConfigureAwait(false);
            if (!GetFeature(3, QuadroProtocol.SettingsLength).SequenceEqual(bytes)) throw new IOException("Device settings did not match after writing.");
        }
        try { await Write(candidate, token).ConfigureAwait(false); return candidate; }
        catch (Exception original)
        {
            try { using var restore = new CancellationTokenSource(TimeSpan.FromSeconds(5)); await Write(before, restore.Token).ConfigureAwait(false); }
            catch (Exception rollback) { throw new IOException("Writing and rollback both failed. Backup: " + backup, new AggregateException(original, rollback)); }
            throw new IOException("Settings were not applied. Original settings were restored.", original);
        }
    }
    public void Dispose() { stream.Dispose(); handle.Dispose(); }
    public static IReadOnlyList<HidDescription> EnumerateQuadros()
    {
        var found = new List<HidDescription>();
        HidD_GetHidGuid(out var guid);
        var set = SetupDiGetClassDevs(ref guid, null, IntPtr.Zero, 0x12);
        if (set == new IntPtr(-1)) throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            for (uint i = 0; ; i++)
            {
                var item = new DeviceInterfaceData { Size = Marshal.SizeOf<DeviceInterfaceData>() };
                if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, i, ref item))
                {
                    var error = Marshal.GetLastWin32Error(); if (error == 259) break;
                    throw new Win32Exception(error);
                }
                SetupDiGetDeviceInterfaceDetail(set, ref item, IntPtr.Zero, 0, out var size, IntPtr.Zero);
                var detail = Marshal.AllocHGlobal((int)size);
                try
                {
                    Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                    if (!SetupDiGetDeviceInterfaceDetail(set, ref item, detail, size, out _, IntPtr.Zero)) continue;
                    var path = Marshal.PtrToStringUni(detail + 4)!;
                    if (!path.Contains("vid_0c70&pid_f00d", StringComparison.OrdinalIgnoreCase)) continue;
                    using var h = CreateFile(path, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
                    if (h.IsInvalid || !HidD_GetPreparsedData(h, out var pp)) continue;
                    try
                    {
                        if (HidP_GetCaps(pp, out var caps) < 0) continue;
                        var product = new byte[256]; var serial = new byte[256];
                        HidD_GetProductString(h, product, product.Length); HidD_GetSerialNumberString(h, serial, serial.Length);
                        found.Add(new(path, System.Text.Encoding.Unicode.GetString(product).TrimEnd('\0'),
                            System.Text.Encoding.Unicode.GetString(serial).TrimEnd('\0'), caps.Input, caps.Output, caps.Feature));
                    }
                    finally { HidD_FreePreparsedData(pp); }
                }
                finally { Marshal.FreeHGlobal(detail); }
            }
        }
        finally { SetupDiDestroyDeviceInfoList(set); }
        return found;
    }
    [StructLayout(LayoutKind.Sequential)] private struct DeviceInterfaceData { public int Size; public Guid Guid; public uint Flags; public IntPtr Reserved; }
    [StructLayout(LayoutKind.Sequential)] private struct HidCaps
    {
        public ushort Usage, UsagePage, Input, Output, Feature;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
        public ushort Link, InputButtons, InputValues, InputIndices, OutputButtons, OutputValues, OutputIndices, FeatureButtons, FeatureValues, FeatureIndices;
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("hid.dll")] private static extern void HidD_GetHidGuid(out Guid guid);
    [DllImport("hid.dll")] [return: MarshalAs(UnmanagedType.U1)] private static extern bool HidD_GetPreparsedData(SafeFileHandle handle, out IntPtr data);
    [DllImport("hid.dll")] [return: MarshalAs(UnmanagedType.U1)] private static extern bool HidD_FreePreparsedData(IntPtr data);
    [DllImport("hid.dll")] private static extern int HidP_GetCaps(IntPtr data, out HidCaps caps);
    [DllImport("hid.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.U1)] private static extern bool HidD_GetFeature(SafeFileHandle handle, [Out] byte[] buffer, int length);
    [DllImport("hid.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.U1)] private static extern bool HidD_SetFeature(SafeFileHandle handle, byte[] buffer, int length);
    [DllImport("hid.dll")] [return: MarshalAs(UnmanagedType.U1)] private static extern bool HidD_GetProductString(SafeFileHandle handle, [Out] byte[] buffer, int length);
    [DllImport("hid.dll")] [return: MarshalAs(UnmanagedType.U1)] private static extern bool HidD_GetSerialNumberString(SafeFileHandle handle, [Out] byte[] buffer, int length);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr SetupDiGetClassDevs(ref Guid guid, string? enumerator, IntPtr parent, uint flags);
    [DllImport("setupapi.dll", SetLastError = true)] private static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr device, ref Guid guid, uint index, ref DeviceInterfaceData item);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref DeviceInterfaceData item, IntPtr detail, uint length, out uint required, IntPtr device);
    [DllImport("setupapi.dll")] private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
}
