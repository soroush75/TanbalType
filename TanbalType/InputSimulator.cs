using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TanbalType;

internal static class InputSimulator
{
    public static void SendBackspaces(int count)
    {
        if (count <= 0) return;
        RunWithForegroundThread(() =>
        {
            if (!TrySendBackspacesViaSendInput(count))
                SendBackspacesViaKeybdEvent(count);
        });
    }

    public static void SendText(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        RunWithForegroundThread(() =>
        {
            if (TrySendTextViaSendInput(text))
                return;

            if (TrySendTextViaClipboard(text))
                return;

            SendTextCharByChar(text);
        });
    }

    private static void RunWithForegroundThread(Action action)
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        var fgThread = hwnd != IntPtr.Zero
            ? NativeMethods.GetWindowThreadProcessId(hwnd, out _)
            : 0u;
        var ourThread = NativeMethods.GetCurrentThreadId();
        var attached = false;

        try
        {
            if (fgThread != 0 && fgThread != ourThread)
                attached = NativeMethods.AttachThreadInput(ourThread, fgThread, true);

            action();
            Thread.Sleep(5);
        }
        finally
        {
            if (attached)
                NativeMethods.AttachThreadInput(ourThread, fgThread, false);
        }
    }

    private static bool TrySendBackspacesViaSendInput(int count)
    {
        var inputs = new NativeMethods.Input[count * 2];
        for (var i = 0; i < count; i++)
        {
            inputs[i * 2] = KeyDown(NativeMethods.VkBack);
            inputs[i * 2 + 1] = KeyUp(NativeMethods.VkBack);
        }

        return Send(inputs);
    }

    private static void SendBackspacesViaKeybdEvent(int count)
    {
        AppLog.Write($"SendInput backspace failed, using keybd_event x{count}");
        for (var i = 0; i < count; i++)
        {
            NativeMethods.keybd_event((byte)NativeMethods.VkBack, 0, 0, UIntPtr.Zero);
            NativeMethods.keybd_event((byte)NativeMethods.VkBack, 0, NativeMethods.KeyeventfKeyup, UIntPtr.Zero);
        }
    }

    private static bool TrySendTextViaSendInput(string text)
    {
        var inputs = new List<NativeMethods.Input>(text.Length * 2);
        foreach (var ch in text)
        {
            inputs.Add(UnicodeDown(ch));
            inputs.Add(UnicodeUp(ch));
        }

        return Send(inputs.ToArray());
    }

    private static bool TrySendTextViaClipboard(string text)
    {
        try
        {
            AppLog.Write($"SendInput text failed, using clipboard paste len={text.Length}");
            var previous = Clipboard.ContainsText() ? Clipboard.GetText() : null;
            Clipboard.SetText(text);
            SendCtrlV();
            Thread.Sleep(20);

            if (previous is not null)
            {
                try { Clipboard.SetText(previous); }
                catch { /* ignore restore failure */ }
            }

            return true;
        }
        catch (Exception ex)
        {
            AppLog.Write($"Clipboard paste failed: {ex.Message}");
            return false;
        }
    }

    private static void SendCtrlV()
    {
        var inputs = new[]
        {
            KeyDown(NativeMethods.VkControl),
            KeyDown(0x56),
            KeyUp(0x56),
            KeyUp(NativeMethods.VkControl),
        };
        if (!Send(inputs))
        {
            NativeMethods.keybd_event((byte)NativeMethods.VkControl, 0, 0, UIntPtr.Zero);
            NativeMethods.keybd_event(0x56, 0, 0, UIntPtr.Zero);
            NativeMethods.keybd_event(0x56, 0, NativeMethods.KeyeventfKeyup, UIntPtr.Zero);
            NativeMethods.keybd_event((byte)NativeMethods.VkControl, 0, NativeMethods.KeyeventfKeyup, UIntPtr.Zero);
        }
    }

    private static void SendTextCharByChar(string text)
    {
        AppLog.Write($"All text methods failed, char-by-char len={text.Length}");
        foreach (var ch in text)
        {
            var pair = new[] { UnicodeDown(ch), UnicodeUp(ch) };
            if (!Send(pair))
            {
                AppLog.Write($"Failed to send char U+{(int)ch:X4}");
                break;
            }

            Thread.Sleep(1);
        }
    }

    private static bool Send(NativeMethods.Input[] inputs)
    {
        var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, NativeMethods.InputSize);
        if (sent == inputs.Length)
            return true;

        var err = Marshal.GetLastWin32Error();
        AppLog.Write($"SendInput sent {sent}/{inputs.Length}, size={NativeMethods.InputSize}, err={err}");
        return false;
    }

    private static NativeMethods.Input KeyDown(uint vk) => new()
    {
        type = NativeMethods.InputKeyboard,
        Ki = new NativeMethods.KeyboardInput
        {
            wVk = (ushort)vk,
            dwExtraInfo = NativeMethods.SyntheticMarker,
        },
    };

    private static NativeMethods.Input KeyUp(uint vk) => new()
    {
        type = NativeMethods.InputKeyboard,
        Ki = new NativeMethods.KeyboardInput
        {
            wVk = (ushort)vk,
            dwFlags = NativeMethods.KeyeventfKeyup,
            dwExtraInfo = NativeMethods.SyntheticMarker,
        },
    };

    private static NativeMethods.Input UnicodeDown(char ch) => new()
    {
        type = NativeMethods.InputKeyboard,
        Ki = new NativeMethods.KeyboardInput
        {
            wScan = (ushort)ch,
            dwFlags = NativeMethods.KeyeventfUnicode,
            dwExtraInfo = NativeMethods.SyntheticMarker,
        },
    };

    private static NativeMethods.Input UnicodeUp(char ch) => new()
    {
        type = NativeMethods.InputKeyboard,
        Ki = new NativeMethods.KeyboardInput
        {
            wScan = (ushort)ch,
            dwFlags = NativeMethods.KeyeventfUnicode | NativeMethods.KeyeventfKeyup,
            dwExtraInfo = NativeMethods.SyntheticMarker,
        },
    };
}
