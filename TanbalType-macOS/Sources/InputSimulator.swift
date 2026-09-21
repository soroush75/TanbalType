import CoreGraphics
import Foundation

/// ارسال کلیدهای مصنوعی. همهٔ رویدادها علامت‌گذاری می‌شوند تا event tap خودمان آن‌ها را نادیده بگیرد.
enum InputSimulator {
    /// مقدار دلخواه در فیلد eventSourceUserData ("TANB")
    static let syntheticMarker: Int64 = 0x5441_4E42

    private static let source: CGEventSource? = {
        let s = CGEventSource(stateID: .privateState)
        s?.userData = syntheticMarker
        return s
    }()

    static func isSynthetic(_ event: CGEvent) -> Bool {
        event.getIntegerValueField(.eventSourceUserData) == syntheticMarker
    }

    static func sendBackspaces(_ count: Int) {
        for _ in 0..<max(count, 0) {
            sendKey(KeyCodes.delete)
        }
    }

    static func sendText(_ text: String) {
        for ch in text {
            switch ch {
            case "\n", "\r\n": sendKey(KeyCodes.returnKey)
            case "\t": sendKey(KeyCodes.tab)
            default: sendUnicode(Array(String(ch).utf16))
            }
        }
    }

    private static func sendKey(_ code: UInt16) {
        for down in [true, false] {
            guard let event = CGEvent(keyboardEventSource: source, virtualKey: code, keyDown: down) else { continue }
            post(event)
        }
    }

    private static func sendUnicode(_ units: [UniChar]) {
        for down in [true, false] {
            guard let event = CGEvent(keyboardEventSource: source, virtualKey: 0, keyDown: down) else { continue }
            units.withUnsafeBufferPointer {
                event.keyboardSetUnicodeString(stringLength: $0.count, unicodeString: $0.baseAddress)
            }
            post(event)
        }
    }

    private static func post(_ event: CGEvent) {
        event.flags = []
        event.setIntegerValueField(.eventSourceUserData, value: syntheticMarker)
        event.post(tap: .cghidEventTap)
        // مکث کوتاه تا برنامه‌های کند (مثلاً Electron) ترتیب رویدادها را گم نکنند
        usleep(1_500)
    }
}
