namespace TanbalType;

internal static class LayoutManager
{
    private static IntPtr? _persianLayout;
    private static IntPtr? _englishLayout;

    public static bool IsPersianLayout()
    {
        var langId = GetForegroundLangId();
        return langId == 0x0429;
    }

    public static void SwitchForCorrection(bool wasPersian, bool correctedIsPersian)
    {
        if (correctedIsPersian == wasPersian)
        {
            AppLog.Write("Layout switch: skipped (correction same language as layout)");
            return;
        }

        if (correctedIsPersian)
            ApplyForegroundLayout(GetPersianLayout(), wantPersian: true);
        else
            ApplyForegroundLayout(GetEnglishLayout(), wantPersian: false);
    }

    private static void ApplyForegroundLayout(IntPtr targetHkl, bool wantPersian)
    {
        var label = wantPersian ? "Persian" : "English";
        if (targetHkl == IntPtr.Zero)
        {
            AppLog.Write($"Layout switch FAILED: could not load {label} keyboard");
            return;
        }

        if (IsPersianLayout() == wantPersian)
        {
            AppLog.Write($"Layout switch: already {label}");
            return;
        }

        var hwnd = NativeMethods.GetForegroundWindow();

        if (hwnd != IntPtr.Zero)
        {
            NativeMethods.SendMessage(
                hwnd,
                NativeMethods.WmInputLangChangeRequest,
                IntPtr.Zero,
                targetHkl);
            Thread.Sleep(40);

            if (IsPersianLayout() == wantPersian)
            {
                AppLog.Write($"Layout switch OK -> {label}");
                return;
            }

            NativeMethods.PostMessage(
                hwnd,
                NativeMethods.WmInputLangChangeRequest,
                IntPtr.Zero,
                targetHkl);
            Thread.Sleep(40);

            if (IsPersianLayout() == wantPersian)
            {
                AppLog.Write($"Layout switch OK -> {label} (PostMessage)");
                return;
            }
        }

        if (TryActivateOnForegroundThread(targetHkl))
        {
            Thread.Sleep(40);
            if (IsPersianLayout() == wantPersian)
            {
                AppLog.Write($"Layout switch OK -> {label} (ActivateKeyboardLayout)");
                return;
            }
        }

        if (TryLanguageHotkeyToggle())
        {
            Thread.Sleep(50);
            if (IsPersianLayout() == wantPersian)
            {
                AppLog.Write($"Layout switch OK -> {label} (Alt+Shift)");
                return;
            }
        }

        AppLog.Write($"Layout switch FAILED -> wanted {label} (enable EN+FA in Windows language settings)");
    }

    private static bool TryActivateOnForegroundThread(IntPtr hkl)
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            return false;

        var fgThread = NativeMethods.GetWindowThreadProcessId(hwnd, out _);
        var ourThread = NativeMethods.GetCurrentThreadId();
        var attached = false;

        try
        {
            if (fgThread != 0 && fgThread != ourThread)
                attached = NativeMethods.AttachThreadInput(ourThread, fgThread, true);

            NativeMethods.ActivateKeyboardLayout(hkl, NativeMethods.KlActivate);
            return true;
        }
        finally
        {
            if (attached)
                NativeMethods.AttachThreadInput(ourThread, fgThread, false);
        }
    }

    /// <summary>Alt+Shift — works when only English and Persian are installed.</summary>
    private static bool TryLanguageHotkeyToggle()
    {
        const byte vkMenu = 0x12;
        const byte vkShift = 0x10;

        NativeMethods.keybd_event(vkMenu, 0, 0, UIntPtr.Zero);
        NativeMethods.keybd_event(vkShift, 0, 0, UIntPtr.Zero);
        NativeMethods.keybd_event(vkShift, 0, NativeMethods.KeyeventfKeyup, UIntPtr.Zero);
        NativeMethods.keybd_event(vkMenu, 0, NativeMethods.KeyeventfKeyup, UIntPtr.Zero);
        return true;
    }

    private static ushort GetForegroundLangId()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        var threadId = hwnd != IntPtr.Zero
            ? NativeMethods.GetWindowThreadProcessId(hwnd, out _)
            : 0u;
        var hkl = NativeMethods.GetKeyboardLayout(threadId);
        return (ushort)(hkl.ToInt64() & 0xFFFF);
    }

    private static IntPtr GetPersianLayout() =>
        _persianLayout ??= NativeMethods.LoadKeyboardLayout(NativeMethods.PersianKlid, NativeMethods.KlfActivate);

    private static IntPtr GetEnglishLayout() =>
        _englishLayout ??= NativeMethods.LoadKeyboardLayout(NativeMethods.EnglishKlid, NativeMethods.KlfActivate);
}
