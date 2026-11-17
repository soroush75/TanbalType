namespace TanbalType;

internal sealed class CorrectionService
{
    private readonly object _lock = new();
    private readonly Action<Action> _dispatch;
    private string _buffer = string.Empty;
    private bool? _bufferLayoutIsPersian;
    private char? _lastDelimiter = null; // حافظه برای ذخیره آخرین کلید جداکننده
    private IntPtr _lastForegroundWindow = IntPtr.Zero;
    private IntPtr _lastFocusedControl = IntPtr.Zero;

    /// <summary>
    /// آیا می‌دانیم آنچه در بافر است از آغازِ یک کلمه شروع شده؟ یعنی پیش از آن
    /// فاصله/اینتر بوده، نه حرفی از کلمه‌ای که از قبل آن‌جا نوشته شده است.
    ///
    /// بافر تنها تا زمانی با متنِ واقعیِ پیشِ مکان‌نما می‌خواند که کاربر پشتِ‌سرهم
    /// تایپ کند. با عوض‌شدنِ پنجره، کلیکِ ماوس، کلیدهای جهت یا میان‌برها، مکان‌نما
    /// می‌تواند وسطِ کلمه‌ای باشد که از قبل نوشته شده؛ آن‌وقت «ing» که کاربر به
    /// «engineer» می‌چسباند به‌تنهایی سنجیده می‌شود و «هدل» از آن درمی‌آید.
    /// در آن حالت این پرچم false است و <see cref="Detector"/> فقط شواهدِ قطعی را
    /// می‌پذیرد.
    /// </summary>
    private bool _wordStartKnown;
    private bool _enabled = true;

    // درخواستِ دورریختنِ بافر، از نخی جز نخِ کلیدها (کلیکِ ماوس، خاموش/روشن‌شدن).
    // بی‌قفل ست می‌شود و در کلیدِ بعدی اعمال می‌شود — توضیح در InvalidateBuffer.
    private volatile bool _invalidateRequested;

    public CorrectionService(Action<Action> dispatch)
    {
        _dispatch = dispatch;
    }

