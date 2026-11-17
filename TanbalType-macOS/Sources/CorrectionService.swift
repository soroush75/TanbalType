import AppKit
import Carbon

/// بافر کردن کلمهٔ در حال تایپ و اصلاح آن هنگام زدن Space/Enter/Tab.
/// همهٔ متدها روی نخ اصلی (همان نخ event tap) اجرا می‌شوند؛ خودِ اصلاح روی یک صف پس‌زمینه
/// انجام می‌شود تا event tap در حین ارسال کلیدها مسدود نشود.
final class CorrectionService {
    var enabled = true
    private(set) var isCorrecting = false

    /// پس از پایان هر اصلاح صدا زده می‌شود (روی نخ اصلی).
    var correctionFinished: (() -> Void)?

    private var buffer = ""
    private var bufferLayoutIsPersian: Bool?
    private var lastDelimiter: Character? // آخرین کلید جداکننده
    private let queue = DispatchQueue(label: "TanbalType.correction", qos: .userInteractive)

    /// آیا می‌دانیم آنچه در بافر است از آغازِ یک کلمه شروع شده؟ یعنی پیش از آن فاصله/اینتر
    /// بوده، نه حرفی از کلمه‌ای که از قبل آن‌جا نوشته شده است.
    ///
    /// بافر تنها تا زمانی با متنِ واقعیِ پیشِ مکان‌نما می‌خواند که کاربر پشتِ‌سرهم تایپ کند. با
    /// عوض‌شدنِ برنامهٔ فعال، کلیکِ ماوس، کلیدهای جهت یا میان‌برها، مکان‌نما می‌تواند وسطِ
    /// کلمه‌ای باشد که از قبل نوشته شده؛ آن‌وقت «ing» که کاربر به «engineer» می‌چسباند
    /// به‌تنهایی سنجیده می‌شود و «هدل» از آن درمی‌آید. در آن حالت این پرچم false است و
    /// `Detector` فقط شواهدِ قطعی را می‌پذیرد.
    private var wordStartKnown = false

    /// درخواستِ دورریختنِ بافر از بیرونِ مسیرِ کلیدها (کلیکِ ماوس) — توضیح در `invalidateBuffer`.
    private var invalidateRequested = false

    /// با تغییر برنامهٔ فعال صدا زده می‌شود تا متنِ یک برنامه به برنامهٔ دیگر منتقل نشود.
    ///
    /// بافر پاک می‌شود و «آغازِ کلمه» نامعلوم می‌شود: از این پس نمی‌دانیم پیش از آنچه کاربر
    /// می‌نویسد فاصله بوده یا حرفی از یک کلمهٔ نیمه‌تمام.
    func reset() {
        buffer = ""
        bufferLayoutIsPersian = nil
        lastDelimiter = nil
        wordStartKnown = false
    }

    /// از بیرون (کلیکِ ماوس) خبر می‌دهد که مکان‌نما ممکن است جابه‌جا شده باشد، پس بافر و
    /// دانستهٔ ما دربارهٔ متنِ پیشِ آن بی‌اعتبار است.
    ///
    /// این‌جا عمداً هیچ کارِ دیگری انجام نمی‌شود: callbackِ event tap باید بی‌درنگ برگردد،
    /// وگرنه macOS tap را غیرفعال می‌کند (و بارِ اولِ خواندنِ لغت‌نامه — ۳۳۲ هزار واژه — ده‌ها
    /// میلی‌ثانیه طول می‌کشد). پس فقط یک پرچم ست می‌شود که در کلیدِ بعدی اعمال می‌شود.
    func invalidateBuffer() {
        invalidateRequested = true
    }

