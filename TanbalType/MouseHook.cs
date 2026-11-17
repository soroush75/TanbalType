using System.Runtime.InteropServices;

namespace TanbalType;

/// <summary>
/// کلیکِ ماوس را می‌پاید تا برنامه بفهمد مکان‌نما جابه‌جا شده است.
///
/// بافرِ کلیدها تنها تا زمانی با متنِ واقعیِ پیشِ مکان‌نما می‌خواند که کاربر
/// پشتِ‌سرهم تایپ کند. یک کلیک می‌تواند مکان‌نما را جای دیگری — مثلاً وسط یا
/// انتهای کلمه‌ای که از قبل نوشته شده — ببرد؛ آن‌وقت اصلاح بر پایهٔ بافرِ کهنه
/// هم کلمهٔ درست را خراب می‌کند («engineer» + «ing» → «engineerهدل») و هم ممکن
/// است چند حرفِ متنِ قبلی را پاک کند. با دیدنِ کلیک، بافر از صفر شروع می‌شود.
///
/// تنها پیامِ فشردنِ دکمه‌ها بررسی می‌شود و حرکتِ ماوس بی‌درنگ رد می‌شود، چون
/// callbackِ یک low-level hook باید سریع برگردد وگرنه ویندوز آن را برمی‌دارد.
/// </summary>
internal sealed class MouseHook : IDisposable
{
    private readonly CorrectionService _service;
    private NativeMethods.LowLevelMouseProc? _proc;
    private IntPtr _hookId = IntPtr.Zero;
    private bool _disposed;

    public MouseHook(CorrectionService service)
    {
        _service = service;
    }

    /// <summary>
    /// نصبِ hook. نشستنِ آن شرطِ کارکردنِ برنامه نیست: اگر نصب نشود فقط
    /// جابه‌جاییِ مکان‌نما با ماوس دیده نمی‌شود، پس خطا log می‌شود و بالا نمی‌رود.
    /// </summary>
    public void Install()
    {
        if (_hookId != IntPtr.Zero) return;

        _proc = HookCallback;

        // برای hookهای low-level، ویندوز hMod را نادیده می‌گیرد؛ handleِ خودِ exe کافی است.
        _hookId = NativeMethods.SetWindowsHookEx(
            NativeMethods.WhMouseLl,
            _proc,
            NativeMethods.GetModuleHandle(null),
            0);

        if (_hookId == IntPtr.Zero)
        {
            var err = Marshal.GetLastWin32Error();
            _proc = null;
            AppLog.Write($"Mouse hook install failed (error {err}); caret moves by mouse stay unnoticed.");
            return;
        }

        AppLog.Write("Mouse hook installed.");
    }

    public void Uninstall()
    {
        if (_hookId == IntPtr.Zero) return;
        NativeMethods.UnhookWindowsHookEx(_hookId);
        _hookId = IntPtr.Zero;
        _proc = null;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && IsButtonDown(wParam.ToInt32()))
        {
            try
            {
                _service.InvalidateBuffer();
            }
            catch (Exception ex)
            {
                // استثنا نباید از داخل hook بیرون بزند وگرنه ویندوز hook را برمی‌دارد.
                AppLog.Write($"Mouse hook handler failed: {ex}");
            }
        }

        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    /// <summary>چرخِ ماوس مکان‌نما را جابه‌جا نمی‌کند، پس فقط فشردنِ دکمه‌ها مهم است.</summary>
    private static bool IsButtonDown(int message) =>
        message is NativeMethods.WmLButtonDown
            or NativeMethods.WmRButtonDown
            or NativeMethods.WmMButtonDown
            or NativeMethods.WmXButtonDown;

    public void Dispose()
    {
        if (_disposed) return;
        Uninstall();
        _disposed = true;
    }
}
