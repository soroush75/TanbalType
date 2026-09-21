import Foundation

/// فایل‌های تنظیمات کاربر در ~/Library/Application Support/TanbalType
enum AppSupport {
    static let directory: URL = FileManager.default
        .urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
        .appendingPathComponent("TanbalType", isDirectory: true)

    static func readLines(_ name: String) -> [String] {
        let url = directory.appendingPathComponent(name)
        guard let text = try? String(contentsOf: url, encoding: .utf8) else { return [] }
        return text.components(separatedBy: .newlines)
            .map { $0.trimmingCharacters(in: .whitespaces) }
            .filter { !$0.isEmpty }
    }

    static func writeLines(_ name: String, _ lines: [String]) {
        do {
            try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
            let text = lines.joined(separator: "\n") + (lines.isEmpty ? "" : "\n")
            try text.write(to: directory.appendingPathComponent(name), atomically: true, encoding: .utf8)
        } catch {
            AppLog.write("Save \(name) failed: \(error.localizedDescription)")
        }
    }

    /// مرتب‌سازی بدون حساسیت به حروف بزرگ/کوچک (مثل OrdinalIgnoreCase)
    static func sorted(_ words: some Sequence<String>) -> [String] {
        words.sorted { $0.lowercased() < $1.lowercased() }
    }
}

/// لغاتی که کاربر خودش به برنامه یاد می‌دهد. هر بار که همان کلمه با چیدمانِ اشتباه
/// تایپ شود، برنامه آن را دقیقاً به شکلی که کاربر ثبت کرده اصلاح می‌کند.
enum UserWords {
    private static let fileName = "userwords.txt"
    private static let lock = NSLock()

    // کلید = شکلِ نرمال‌شده (حروف کوچک، بدون نیم‌فاصله)، مقدار = همان چیزی که کاربر وارد کرده
    private static var words: [String: String] = {
        var dict: [String: String] = [:]
        for line in AppSupport.readLines(fileName) {
            let key = normalize(line)
            if !key.isEmpty { dict[key] = line }
        }
        return dict
    }()

    static func all() -> [String] {
        lock.lock(); defer { lock.unlock() }
        return AppSupport.sorted(words.values)
    }

    /// آیا خودِ این کلمه یکی از لغات کاربر است؟ (یعنی درست تایپ شده و نباید تغییر کند)
    static func isUserWord(_ word: String) -> Bool {
        canonical(for: word) != nil
    }

    /// اگر کلمه در فهرستِ کاربر باشد، شکلِ دقیقی که کاربر ثبت کرده را برمی‌گرداند.
    static func canonical(for word: String) -> String? {
        lock.lock(); defer { lock.unlock() }
        if words.isEmpty { return nil }
        return words[normalize(word)]
    }

    @discardableResult
    static func add(_ input: String) -> Bool {
        let word = input.trimmingCharacters(in: .whitespacesAndNewlines)
        let key = normalize(word)
        if key.isEmpty { return false }

        lock.lock(); defer { lock.unlock() }
        if words[key] != nil { return false }
        words[key] = word
        AppSupport.writeLines(fileName, AppSupport.sorted(words.values))
        return true
    }

    @discardableResult
    static func remove(_ word: String) -> Bool {
        lock.lock(); defer { lock.unlock() }
        guard words.removeValue(forKey: normalize(word)) != nil else { return false }
        AppSupport.writeLines(fileName, AppSupport.sorted(words.values))
        return true
    }

    /// یکسان‌سازی نویسه‌های عربی و حذف نیم‌فاصله/نشانگرهای جهت، تا کلمهٔ ثبت‌شده
    /// با خروجیِ نگاشتِ کیبورد (که همیشه «ی» و «ک» فارسی و بدون نیم‌فاصله است) تطبیق پیدا کند.
    private static func normalize(_ word: String) -> String {
        var result = ""
        for ch in word.trimmingCharacters(in: .whitespacesAndNewlines) {
            switch ch {
            case "ي", "ى": result.append("ی")
            case "ك": result.append("ک")
            case "\u{200C}", "\u{200E}", "\u{200F}": continue // نیم‌فاصله، LRM، RLM
            default: result.append(ch)
            }
        }
        return result.lowercased()
    }
}

/// لغات استثنا: کلماتی که برنامه هرگز روی آن‌ها اصلاح خودکار انجام نمی‌دهد.
enum UserExceptions {
    private static let fileName = "exceptions.txt"
    private static let lock = NSLock()

    // کلید = حروف کوچک (مقایسهٔ بدون حساسیت به حروف بزرگ/کوچک)، مقدار = شکل ثبت‌شده
    private static var words: [String: String] = {
        var dict: [String: String] = [:]
        for line in AppSupport.readLines(fileName) where dict[line.lowercased()] == nil {
            dict[line.lowercased()] = line
        }
        return dict
    }()

    static func all() -> [String] {
        lock.lock(); defer { lock.unlock() }
        return AppSupport.sorted(words.values)
    }

    static func isException(_ word: String) -> Bool {
        if word.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty { return false }
        lock.lock(); defer { lock.unlock() }
        return words[word.lowercased()] != nil
    }

    @discardableResult
    static func add(_ input: String) -> Bool {
        let word = input.trimmingCharacters(in: .whitespacesAndNewlines)
        if word.isEmpty { return false }

        lock.lock(); defer { lock.unlock() }
        if words[word.lowercased()] != nil { return false }
        words[word.lowercased()] = word
        AppSupport.writeLines(fileName, AppSupport.sorted(words.values))
        return true
    }

    @discardableResult
    static func remove(_ word: String) -> Bool {
        lock.lock(); defer { lock.unlock() }
        guard words.removeValue(forKey: word.lowercased()) != nil else { return false }
        AppSupport.writeLines(fileName, AppSupport.sorted(words.values))
        return true
    }
}
