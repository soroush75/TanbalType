import Carbon

/// نگاشت بین کلیدهای کیبورد US انگلیسی و چیدمان فارسی.
///
/// برخلاف ویندوز، چیدمان‌های فارسی مک با هم فرق دارند (Persian-ISIRI2901، Persian، Persian-QWERTY)
/// و هیچ‌کدام دقیقاً مثل چیدمان ویندوز نیست. پس نگاشت از روی چیدمان فارسی‌ای که کاربر در سیستم
/// فعال کرده ساخته می‌شود (`configure(from:)`)؛ نگاشت ویندوز فقط پیش‌فرض است.
enum Mapper {
    /// لایهٔ پایهٔ چیدمان «Persian» ویندوز — همان نگاشت نسخهٔ ویندوز (پیش‌فرض و مبنای self-test).
    static let windowsBaseKeys: [Character: Character] = [
        "`": "ذ", "1": "۱", "2": "۲", "3": "۳", "4": "۴",
        "5": "۵", "6": "۶", "7": "۷", "8": "۸", "9": "۹",
        "0": "۰", "-": "-", "=": "=",
        "q": "ض", "w": "ص", "e": "ث", "r": "ق", "t": "ف",
        "y": "غ", "u": "ع", "i": "ه", "o": "خ", "p": "ح",
        "[": "ج", "]": "چ", "\\": "پ",
        "a": "ش", "s": "س", "d": "ی", "f": "ب", "g": "ل",
        "h": "ا", "j": "ت", "k": "ن", "l": "م",
        ";": "ک", "'": "گ",
        "z": "ظ", "x": "ط", "c": "ز", "v": "ر", "b": "ذ",
        "n": "د", "m": "ئ", ",": "و", ".": ".", "/": "/",
    ]

    /// «آ» با Shift+H تایپ می‌شود؛ بدون این نگاشت واژه‌هایی مثل «آقا» (Hrh) تشخیص داده نمی‌شدند.
    static let windowsShiftedKeys: [Character: Character] = ["H": "آ"]

    /// حروفی که لغت‌نامه با آن‌ها ساخته شده؛ فقط نگاشت‌های Shift دارِ منتهی به این حروف پذیرفته می‌شوند
    /// (اعراب، «ي»/«ك» عربی و علائم نباید حروف بزرگ انگلیسی را خراب کنند).
    private static let dictionaryLetters = Set("ابپتثجچحخدذرزژسشصضطظعغفقکگلمنوهیآئءؤ")

    private(set) static var enKeyToFa: [Character: Character] = [:]
    private(set) static var enLayoutKeys: Set<Character> = []
    private static var faToEnKey: [Character: Character] = [:]

    static let persianPunctAscii: Set<Character> = [",", ";"]

    /// شناسهٔ چیدمانی که نگاشت فعلی از آن ساخته شده (برای جلوگیری از ساخت مجدد).
    private(set) static var sourceID = ""

    static func useWindowsLayout() {
        apply(base: windowsBaseKeys, shifted: windowsShiftedKeys, id: "windows")
    }

    /// نگاشت را از روی جدول کلیدهای یک چیدمان فارسی مک می‌سازد.
    @discardableResult
    static func configure(from source: TISInputSource) -> Bool {
        let id = LayoutManager.sourceID(source)
        if id == sourceID { return true }

        guard let layout = KeyLayout(source: source) else { return false }

        var base: [Character: Character] = [:]
        var shifted: [Character: Character] = [:]
        for key in KeyCodes.usKeys {
            if let fa = layout.translate(key.code, shift: false) {
                base[key.plain] = fa
            }
        }

        // Shift فقط وقتی لحاظ می‌شود که حرفی تولید کند که در لایهٔ پایه نیست (مثل آ، ژ، ء، ؤ، ئ)
        let baseLetters = Set(base.values)
        for key in KeyCodes.usKeys where key.plain.isLetter {
            if let fa = layout.translate(key.code, shift: true),
               dictionaryLetters.contains(fa),
               !baseLetters.contains(fa) {
                shifted[key.shifted] = fa
            }
        }

        // اگر چیدمان حروف فارسی کافی نداشت (مثلاً چیدمان عجیب)، نگاشت قبلی حفظ می‌شود
        guard base.values.filter({ dictionaryLetters.contains($0) }).count >= 25 else {
            AppLog.write("Mapper: layout \(id) has too few Persian letters, ignored")
            return false
        }

        apply(base: base, shifted: shifted, id: id)
        AppLog.write("Mapper: using layout \(id) (\(base.count) base, \(shifted.count) shifted)")
        return true
    }

