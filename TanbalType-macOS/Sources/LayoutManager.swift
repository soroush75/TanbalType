import Carbon

/// تشخیص و تعویض چیدمان کیبورد با Text Input Sources (TIS).
/// همهٔ متدها باید روی نخ اصلی صدا زده شوند.
enum LayoutManager {
    private static var lastPersianID: String?
    private static var lastEnglishID: String?

    static func sourceID(_ source: TISInputSource) -> String {
        stringProperty(source, kTISPropertyInputSourceID) ?? ""
    }

    private static func stringProperty(_ source: TISInputSource, _ key: CFString) -> String? {
        guard let ptr = TISGetInputSourceProperty(source, key) else { return nil }
        return Unmanaged<CFString>.fromOpaque(ptr).takeUnretainedValue() as String
    }

    private static func boolProperty(_ source: TISInputSource, _ key: CFString) -> Bool {
        guard let ptr = TISGetInputSourceProperty(source, key) else { return false }
        return CFBooleanGetValue(Unmanaged<CFBoolean>.fromOpaque(ptr).takeUnretainedValue())
    }

    static func isPersian(_ source: TISInputSource) -> Bool {
        if let ptr = TISGetInputSourceProperty(source, kTISPropertyInputSourceLanguages) {
            let languages = Unmanaged<CFArray>.fromOpaque(ptr).takeUnretainedValue() as? [String] ?? []
            if languages.first == "fa" { return true }
        }
        let id = sourceID(source).lowercased()
        return id.contains("persian") || id.contains("farsi")
    }

    static var current: TISInputSource {
        TISCopyCurrentKeyboardInputSource().takeRetainedValue()
    }

    static func isPersianLayout() -> Bool {
        isPersian(current)
    }

    /// چیدمان‌هایی که کاربر در System Settings › Keyboard › Input Sources فعال کرده.
    private static func enabledSources() -> [TISInputSource] {
        let filter = [
            kTISPropertyInputSourceCategory as String: kTISCategoryKeyboardInputSource as String,
            kTISPropertyInputSourceIsSelectCapable as String: true,
        ] as CFDictionary
        return TISCreateInputSourceList(filter, false)?.takeRetainedValue() as? [TISInputSource] ?? []
    }

    /// هر چیدمانِ نصب‌شده با این شناسه (حتی اگر فعال نباشد).
    static func source(withID id: String) -> TISInputSource? {
        let filter = [kTISPropertyInputSourceID as String: id] as CFDictionary
        let list = TISCreateInputSourceList(filter, true)?.takeRetainedValue() as? [TISInputSource]
        return list?.first
    }

    /// همهٔ چیدمان‌های فارسیِ نصب‌شده روی سیستم (برای self-test).
    static func installedPersianLayouts() -> [TISInputSource] {
        let filter = [kTISPropertyInputSourceType as String: kTISTypeKeyboardLayout as String] as CFDictionary
        let list = TISCreateInputSourceList(filter, true)?.takeRetainedValue() as? [TISInputSource] ?? []
        return list.filter(isPersian)
    }

    static func persianTarget() -> TISInputSource? {
        let sources = enabledSources()
        if let id = lastPersianID, let s = sources.first(where: { sourceID($0) == id }) {
            return s
        }
        return sources.first(where: isPersian)
    }

    static func englishTarget() -> TISInputSource? {
        let sources = enabledSources().filter {
            !isPersian($0) && boolProperty($0, kTISPropertyInputSourceIsASCIICapable)
        }
        if let id = lastEnglishID, let s = sources.first(where: { sourceID($0) == id }) {
            return s
        }
        let preferred = ["com.apple.keylayout.US", "com.apple.keylayout.ABC"]
        return sources.first(where: { preferred.contains(sourceID($0)) }) ?? sources.first
    }

    /// با تغییر چیدمان (توسط کاربر یا برنامه) صدا زده می‌شود تا آخرین چیدمانِ هر زبان
    /// به خاطر سپرده شود و نگاشت کلیدها با چیدمانِ فارسیِ فعلی هماهنگ بماند.
    static func refresh() {
        let source = current
        let id = sourceID(source)
        if isPersian(source) {
            lastPersianID = id
            Mapper.configure(from: source)
        } else if boolProperty(source, kTISPropertyInputSourceIsASCIICapable) {
            lastEnglishID = id
        }

        // هنوز هیچ نگاشتی ساخته نشده (مثلاً برنامه روی حالت انگلیسی اجرا شده)
        if Mapper.sourceID.isEmpty {
            if let persian = persianTarget(), Mapper.configure(from: persian) {
                return
            }
            AppLog.write("No usable Persian input source enabled — using Windows key map")
            Mapper.useWindowsLayout()
        }
    }

    static func switchForCorrection(wasPersian: Bool, correctedIsPersian: Bool) {
        if correctedIsPersian == wasPersian {
            AppLog.write("Layout switch: skipped (correction same language as layout)")
            return
        }

        let label = correctedIsPersian ? "Persian" : "English"
        if isPersianLayout() == correctedIsPersian {
            AppLog.write("Layout switch: already \(label)")
            return
        }

        guard let target = correctedIsPersian ? persianTarget() : englishTarget() else {
            AppLog.write("Layout switch FAILED: no \(label) input source enabled in System Settings")
            return
        }

        let status = TISSelectInputSource(target)
        if status == noErr && isPersianLayout() == correctedIsPersian {
            AppLog.write("Layout switch OK -> \(label) (\(sourceID(target)))")
        } else {
            AppLog.write("Layout switch FAILED -> \(label) status=\(status)")
        }
    }
}
