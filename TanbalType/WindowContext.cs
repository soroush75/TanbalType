using System.Runtime.InteropServices;
using System.Text;

namespace TanbalType;

/// <summary>
/// اطلاعات پنجرهٔ فعال برای تصمیم‌گیری امن‌تر دربارهٔ اصلاح.
/// </summary>
internal static class WindowContext
{
    /// <summary>
    /// آیا فیلدِ فعال یک کادر رمز عبور است؟ (best-effort)
    ///
    /// این تشخیص برای کنترل‌های بومی Win32 با استایل ES_PASSWORD کار می‌کند
    /// (بیشترِ پنجره‌های ورود دسکتاپ). مرورگرها و برنامه‌های مبتنی بر Electron
    /// از Edit بومی استفاده نمی‌کنند، پس کادر رمز آن‌ها این‌جا تشخیص داده نمی‌شود؛
    /// امنیتِ آن موارد از طریق «رد کردن توکن‌های حاوی عدد» در Detector تأمین می‌شود.
    /// در صورت هر خطا، false برمی‌گرداند تا تایپ عادی مختل نشود.
    /// </summary>
    public static bool IsPasswordFieldFocused()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            return false;

        var threadId = NativeMethods.GetWindowThreadProcessId(hwnd, out _);
        if (threadId == 0)
            return false;

        var gti = new NativeMethods.GuiThreadInfo
        {
            cbSize = Marshal.SizeOf<NativeMethods.GuiThreadInfo>(),
        };

        if (!NativeMethods.GetGUIThreadInfo(threadId, ref gti) || gti.hwndFocus == IntPtr.Zero)
            return false;

        return IsPasswordEdit(gti.hwndFocus);
    }

    /// <summary>آیا این هندل یک کنترل Edit با استایل ES_PASSWORD است؟</summary>
    internal static bool IsPasswordEdit(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return false;

        // بیت ES_PASSWORD (0x20) فقط روی کنترل‌های Edit معنیِ «رمز» دارد؛ روی سایر
        // کلاس‌ها (دکمه، Static و ...) همان بیت معنیِ دیگری دارد. پس اول باید کلاس Edit باشد.
        if (!IsEditControl(hwnd))
            return false;

        var style = NativeMethods.GetWindowStyle(hwnd);
        return (style & NativeMethods.EsPassword) != 0;
    }

    private static bool IsEditControl(IntPtr hwnd)
    {
        var sb = new StringBuilder(64);
        var len = NativeMethods.GetClassName(hwnd, sb, sb.Capacity);
        if (len <= 0)
            return false;

        // "Edit"، "RICHEDIT50W"، "RichEdit20W" و ...
        return sb.ToString().Contains("edit", StringComparison.OrdinalIgnoreCase);
    }
}
