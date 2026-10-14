using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Lutra.Service;

internal static class WindowsServiceHost
{
    public const string Name = "LutraControl";
    private static readonly ServiceMainCallback MainCallback = ServiceEntryPoint;
    private static readonly HandlerCallback ControlCallback = Control;
    private static readonly CancellationTokenSource Stop = new();
    private static Func<CancellationToken, Task> worker = null!;
    private static IntPtr statusHandle;
    private static Exception? failure;
    public static void Run(Func<CancellationToken, Task> action)
    {
        worker = action;
        var table = new[] { new ServiceEntry { Name = Name, Main = MainCallback }, new ServiceEntry() };
        if (!StartServiceCtrlDispatcher(table)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Start Lutra from Windows Service Control Manager.");
        if (failure != null) throw new IOException("Lutra service stopped unexpectedly.", failure);
    }
    private static void ServiceEntryPoint(uint argc, IntPtr argv)
    {
        statusHandle = RegisterServiceCtrlHandlerEx(Name, ControlCallback, IntPtr.Zero);
        if (statusHandle == IntPtr.Zero) return;
        SetStatus(2, 0, 10000);
        try { SetStatus(4, 5, 0); worker(Stop.Token).GetAwaiter().GetResult(); }
        catch (Exception e) { failure = e; }
        finally { SetStatus(1, 0, 0, failure == null ? 0u : 1066u); }
    }
    private static uint Control(uint control, uint eventType, IntPtr data, IntPtr context)
    {
        if (control is 1 or 5) { SetStatus(3, 0, 15000); Stop.Cancel(); }
        else if (control == 4) SetStatus(Stop.IsCancellationRequested ? 3u : 4u, Stop.IsCancellationRequested ? 0u : 5u, 0);
        return 0;
    }
    private static void SetStatus(uint state, uint accepted, uint wait, uint error = 0)
    {
        var status = new ServiceStatus { Type = 0x10, State = state, Accepted = accepted, WaitHint = wait, Win32ExitCode = error, ServiceExitCode = error == 0 ? 0u : 1u };
        SetServiceStatus(statusHandle, ref status);
    }
    private delegate void ServiceMainCallback(uint argc, IntPtr argv);
    private delegate uint HandlerCallback(uint control, uint eventType, IntPtr data, IntPtr context);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct ServiceEntry { public string? Name; public ServiceMainCallback? Main; }
    [StructLayout(LayoutKind.Sequential)] private struct ServiceStatus { public uint Type, State, Accepted, Win32ExitCode, ServiceExitCode, Checkpoint, WaitHint; }
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool StartServiceCtrlDispatcher([In] ServiceEntry[] table);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr RegisterServiceCtrlHandlerEx(string name, HandlerCallback handler, IntPtr context);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool SetServiceStatus(IntPtr handle, ref ServiceStatus status);
}