    /// خروجی true یعنی رویداد باید مصرف شود (به برنامهٔ فعال نرسد).
    func handleKeyDown(_ event: CGEvent) -> Bool {
        guard enabled, !isCorrecting else { return false }

        // کلیکِ ماوس از مسیرِ دیگری خبر داده است؛ همین‌جا — نه داخلِ خودِ callback — بافر دور
        // ریخته می‌شود.
        if invalidateRequested {
            invalidateRequested = false
            reset()
        }

        let flags = event.flags
        if flags.contains(.maskCommand) || flags.contains(.maskControl) || flags.contains(.maskAlternate) {
            // میان‌بر (Cmd/Ctrl/Option + …) هر کاری می‌تواند بکند — چسباندن، undo، انتخابِ همه،
            // تعویضِ زبان — پس پس از آن معلوم نیست چه متنی پیشِ مکان‌نما نشسته است.
            // (فشردنِ تنهای modifier رویدادِ flagsChanged است و اصلاً به این‌جا نمی‌رسد.)
            invalidateBuffer()
            return false
        }

        // در کادر رمز عبور macOS «Secure Input» را روشن می‌کند؛ آنجا بافر نمی‌کنیم و اصلاح نمی‌کنیم
        if IsSecureEventInputEnabled() {
            buffer = ""
            bufferLayoutIsPersian = nil
            // آنچه کاربر در کادر رمز زده را نشمرده‌ایم، پس آغازِ کلمهٔ بعدی نامعلوم است
            wordStartKnown = false
            return false
        }

        // تایپ داخل پنجره‌های خودِ برنامه (مثلاً افزودن لغت شخصی) نباید اصلاح شود
        if NSRunningApplication.current.isActive {
            return false
        }

        let code = UInt16(event.getIntegerValueField(.keyboardEventKeycode))
        let persian = LayoutManager.isPersianLayout()

        if code == KeyCodes.delete {
            // پاک‌کردن یعنی پاک‌کردن: برنامه نباید متن را بازگرداند یا دوباره تصمیم به اصلاح بگیرد
            if !buffer.isEmpty {
                buffer.removeLast()
                if buffer.isEmpty {
                    bufferLayoutIsPersian = nil
                }
            } else {
                // پاک‌کردن از پیشِ بافر: آن حرف را ما نشمرده‌ایم، پس نه می‌دانیم چه بوده و نه
                // چه چیزی پیش‌تر از آن مانده است
                lastDelimiter = nil
                wordStartKnown = false
            }
            return false
        }

        // کلیدهای جهت و ویرایش، مکان‌نما را جابه‌جا می‌کنند یا متنِ اطرافش را عوض می‌کنند؛
        // از آن پس بافر با متنِ پیشِ مکان‌نما نمی‌خواند.
        if KeyCodes.caretMoving.contains(code) {
            reset()
            return false
        }

        if let delimiter = Self.delimiter(for: code) {
            let layoutIsPersian = bufferLayoutIsPersian ?? persian
            let word = buffer
            let wordStartWasKnown = wordStartKnown
            buffer = ""
            bufferLayoutIsPersian = nil

            // این جداکننده خودش مرزِ کلمه است، پس آغازِ کلمهٔ بعدی معلوم است.
            wordStartKnown = true

            let corrected = maybeCorrect(word, layoutIsPersian: layoutIsPersian,
                                         wordStartKnown: wordStartWasKnown)
            // توجه: با فعال‌بودن log، متن تایپ‌شده هم ذخیره می‌شود (برای عیب‌یابی).
            AppLog.write("Delimiter key=\(code) layout=\(layoutIsPersian ? "fa" : "en") start=\(wordStartWasKnown ? "known" : "unknown") word='\(word)' -> '\(corrected ?? word)'")

            // آیا کلمهٔ قبلی واقعاً با Space جدا شده است یا مثلاً با Enter (اول خط)
            let replacePrevSpace = lastDelimiter == " "
            lastDelimiter = delimiter

            guard let corrected else { return false }
            scheduleCorrection(original: word, corrected: corrected, delimiter: String(delimiter),
                               layoutWasPersian: layoutIsPersian, replaceSpace: replacePrevSpace)
            return true
        }

        guard let ch = Self.bufferChar(event, code: code, persian: persian), !ch.isNewline,
              !ch.unicodeScalars.contains(where: { $0.properties.generalCategory == .control })
        else { return false }

        if buffer.isEmpty {
            bufferLayoutIsPersian = persian
        }
        buffer.append(ch)
        return false
    }

    private static func delimiter(for code: UInt16) -> Character? {
        switch code {
        case KeyCodes.space: return " "
        case KeyCodes.returnKey, KeyCodes.keypadEnter: return "\n"
        case KeyCodes.tab: return "\t"
        default: return nil
        }
    }

    /// حالت انگلیسی: کلید فیزیکی US (مثلاً s برای نگاشت به فارسی).
    /// حالت فارسی: همان حرفی که روی صفحه نوشته می‌شود (مثلاً hello → «اثممخ»).
    private static func bufferChar(_ event: CGEvent, code: UInt16, persian: Bool) -> Character? {
        guard let physical = KeyCodes.physicalChar(code, flags: event.flags) else { return nil }
        if !persian {
            return physical
        }

        var length = 0
        var chars = [UniChar](repeating: 0, count: 4)
        event.keyboardGetUnicodeString(maxStringLength: chars.count, actualStringLength: &length, unicodeString: &chars)
        let typed = String(utf16CodeUnits: chars, count: length)
        if typed.count == 1, let ch = typed.first {
            return ch
        }
        return Mapper.enKeysToPersian(String(physical)).first
    }

    private func maybeCorrect(_ word: String, layoutIsPersian: Bool,
                              wordStartKnown: Bool) -> String? {
        if word.trimmingCharacters(in: .whitespaces).isEmpty || word.count < 2 {
            return nil
        }
        let corrected = Detector.detectWrongLayout(word, currentLayoutIsPersian: layoutIsPersian,
                                                   wordStartKnown: wordStartKnown)
        return corrected == word ? nil : corrected
    }

    private func scheduleCorrection(original: String, corrected: String, delimiter: String,
                                    layoutWasPersian: Bool, replaceSpace: Bool) {
        isCorrecting = true

        let correctedIsPersian = Mapper.countPersian(corrected) > corrected.count / 2
        var deleteCount = original.count
        var prefix = ""

        // فقط زمانی Space قبلی را حذف و دوباره تایپ می‌کنیم که مطمئن باشیم اول خط نیستیم
        // (حل مشکل به هم چسبیدن کلمات فارسی و انگلیسی در متن راست‌به‌چپ)
        if correctedIsPersian && !layoutWasPersian && replaceSpace {
            prefix = " "
            deleteCount += 1
        }

        AppLog.write("Apply delete=\(deleteCount) '\(original)' -> '\(prefix)\(corrected)'")

        queue.async { [weak self] in
            usleep(15_000)
            InputSimulator.sendBackspaces(deleteCount)

            DispatchQueue.main.sync {
                LayoutManager.switchForCorrection(wasPersian: layoutWasPersian, correctedIsPersian: correctedIsPersian)
            }
            usleep(30_000)

            InputSimulator.sendText(prefix + corrected + delimiter)
            usleep(5_000)

            DispatchQueue.main.async {
                self?.isCorrecting = false
                self?.correctionFinished?()
            }
        }
    }
}
