import AppKit

/// log برای عیب‌یابی. پیش‌فرض خاموش است و فقط با انتخاب کاربر از منو فعال می‌شود
/// (چون با فعال بودنش متن تایپ‌شده هم ذخیره می‌شود).
enum AppLog {
    private static let lock = NSLock()
    private static let formatter: DateFormatter = {
        let f = DateFormatter()
        f.locale = Locale(identifier: "en_US_POSIX")
        f.dateFormat = "yyyy-MM-dd HH:mm:ss.SSS"
        return f
    }()

    static var isEnabled = false

    /// در self-test خروجی به‌جای فایل روی ترمینال چاپ می‌شود.
    static var printToStdout = false

    static let path: URL = FileManager.default
        .urls(for: .libraryDirectory, in: .userDomainMask)[0]
        .appendingPathComponent("Logs/TanbalType/TanbalType.log")

    static func write(_ message: @autoclosure () -> String) {
        if printToStdout {
            print(message())
            return
        }
        guard isEnabled else { return }

        let line = "\(formatter.string(from: Date())) \(message())\n"
        lock.lock()
        defer { lock.unlock() }
        do {
            try FileManager.default.createDirectory(
                at: path.deletingLastPathComponent(), withIntermediateDirectories: true)
            if let handle = try? FileHandle(forWritingTo: path) {
                defer { try? handle.close() }
                handle.seekToEndOfFile()
                handle.write(Data(line.utf8))
            } else {
                try Data(line.utf8).write(to: path)
            }
        } catch {
            // نوشتن log نباید برنامه را مختل کند
        }
    }

    static func writeHeader() {
        let info = ProcessInfo.processInfo
        write("=== TanbalType log started ===")
        write("Version: \(AppInfo.version), macOS \(info.operatingSystemVersionString)")
        write("Bundle: \(Bundle.main.bundlePath)")
    }

    static func open() {
        if FileManager.default.fileExists(atPath: path.path) {
            NSWorkspace.shared.open(path)
            return
        }
        Alerts.info("فایل log پیدا نشد.\n\nمسیر:\n\(path.path)\n\nابتدا گزینهٔ «ذخیره log» را فعال کنید.")
    }
}
