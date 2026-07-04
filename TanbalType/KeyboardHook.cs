using System.Runtime.InteropServices;

namespace TanbalType;

internal sealed class KeyboardHook : IDisposable
{
    private readonly CorrectionService _service;
    private NativeMethods.LowLevelKeyboardProc? _proc;
    private IntPtr _hookId = IntPtr.Zero;
    private bool _disposed;

    public KeyboardHook(CorrectionService service)
    {
        _service = service;
    }

    public void Install()
    {
        if (_hookId != IntPtr.Zero) return;

        _proc = HookCallback;
        var moduleHandle = ResolveModuleHandle();

        _hookId = NativeMethods.SetWindowsHookEx(
            NativeMethods.WhKeyboardLl,
            _proc,
            moduleHandle,
            0);

        if (_hookId == IntPtr.Zero)
        {
            var err = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"SetWindowsHookEx failed (error {err}).");
        }

        AppLog.Write($"Hook installed. module={moduleHandle}, inputSize={NativeMethods.InputSize}, marshalSize={System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.Input>()}");
    }

    private static IntPtr ResolveModuleHandle()
    {
        var exePath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(exePath))
        {
            var handle = NativeMethods.GetModuleHandle(exePath);
            if (handle != IntPtr.Zero)
                return handle;
        }

        var main = System.Diagnostics.Process.GetCurrentProcess().MainModule;
        if (main is not null)
        {
            var handle = NativeMethods.GetModuleHandle(main.ModuleName);
            if (handle != IntPtr.Zero)
                return handle;
        }

        return NativeMethods.GetModuleHandle(null);
    }

    public void Uninstall()
    {
        if (_hookId == IntPtr.Zero) return;
        NativeMethods.UnhookWindowsHookEx(_hookId);
        _hookId = IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var data = Marshal.PtrToStructure<NativeMethods.KbdLlHookStruct>(lParam);
            if (_service.ShouldIgnoreEvent(data.dwExtraInfo))
                return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);

            if (_service.Enabled && !_service.IsCorrecting)
            {
                var message = wParam.ToInt32();
                if (message is NativeMethods.WmKeydown or NativeMethods.WmSysKeydown)
                {
                    if (_service.HandleKey(data.vkCode, data.scanCode, out var consume) && consume)
                        return (IntPtr)1;
                }
            }
        }

        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed) return;
        Uninstall();
        _disposed = true;
    }
}
