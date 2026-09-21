import CoreGraphics

/// کدهای مجازی کلیدهای مک (مستقل از چیدمان، بر اساس جای فیزیکی کلید روی کیبورد ANSI).
enum KeyCodes {
    static let returnKey: UInt16 = 36
    static let tab: UInt16 = 48
    static let space: UInt16 = 49
    static let delete: UInt16 = 51 // Backspace
    static let keypadEnter: UInt16 = 76
    static let f10: UInt16 = 109

    struct USKey {
        let code: UInt16
        let plain: Character
        let shifted: Character
    }

    /// کلیدهای کیبورد US با خروجی بدون Shift و با Shift.
    static let usKeys: [USKey] = [
        USKey(code: 50, plain: "`", shifted: "~"),
        USKey(code: 18, plain: "1", shifted: "!"),
        USKey(code: 19, plain: "2", shifted: "@"),
        USKey(code: 20, plain: "3", shifted: "#"),
        USKey(code: 21, plain: "4", shifted: "$"),
        USKey(code: 23, plain: "5", shifted: "%"),
        USKey(code: 22, plain: "6", shifted: "^"),
        USKey(code: 26, plain: "7", shifted: "&"),
        USKey(code: 28, plain: "8", shifted: "*"),
        USKey(code: 25, plain: "9", shifted: "("),
        USKey(code: 29, plain: "0", shifted: ")"),
        USKey(code: 27, plain: "-", shifted: "_"),
        USKey(code: 24, plain: "=", shifted: "+"),
        USKey(code: 12, plain: "q", shifted: "Q"),
        USKey(code: 13, plain: "w", shifted: "W"),
        USKey(code: 14, plain: "e", shifted: "E"),
        USKey(code: 15, plain: "r", shifted: "R"),
        USKey(code: 17, plain: "t", shifted: "T"),
        USKey(code: 16, plain: "y", shifted: "Y"),
        USKey(code: 32, plain: "u", shifted: "U"),
        USKey(code: 34, plain: "i", shifted: "I"),
        USKey(code: 31, plain: "o", shifted: "O"),
        USKey(code: 35, plain: "p", shifted: "P"),
        USKey(code: 33, plain: "[", shifted: "{"),
        USKey(code: 30, plain: "]", shifted: "}"),
        USKey(code: 42, plain: "\\", shifted: "|"),
        USKey(code: 0, plain: "a", shifted: "A"),
        USKey(code: 1, plain: "s", shifted: "S"),
        USKey(code: 2, plain: "d", shifted: "D"),
        USKey(code: 3, plain: "f", shifted: "F"),
        USKey(code: 5, plain: "g", shifted: "G"),
        USKey(code: 4, plain: "h", shifted: "H"),
        USKey(code: 38, plain: "j", shifted: "J"),
        USKey(code: 40, plain: "k", shifted: "K"),
        USKey(code: 37, plain: "l", shifted: "L"),
        USKey(code: 41, plain: ";", shifted: ":"),
        USKey(code: 39, plain: "'", shifted: "\""),
        USKey(code: 6, plain: "z", shifted: "Z"),
        USKey(code: 7, plain: "x", shifted: "X"),
        USKey(code: 8, plain: "c", shifted: "C"),
        USKey(code: 9, plain: "v", shifted: "V"),
        USKey(code: 11, plain: "b", shifted: "B"),
        USKey(code: 45, plain: "n", shifted: "N"),
        USKey(code: 46, plain: "m", shifted: "M"),
        USKey(code: 43, plain: ",", shifted: "<"),
        USKey(code: 47, plain: ".", shifted: ">"),
        USKey(code: 44, plain: "/", shifted: "?"),
    ]

    private static let byCode: [UInt16: USKey] =
        Dictionary(uniqueKeysWithValues: usKeys.map { ($0.code, $0) })

    /// کاراکترِ کلید فیزیکی روی چیدمان US (برای نگاشت به فارسی). Caps Lock فقط روی حروف اثر دارد.
    static func physicalChar(_ code: UInt16, flags: CGEventFlags) -> Character? {
        guard let key = byCode[code] else { return nil }
        let shift = flags.contains(.maskShift)
        if key.plain.isLetter {
            let caps = flags.contains(.maskAlphaShift)
            return shift != caps ? key.shifted : key.plain
        }
        return shift ? key.shifted : key.plain
    }
}
