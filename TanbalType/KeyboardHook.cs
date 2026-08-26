using System.Runtime.InteropServices;

namespace TanbalType;

internal sealed class KeyboardHook : IDisposable
{
    private readonly CorrectionService _service;
    private NativeMethods.LowLevelKeyboardProc? _proc;
    private IntPtr _hookId = IntPtr.Zero;
    private bool _disposed;
    private bool _toggleKeyHeld;

    /// <summary>کلید میان‌بر سراسری برای فعال/غیرفعال کردن برنامه.</summary>
    public const uint ToggleHotkey = NativeMethods.VkF10;

    /// <summary>با فشردن کلید میان‌بر صدا زده می‌شود؛ روی همان نخی که hook را نصب کرده (نخ UI).</summary>
    public event Action? ToggleRequested;

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

            var message = wParam.ToInt32();

            // میان‌بر پیش از بررسی Enabled سنجیده می‌شود تا در حالت غیرفعال هم بتواند برنامه را برگرداند.
            if (data.vkCode == ToggleHotkey && HandleToggleHotkey(message))
                return (IntPtr)1;

            if (_service.Enabled && !_service.IsCorrecting)
            {
                if (message is NativeMethods.WmKeydown or NativeMethods.WmSysKeydown)
                {
                    if (_service.HandleKey(data.vkCode, data.scanCode, out var consume) && consume)
                        return (IntPtr)1;
                }
            }
        }

        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    /// <summary>
    /// F10 تنها (بدون Ctrl/Alt/Shift/Win) برنامه را فعال/غیرفعال می‌کند و خودِ کلید مصرف می‌شود
    /// تا به برنامهٔ فعال نرسد. ترکیب‌هایی مثل Shift+F10 دست‌نخورده عبور می‌کنند.
    /// </summary>
    /// <returns>true اگر این رویداد باید مصرف شود.</returns>
    private bool HandleToggleHotkey(int message)
    {
        // F10 به‌تنهایی پیام SYSKEYDOWN می‌سازد (کلید منو در ویندوز)، پس هر دو پیام بررسی می‌شوند.
        if (message is NativeMethods.WmKeydown or NativeMethods.WmSysKeydown)
        {
            if (IsModifierDown())
                return false;

            if (_toggleKeyHeld)
                return true; // نگه‌داشتن کلید (auto-repeat) نباید پشت‌سرهم سوییچ کند

            _toggleKeyHeld = true;

            try
            {
                ToggleRequested?.Invoke();
            }
            catch (Exception ex)
            {
                // استثنا نباید از داخل hook بیرون بزند وگرنه ویندوز hook را برمی‌دارد.
                AppLog.Write($"Toggle hotkey handler failed: {ex}");
            }

            return true;
        }

        if (message is NativeMethods.WmKeyup or NativeMethods.WmSysKeyup)
        {
            if (!_toggleKeyHeld)
                return false; // keydown مصرف نشده بود (مثلاً Shift+F10)، پس keyup هم باید عبور کند

            _toggleKeyHeld = false;
            return true;
        }

        return false;
    }

    // داخل low-level hook باید وضعیت کلیدها به‌صورت async خوانده شود؛
    // GetKeyState وضعیت صف پیام همین نخ را برمی‌گرداند که اینجا به‌روز نیست.
    private static bool IsModifierDown() =>
        IsPressed(NativeMethods.VkControl)
        || IsPressed(NativeMethods.VkMenu)
        || IsPressed(NativeMethods.VkShift)
        || IsPressed(NativeMethods.VkLwin)
        || IsPressed(NativeMethods.VkRwin);

    private static bool IsPressed(uint vk) =>
        (NativeMethods.GetAsyncKeyState((int)vk) & 0x8000) != 0;

    public void Dispose()
    {
        if (_disposed) return;
        Uninstall();
        _disposed = true;
    }
}