    private static func apply(base: [Character: Character], shifted: [Character: Character], id: String) {
        var map = base

        // حروف بزرگ (Caps Lock یا Shift) مثل حرف کوچکِ خودشان نگاشت شوند
        for (key, fa) in base where key.isASCII && key.isLowercase {
            map[Character(key.uppercased())] = fa
        }

        // نگاشت اختصاصی Shift بر فالبکِ بالا اولویت دارد
        for (key, fa) in shifted {
            map[key] = fa
        }

        // در نگاشت معکوس a-z بر ارقام/علائم اولویت دارد (مثلاً b و ` هر دو → ذ)
        var reverse: [Character: Character] = [:]
        let ordered = map.sorted { lhs, rhs in
            let lp = reverseMapPriority(lhs.key), rp = reverseMapPriority(rhs.key)
            return lp != rp ? lp < rp : lhs.key < rhs.key
        }
        for (key, fa) in ordered where reverse[fa] == nil {
            reverse[fa] = key
        }

        enKeyToFa = map
        enLayoutKeys = Set(map.keys)
        faToEnKey = reverse
        sourceID = id
    }

    private static func reverseMapPriority(_ key: Character) -> Int {
        if key >= "a" && key <= "z" { return 0 }
        if key >= "0" && key <= "9" { return 1 }
        return 2
    }

    static func enKeysToPersian(_ text: String) -> String {
        String(text.map { enKeyToFa[$0] ?? $0 })
    }

    static func persianToEnKeys(_ text: String) -> String {
        String(text.map { faToEnKey[$0] ?? $0 })
    }

    /// همان متن، اگر با چیدمانِ مقابل تایپ می‌شد چه شکلی می‌شد.
    static func swapLayout(_ text: String) -> String {
        countPersian(text) > 0 ? persianToEnKeys(text) : enKeysToPersian(text)
    }

    static func countEnLayoutKeys(_ text: String) -> Int {
        text.reduce(0) { $0 + (enLayoutKeys.contains($1) ? 1 : 0) }
    }

    static func isPersianChar(_ ch: Character) -> Bool {
        guard let scalar = ch.unicodeScalars.first else { return false }
        return (0x0600...0x06FF).contains(scalar.value)
    }

    static func countPersian(_ text: String) -> Int {
        text.reduce(0) { $0 + (isPersianChar($1) ? 1 : 0) }
    }

    static func countAsciiLetters(_ text: String) -> Int {
        text.reduce(0) { $0 + ($1.isASCII && $1.isLetter ? 1 : 0) }
    }
}

/// خواندن جدول کلیدهای یک چیدمان (uchr) با UCKeyTranslate.
struct KeyLayout {
    private let data: CFData

    init?(source: TISInputSource) {
        guard let ptr = TISGetInputSourceProperty(source, kTISPropertyUnicodeKeyLayoutData) else {
            return nil
        }
        data = Unmanaged<CFData>.fromOpaque(ptr).takeUnretainedValue()
    }

    func translate(_ keyCode: UInt16, shift: Bool) -> Character? {
        guard let bytes = CFDataGetBytePtr(data) else { return nil }
        return bytes.withMemoryRebound(to: UCKeyboardLayout.self, capacity: 1) { layout in
            var deadKeys: UInt32 = 0
            var length = 0
            var chars = [UniChar](repeating: 0, count: 4)
            let modifiers: UInt32 = shift ? UInt32(shiftKey >> 8) : 0
            let status = UCKeyTranslate(
                layout, keyCode, UInt16(kUCKeyActionDown), modifiers,
                UInt32(LMGetKbdType()), OptionBits(kUCKeyTranslateNoDeadKeysBit),
                &deadKeys, chars.count, &length, &chars)
            guard status == noErr, length > 0 else { return nil }
            let string = String(utf16CodeUnits: chars, count: length)
            guard string.count == 1, let ch = string.first,
                  !ch.isWhitespace, ch.unicodeScalars.allSatisfy({ !$0.properties.isDefaultIgnorableCodePoint })
            else { return nil }
            return ch
        }
    }
}
