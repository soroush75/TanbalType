import AppKit

// اجرای آزمون تشخیص بدون رابط کاربری:
//   --selftest      نگاشت ویندوز + چیدمان استاندارد مک (ISIRI 2901)
//   --selftest-all  همهٔ چیدمان‌های فارسیِ نصب‌شده روی سیستم (فقط برای گزارش)
let arguments = CommandLine.arguments
if arguments.contains("--selftest") || arguments.contains("--selftest-all") {
    AppLog.printToStdout = true
    LayoutManager.refresh()
    let layouts = arguments.contains("--selftest-all")
        ? LayoutManager.installedPersianLayouts().map(LayoutManager.sourceID)
        : ["com.apple.keylayout.Persian-ISIRI2901"]
    exit(DetectorSelfTest.run(layoutIDs: layouts) == 0 ? 0 : 1)
}

let app = NSApplication.shared
let delegate = AppDelegate()
app.delegate = delegate
app.setActivationPolicy(.accessory)
app.run()