    /// <summary>
    /// در مدتی که برنامه خاموش است کلیدها شمرده نمی‌شوند، پس با هر بار خاموش یا
    /// روشن شدن (F10 یا منوی tray) بافر بی‌اعتبار می‌شود و از صفر شروع می‌کنیم.
    /// </summary>
    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            InvalidateBuffer();
        }
    }

    public bool IsCorrecting { get; private set; }

    public bool ShouldIgnoreEvent(IntPtr extraInfo) =>
        extraInfo.ToInt64() == NativeMethods.SyntheticMarker.ToInt64();

    /// <summary>
    /// از بیرون (کلیکِ ماوس) خبر می‌دهد که مکان‌نما ممکن است جابه‌جا شده باشد، پس
    /// بافر و دانستهٔ ما دربارهٔ متنِ پیشِ آن بی‌اعتبار است. از هر نخی صدا زدنش آزاد است.
    ///
    /// این‌جا عمداً قفل گرفته نمی‌شود: callbackِ hookِ ماوس باید بی‌درنگ برگردد، و
    /// همان قفل را گاه نخِ اصلاح (Thread.Sleep + فرستادنِ کلیدها) یا بارِ اولِ
    /// خواندنِ لغت‌نامه (۳۳۲ هزار واژه) ده‌ها میلی‌ثانیه نگه می‌دارد؛ معطل‌شدنِ
    /// hook یعنی لگِ ماوس و در بدترین حالت برداشتنِ hook توسط ویندوز.
    /// پس فقط پرچم ست می‌شود و در کلیدِ بعدی، داخلِ همان قفل، اعمال می‌شود.
    /// </summary>
    public void InvalidateBuffer() => _invalidateRequested = true;

    /// <summary>
    /// بافر پاک می‌شود و «آغازِ کلمه» نامعلوم می‌شود: از این پس نمی‌دانیم پیش از
    /// آنچه کاربر می‌نویسد فاصله بوده یا حرفی از یک کلمهٔ نیمه‌تمام.
    /// </summary>
    private void ResetBuffer()
    {
        _buffer = string.Empty;
        _bufferLayoutIsPersian = null;
        _lastDelimiter = null;
        _wordStartKnown = false;
    }

    public bool HandleKey(uint vkCode, uint scanCode, out bool consume)
    {
        consume = false;
        if (!Enabled || IsCorrecting)
            return false;

        // میان‌بر (Ctrl/Alt/Win + …) هر کاری می‌تواند بکند — چسباندن، undo، انتخابِ
        // همه — پس پس از آن معلوم نیست چه متنی پیشِ مکان‌نما نشسته است.
        if (IsModifierDown())
        {
            InvalidateBuffer();
            return false;
        }

        lock (_lock)
        {
            // کلیکِ ماوس یا خاموش/روشن‌شدنِ برنامه از نخی دیگر خبر داده است؛ همین‌جا
            // — نه داخلِ خودِ hookِ ماوس — بافر دور ریخته می‌شود.
            if (_invalidateRequested)
            {
                _invalidateRequested = false;
                ResetBuffer();
            }

            // با تغییر پنجرهٔ فعال یا کادرِ متنیِ فعال، بافر پاک می‌شود تا متنِ یک
            // برنامه به برنامهٔ دیگر منتقل نشود. آن‌جا که کاربر می‌رود هم معلوم نیست
            // مکان‌نما کجای متن است، پس آغازِ کلمه نامعلوم می‌شود.
            var foreground = NativeMethods.GetForegroundWindow();
            var focused = WindowContext.GetFocusedControl();

            // بعضی برنامه‌ها هندلِ کادرِ فعال را نمی‌دهند. «نامعلوم» دلیلِ عوض‌شدنِ
            // کادر نیست، پس همان هندلِ قبلی نگه داشته می‌شود تا بافر وسطِ کلمه پاک نشود.
            if (focused == IntPtr.Zero)
                focused = _lastFocusedControl;

            if (foreground != _lastForegroundWindow || focused != _lastFocusedControl)
            {
                _lastForegroundWindow = foreground;
                _lastFocusedControl = focused;
                ResetBuffer();
            }

            // در کادر رمز عبور اصلاً بافر نمی‌کنیم و اصلاح نمی‌کنیم تا رمز خراب نشود.
            if (WindowContext.IsPasswordEdit(focused))
            {
                ResetBuffer();
                return false;
            }

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
                    // پاک‌کردن از پیشِ بافر: آن حرف را ما نشمرده‌ایم، پس نه می‌دانیم
                    // چه بوده و نه چه چیزی پیش‌تر از آن مانده است.
                    _lastDelimiter = null;
                    _wordStartKnown = false;
                }
                return false;
            }

            // کلیدهای جهت و ویرایش، مکان‌نما را جابه‌جا می‌کنند یا متنِ اطرافش را عوض
            // می‌کنند؛ از آن پس بافر با متنِ پیشِ مکان‌نما نمی‌خواند.
            if (IsCaretMovingKey(vkCode))
            {
                ResetBuffer();
                return false;
            }

            if (IsDelimiterKey(vkCode))
            {
                var delimiterChar = KeyReader.ReadDelimiter(vkCode, scanCode) ?? ' ';
                var layoutIsPersian = _bufferLayoutIsPersian ?? persian;
                var word = _buffer;
                var wordStartKnown = _wordStartKnown;
                _buffer = string.Empty;
                _bufferLayoutIsPersian = null;

                // این جداکننده خودش مرزِ کلمه است، پس آغازِ کلمهٔ بعدی معلوم است.
                _wordStartKnown = true;

                var corrected = MaybeCorrect(word, layoutIsPersian, wordStartKnown);
                // توجه: با فعال‌بودن log، متن تایپ‌شده هم ذخیره می‌شود (برای عیب‌یابی).
                // log پیش‌فرض خاموش است و فقط با انتخاب کاربر از منوی tray فعال می‌شود.
                AppLog.Write($"Delimiter vk={vkCode} layout={(layoutIsPersian ? "fa" : "en")} start={(wordStartKnown ? "known" : "unknown")} word='{word}' -> '{corrected ?? word}'");

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

    private string? MaybeCorrect(string word, bool layoutIsPersian, bool wordStartKnown)
    {
        if (string.IsNullOrWhiteSpace(word) || word.Length < 2)
            return null;

        var corrected = Detector.DetectWrongLayout(word, layoutIsPersian, wordStartKnown);
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

    // داخلِ low-level hook وضعیتِ کلیدها باید async خوانده شود؛ GetKeyState وضعیتِ
    // صفِ پیامِ همین نخ را می‌دهد که کلیدهای برنامهٔ دیگر را ندیده است.
    private static bool IsPressed(uint vk) =>
        (NativeMethods.GetAsyncKeyState((int)vk) & 0x8000) != 0;

    private static bool IsDelimiterKey(uint vkCode) =>
        vkCode is 0x20 or NativeMethods.VkReturn or NativeMethods.VkTab;

    /// <summary>
    /// کلیدهایی که مکان‌نما را جابه‌جا می‌کنند (جهت‌ها، Home/End، Page Up/Down) یا
    /// متنِ اطرافش را عوض می‌کنند (Delete/Insert) و همگامیِ بافر را از بین می‌برند.
    /// </summary>
    private static bool IsCaretMovingKey(uint vkCode) =>
        vkCode is (>= NativeMethods.VkPrior and <= NativeMethods.VkDown)
            or NativeMethods.VkInsert
            or NativeMethods.VkDelete
            or NativeMethods.VkEscape;
}
