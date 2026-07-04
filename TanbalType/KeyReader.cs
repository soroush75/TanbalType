using System.Text;

namespace TanbalType;

internal static class KeyReader
{
    /// <summary>
    /// English layout: physical US key (s → "s" for Persian mapping).
    /// Persian layout: mapped on-screen letter via EnKeysToPersian (hello → "اثممخ").
    /// ToUnicodeEx is unreliable inside WH_KEYBOARD_LL — physical mapping is used instead.
    /// </summary>
    public static char? ReadBufferChar(uint vkCode, uint scanCode, bool persianLayout)
    {
        _ = scanCode;
        if (IsModifierOrSystem(vkCode))
            return null;

        if (!persianLayout)
            return ReadPhysicalKey(vkCode);

        return ReadPersianLayoutChar(vkCode);
    }

    private static char? ReadPersianLayoutChar(uint vkCode)
    {
        var physical = ReadPhysicalKey(vkCode);
        if (physical is null)
            return null;

        var key = physical.Value;
        if (key is >= 'A' and <= 'Z')
            key = (char)(key + 32);

        var mapped = Mapper.EnKeysToPersian(key.ToString());
        return mapped.Length == 1 ? mapped[0] : physical;
    }

    public static char? ReadDelimiter(uint vkCode, uint scanCode)
    {
        if (vkCode == 0x20) return ' ';
        if (vkCode == NativeMethods.VkReturn) return '\n';
        if (vkCode == NativeMethods.VkTab) return '\t';

        if (TryReadUnicode(vkCode, scanCode, out var ch))
            return ch;

        return ReadPhysicalKey(vkCode);
    }

    private static bool TryReadUnicode(uint vkCode, uint scanCode, out char ch)
    {
        ch = default;
        var state = new byte[256];

        for (uint vk = 0; vk < 256; vk++)
        {
            if (IsPressed(vk))
                state[vk] = 0x80;
        }

        if (vkCode < state.Length)
            state[vkCode] |= 0x80;

        var layout = GetForegroundLayout();
        var sb = new StringBuilder(8);
        var result = NativeMethods.ToUnicodeEx(vkCode, scanCode, state, sb, sb.Capacity, 0, layout);
        if (result == 1)
        {
            ch = sb[0];
            return true;
        }

        return false;
    }

    private static IntPtr GetForegroundLayout()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            return NativeMethods.GetKeyboardLayout(0);

        var threadId = NativeMethods.GetWindowThreadProcessId(hwnd, out _);
        return NativeMethods.GetKeyboardLayout(threadId);
    }

    private static char? ReadPhysicalKey(uint vkCode)
    {
        var shift = IsPressed(NativeMethods.VkShift);
        var caps = (NativeMethods.GetKeyState((int)NativeMethods.VkCapital) & 1) != 0;
        var upper = shift ^ caps;

        if (vkCode is >= 0x41 and <= 0x5A)
            return (char)(upper ? vkCode : vkCode + 32);

        if (vkCode is >= 0x30 and <= 0x39)
            return (char)vkCode;

        if (shift)
        {
            return vkCode switch
            {
                0xBA => ':',
                0xBB => '+',
                0xBC => '<',
                0xBD => '_',
                0xBE => '>',
                0xBF => '?',
                0xDE => '"',
                0xC0 => '~',
                _ => null,
            };
        }

        return vkCode switch
        {
            0x20 => ' ',
            0xBA => ';',
            0xBB => '=',
            0xBC => ',',
            0xBD => '-',
            0xBE => '.',
            0xBF => '/',
            0xDE => '\'',
            0xC0 => '`',
            0xDB => '[',
            0xDC => '\\',
            0xDD => ']',
            _ => null,
        };
    }

    private static bool IsModifierOrSystem(uint vkCode) =>
        vkCode is NativeMethods.VkShift
            or NativeMethods.VkControl
            or NativeMethods.VkMenu
            or NativeMethods.VkLwin
            or NativeMethods.VkRwin
            or 0x12; // Alt

    private static bool IsPressed(uint vk) =>
        (NativeMethods.GetKeyState((int)vk) & 0x8000) != 0;
}
