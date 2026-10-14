using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Lutra.Hardware;

/// <summary>Uses the existing Microsoft WinUSB interface. Never replaces a driver or resets USB.</summary>
public sealed class NativeWinUsb : IDisposable
{
    // DeviceInterfaceGUIDs advertised by this QUADRO's Microsoft OS descriptor.
    private static readonly Guid InterfaceGuid = new("99c4bbb0-e925-4397-afee-981cd0702163");
    private readonly SafeFileHandle file;
    private IntPtr usb;
    public string Serial { get; }
    public static NativeWinUsb Open(string expectedSerial)
    {
        Exception? last = null;
        foreach (string path in Enumerate())
        {
            try { return new NativeWinUsb(path, expectedSerial); }
            catch (Exception e) when (e is IOException or Win32Exception) { last = e; }
        }
        throw new IOException("No free QUADRO control interface matches this serial. Close other control software.", last);
    }
    private NativeWinUsb(string path, string expectedSerial)
    {
        // Exclusive ownership: never race another control process for the same pump.
        file = CreateFile(path, 0xC0000000, 0, IntPtr.Zero, 3, 0x40000000, IntPtr.Zero);
        if (file.IsInvalid) { file.Dispose(); throw Error("QUADRO is busy or unavailable."); }
        try
        {
            if (!WinUsb_Initialize(file, out usb)) throw Error("WinUSB initialization failed.");
            byte[] desc = new byte[18];
            if (!WinUsb_GetDescriptor(usb, 1, 0, 0, desc, 18, out var count) || count != 18
                || BitConverter.ToUInt16(desc, 8) != 0x0C70 || BitConverter.ToUInt16(desc, 10) != 0xF00D)
                throw new IOException("Unexpected USB device.");
            var lang = new byte[256];
            if (!WinUsb_GetDescriptor(usb, 3, 0, 0, lang, 256, out count) || count < 4) throw Error("USB language descriptor is missing.");
            ushort language = BitConverter.ToUInt16(lang, 2); var name = new byte[256];
            if (!WinUsb_GetDescriptor(usb, 3, desc[16], language, name, 256, out count) || count < 4) throw Error("USB serial descriptor is missing.");
            Serial = Encoding.Unicode.GetString(name, 2, Math.Min(name[0], (int)count) - 2).TrimEnd('\0');
            if (Serial != expectedSerial) throw new IOException("QUADRO serial does not match.");
            if (!WinUsb_QueryInterfaceSettings(usb, 0, out var iface) || iface.Number != 0) throw Error("Unexpected QUADRO USB interface.");
            bool found = false;
            for (byte i = 0; i < iface.Endpoints; i++)
                if (WinUsb_QueryPipe(usb, 0, i, out var pipe) && pipe.Id == 2 && pipe.Type == 2) found = true;
            if (!found) throw new IOException("QUADRO bulk OUT endpoint 0x02 is missing.");
            uint timeout = 1500;
            if (!WinUsb_SetPipePolicy(usb, 2, 3, 4, ref timeout)) throw Error("Cannot set USB write timeout.");
        }
        catch { Dispose(); throw; }
    }
    public void Send(byte[] report)
    {
        ObjectDisposedException.ThrowIf(usb == IntPtr.Zero, this);
        if (report.Length != 67 || report[0] != 4 || !QuadroProtocol.ValidCrc(report)) throw new InvalidDataException("Invalid sensor packet.");
        if (!WinUsb_WritePipe(usb, 2, report, (uint)report.Length, out var count, IntPtr.Zero)) throw Error("Sending software temperatures failed.");
        if (count != report.Length) throw new IOException("Incomplete software temperature transfer.");
    }
    public void Dispose()
    {
        if (usb != IntPtr.Zero) { WinUsb_Free(usb); usb = IntPtr.Zero; }
        file.Dispose();
    }
    private static Win32Exception Error(string message) => new(Marshal.GetLastWin32Error(), message);
    public static IReadOnlyList<string> Enumerate()
    {
        var guid = InterfaceGuid; var found = new List<string>();
        var set = SetupDiGetClassDevs(ref guid, null, IntPtr.Zero, 0x12);
        if (set == new IntPtr(-1)) throw Error("Cannot enumerate USB interfaces.");
        try
        {
            for (uint i = 0; ; i++)
            {
                var item = new InterfaceData { Size = Marshal.SizeOf<InterfaceData>() };
                if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, i, ref item))
                {
                    if (Marshal.GetLastWin32Error() == 259) break; throw Error("USB enumeration failed.");
                }
                SetupDiGetDeviceInterfaceDetail(set, ref item, IntPtr.Zero, 0, out var size, IntPtr.Zero);
                var detail = Marshal.AllocHGlobal((int)size);
                try
                {
                    Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                    if (!SetupDiGetDeviceInterfaceDetail(set, ref item, detail, size, out _, IntPtr.Zero)) throw Error("Cannot read USB interface path.");
                    var path = Marshal.PtrToStringUni(detail + 4)!;
                    if (path.Contains("vid_0c70&pid_f00d&mi_00", StringComparison.OrdinalIgnoreCase)) found.Add(path);
                }
                finally { Marshal.FreeHGlobal(detail); }
            }
        }
        finally { SetupDiDestroyDeviceInfoList(set); }
        return found;
    }
    [StructLayout(LayoutKind.Sequential)] private struct InterfaceData { public int Size; public Guid Guid; public uint Flags; public IntPtr Reserved; }
    [StructLayout(LayoutKind.Sequential, Pack = 1)] private struct UsbInterface { public byte Length, Type, Number, Alternate, Endpoints, Class, Subclass, Protocol, Index; }
    [StructLayout(LayoutKind.Sequential)] private struct Pipe { public int Type; public byte Id; public ushort MaximumPacket; public byte Interval; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("winusb.dll", SetLastError = true)] private static extern bool WinUsb_Initialize(SafeFileHandle file, out IntPtr handle);
    [DllImport("winusb.dll", SetLastError = true)] private static extern bool WinUsb_Free(IntPtr handle);
    [DllImport("winusb.dll", SetLastError = true)] private static extern bool WinUsb_GetDescriptor(IntPtr handle, byte type, byte index, ushort language, [Out] byte[] buffer, uint length, out uint transferred);
    [DllImport("winusb.dll", SetLastError = true)] private static extern bool WinUsb_QueryInterfaceSettings(IntPtr handle, byte alternate, out UsbInterface descriptor);
    [DllImport("winusb.dll", SetLastError = true)] private static extern bool WinUsb_QueryPipe(IntPtr handle, byte alternate, byte index, out Pipe pipe);
    [DllImport("winusb.dll", SetLastError = true)] private static extern bool WinUsb_SetPipePolicy(IntPtr handle, byte pipe, uint policy, uint length, ref uint value);
    [DllImport("winusb.dll", SetLastError = true)] private static extern bool WinUsb_WritePipe(IntPtr handle, byte pipe, byte[] buffer, uint length, out uint transferred, IntPtr overlapped);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr SetupDiGetClassDevs(ref Guid guid, string? enumerator, IntPtr parent, uint flags);
    [DllImport("setupapi.dll", SetLastError = true)] private static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr device, ref Guid guid, uint index, ref InterfaceData item);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref InterfaceData item, IntPtr detail, uint length, out uint required, IntPtr device);
    [DllImport("setupapi.dll")] private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
}
