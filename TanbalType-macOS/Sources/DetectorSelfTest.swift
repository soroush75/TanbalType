import Foundation

/// آزمون درستیِ تشخیص دوطرفه (پورت DetectorSelfTest.cs). داده‌ها از سورس C# تولید می‌شوند.
///
/// کلیدهای PersianOnEnLayout برای چیدمان ویندوز نوشته شده‌اند؛ برای چیدمان‌های مک، خودِ واژهٔ
/// فارسیِ مورد انتظار نگه داشته می‌شود و کلیدهایش از روی چیدمانِ آزمون‌شده دوباره ساخته می‌شود.
enum DetectorSelfTest {
    /// خروجی: تعداد موارد ناموفق.
    ///
    /// چیدمان‌های Persian-QWERTY (آوایی) و Persian (قدیمی) عمداً جزو آزمون پیش‌فرض نیستند: روی آن‌ها
    /// بسیاری از کلمات کوتاه انگلیسی خودشان واژهٔ فارسیِ معتبرند (مثلاً salam → «سالام»).
    @discardableResult
    static func run(layoutIDs: [String]) -> Int {
        let current = Mapper.sourceID
        var failed = 0

        Mapper.useWindowsLayout()
        let persianTargets = DetectorData.persianOnEnLayout.map(Mapper.enKeysToPersian)

        // نگاشت ویندوز همیشه آزموده می‌شود؛ به‌علاوهٔ چیدمان‌های مکِ خواسته‌شده
        var layouts: [(String, () -> Void)] = [("windows", { Mapper.useWindowsLayout() })]
        for id in layoutIDs where id != "windows" {
            guard let source = LayoutManager.source(withID: id) else { continue }
            layouts.append((id, { Mapper.configure(from: source) }))
        }

        for (name, activate) in layouts {
            activate()
            let before = failed
            failed += runCases(persianTargets: persianTargets)
            AppLog.write("SelfTest [\(name)]: \(failed - before) failure(s)")
        }

        if let source = LayoutManager.source(withID: current) {
            Mapper.configure(from: source)
        } else {
            Mapper.useWindowsLayout()
        }

        AppLog.write(failed == 0
            ? "SelfTest OK — corrections + no false positives"
            : "SelfTest FAILED — \(failed) case(s)")
        return failed
    }

    private static func runCases(persianTargets: [String]) -> Int {
        var failed = 0

        for word in DetectorData.englishOnFaLayout {
            let onScreen = Mapper.enKeysToPersian(word)
            let got = Detector.detectWrongLayout(onScreen, currentLayoutIsPersian: true)
            if got != word {
                AppLog.write("SelfTest FA->EN FAIL: \(onScreen) -> '\(got ?? "nil")' (expect \(word))")
                failed += 1
            }
        }

        for expected in persianTargets {
            let keys = Mapper.persianToEnKeys(expected)
            let got = Detector.detectWrongLayout(keys, currentLayoutIsPersian: false)
            if got != expected {
                AppLog.write("SelfTest EN->FA FAIL: \(keys) -> '\(got ?? "nil")' (expect \(expected))")
                failed += 1
            }
        }

        for word in DetectorData.persianShouldNotFix
        where Detector.detectWrongLayout(word, currentLayoutIsPersian: true) != nil {
            AppLog.write("SelfTest FA false-positive: \(word)")
            failed += 1
        }

        for word in DetectorData.englishShouldNotFix
        where Detector.detectWrongLayout(word, currentLayoutIsPersian: false) != nil {
            AppLog.write("SelfTest EN false-positive: \(word)")
            failed += 1
        }

        for word in DetectorData.siteNamesShouldNotFix {
            if let fixed = Detector.detectWrongLayout(word, currentLayoutIsPersian: false) {
                AppLog.write("SelfTest site-name interference: \(word) -> '\(fixed)'")
                failed += 1
            }
        }

        for word in DetectorData.digitTokensShouldNotFix
        where Detector.detectWrongLayout(word, currentLayoutIsPersian: true) != nil
            || Detector.detectWrongLayout(word, currentLayoutIsPersian: false) != nil {
            AppLog.write("SelfTest digit false-positive: \(word)")
            failed += 1
        }

        return failed
    }
}
