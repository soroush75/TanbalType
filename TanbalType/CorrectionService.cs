namespace TanbalType;

internal sealed class CorrectionService
{
    private static readonly HashSet<char> WordDelimiters =
        [' ', '\n', '\t', '.', '!', '?', ':', '،', '؛', '؟'];

    private const double RevertWindowSec = 4.0;

    private readonly object _lock = new();
    private readonly Action<Action> _dispatch;
    private string _buffer = string.Empty;
    private bool? _bufferLayoutIsPersian;
    private CorrectionRecord? _lastCorrection;
    private int _undoBackspaces;
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
                if (TryHandleUndoBackspace())
                {
                    consume = true;
                    return true;
                }

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

                if (_lastCorrection is not null)
                    ClearCorrectionState();

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

            if (_lastCorrection is not null)
                ClearCorrectionState();

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

        _lastCorrection = new CorrectionRecord(
            original,
            corrected,
            wasPersian,
            delimiter,
            prefix,
            DateTime.UtcNow);
        _undoBackspaces = 0;
    }

    private bool TryHandleUndoBackspace()
    {
        var record = _lastCorrection;
        if (record is null)
            return false;

        if ((DateTime.UtcNow - record.AppliedAt).TotalSeconds > RevertWindowSec)
        {
            ClearCorrectionState();
            return false;
        }

        _undoBackspaces++;
        
        var target = record.Corrected.Length +
                     (string.IsNullOrEmpty(record.Delimiter) ? 0 : 1) +
                     (string.IsNullOrEmpty(record.Prefix) ? 0 : 1);
        
        if (_undoBackspaces < target)
            return false;

        RevertCorrection(record);
        return true;
    }

    private void RevertCorrection(CorrectionRecord record)
    {
        AppLog.Write($"Revert '{record.Corrected}' -> '{record.Original}'");

        var correctedIsPersian = Mapper.CountPersian(record.Corrected) > record.Corrected.Length / 2;
        var deleteCount = record.Corrected.Length +
                          (string.IsNullOrEmpty(record.Delimiter) ? 0 : 1) +
                          (string.IsNullOrEmpty(record.Prefix) ? 0 : 1);

        IsCorrecting = true;
        try
        {
            InputSimulator.SendBackspaces(deleteCount);
            
            LayoutManager.SwitchForCorrection(correctedIsPersian, record.LayoutWasPersian);
            Thread.Sleep(30);

            var restoredPrefix = string.IsNullOrEmpty(record.Prefix) ? "" : " ";

            InputSimulator.SendText(restoredPrefix + record.Original + record.Delimiter);
        }
        finally
        {
            IsCorrecting = false;
        }

        ClearCorrectionState();
    }

    private void ClearCorrectionState()
    {
        _lastCorrection = null;
        _undoBackspaces = 0;
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

    private sealed record CorrectionRecord(
        string Original,
        string Corrected,
        bool LayoutWasPersian,
        string Delimiter,
        string Prefix,
        DateTime AppliedAt);
}