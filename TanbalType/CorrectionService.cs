namespace TanbalType;

internal sealed class CorrectionService
{
    private readonly object _lock = new();
    private readonly Action<Action> _dispatch;
    private string _buffer = string.Empty;
    private bool? _bufferLayoutIsPersian;
    private char? _lastDelimiter = null; // حافظه برای ذخیره آخرین کلید جداکننده

    public CorrectionService(Action<Action> dispatch)
    {
        _dispatch = dispatch;
    }

    public bool Enabled { get; set; } = true;
    public bool IsCorrecting { get; private set; }

    public bool ShouldIgnoreEvent(IntPtr extraInfo) =>
        extraInfo.ToInt64() == NativeMethods.SyntheticMarker.ToInt64();

    public bool HandleKey(uint vkCode, uint scanCode, out bool consume)
    {
        consume = false;
        if (!Enabled || IsCorrecting || IsModifierDown())
            return false;

        lock (_lock)
        {
            var persian = LayoutManager.IsPersianLayout();

            if (vkCode == NativeMethods.VkBack)
            {
                // پاک‌کردن یعنی پاک‌کردن: اگر کاربر بعد از یک اصلاح در حال حذف است،
                // برنامه نباید متن را بازگرداند یا دوباره تصمیم به اصلاح بگیرد.

                if (_buffer.Length > 0)
                {
                    _buffer = _buffer[..^1];
                    if (_buffer.Length == 0)
                        _bufferLayoutIsPersian = null;
                }
                else
                {
                    // اگر کلمه پاک شد و به فضای خالی رسیدیم، حافظه کاراکتر قبلی را ریست می‌کنیم
                    _lastDelimiter = null;
                }
                return false;
            }

            if (IsDelimiterKey(vkCode))
            {
                var delimiterChar = KeyReader.ReadDelimiter(vkCode, scanCode) ?? ' ';
                var layoutIsPersian = _bufferLayoutIsPersian ?? persian;
                var word = _buffer;
                _buffer = string.Empty;
                _bufferLayoutIsPersian = null;

                var corrected = MaybeCorrect(word, layoutIsPersian);
                AppLog.Write($"Delimiter vk={vkCode} layout={(layoutIsPersian ? "fa" : "en")} word='{word}' -> '{corrected ?? word}'");

                // بررسی می‌کنیم که آیا کلمه قبلی واقعاً با Space جدا شده است یا مثلاً با Enter (اول خط)
                bool replacePrevSpace = (_lastDelimiter == ' ');

                if (corrected is not null)
                {
                    consume = true;
                    ScheduleCorrection(word, corrected, delimiterChar.ToString(), layoutIsPersian, delimiterConsumed: true, replaceSpace: replacePrevSpace);
                    _lastDelimiter = delimiterChar;
                    return true;
                }

                _lastDelimiter = delimiterChar;
                return false;
            }

            var ch = KeyReader.ReadBufferChar(vkCode, scanCode, persian);
            if (ch is null || char.IsControl(ch.Value))
                return false;


            if (_buffer.Length == 0)
                _bufferLayoutIsPersian = persian;

            _buffer += ch.Value;
            return false;
        }
    }

    private void ScheduleCorrection(string original, string corrected, string delimiter, bool layoutWasPersian, bool delimiterConsumed, bool replaceSpace)
    {
        _dispatch(() =>
        {
            Thread.Sleep(15);
            lock (_lock)
            {
                ApplyCorrection(original, corrected, delimiter, layoutWasPersian, delimiterConsumed, replaceSpace);
            }
        });
    }

    private string? MaybeCorrect(string word, bool layoutIsPersian)
    {
        if (string.IsNullOrWhiteSpace(word) || word.Length < 2)
            return null;

        var corrected = Detector.DetectWrongLayout(word, layoutIsPersian);
        return corrected is null || corrected == word ? null : corrected;
    }

    private void ApplyCorrection(string original, string corrected, string delimiter, bool wasPersian, bool delimiterConsumed, bool replaceSpace)
    {
        var correctedIsPersian = Mapper.CountPersian(corrected) > corrected.Length / 2;
        var deleteCount = original.Length + (delimiterConsumed ? 0 : (string.IsNullOrEmpty(delimiter) ? 0 : 1));
        var prefix = string.Empty;

        // فقط زمانی Space قبلی را حذف و با Space فارسی جایگزین می‌کنیم که مطمئن باشیم اول خط نیستیم
        if (correctedIsPersian && !wasPersian && replaceSpace)
        {
            prefix = " ";
            deleteCount += 1;
        }

        AppLog.Write($"Apply delete={deleteCount} '{original}' -> '{prefix}{corrected}' (consumed: {delimiterConsumed})");

        IsCorrecting = true;
        try
        {
            if (deleteCount > 0)
                InputSimulator.SendBackspaces(deleteCount);

            LayoutManager.SwitchForCorrection(wasPersian, correctedIsPersian);
            Thread.Sleep(30);

            InputSimulator.SendText(prefix + corrected + delimiter);
        }
        finally
        {
            IsCorrecting = false;
        }

    }

    private static bool IsModifierDown() =>
        IsPressed(NativeMethods.VkControl)
        || IsPressed(NativeMethods.VkMenu)
        || IsPressed(NativeMethods.VkLwin)
        || IsPressed(NativeMethods.VkRwin);

    private static bool IsPressed(uint vk) =>
        (NativeMethods.GetKeyState((int)vk) & 0x8000) != 0;

    private static bool IsDelimiterKey(uint vkCode) =>
        vkCode is 0x20 or NativeMethods.VkReturn or NativeMethods.VkTab;
}
