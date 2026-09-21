import Foundation

/// تشخیص کلمه‌ای که با چیدمان اشتباه تایپ شده — پورت مستقیم Detector.cs نسخهٔ ویندوز.
/// فهرست‌های لغت از DetectorData (ساخته‌شده از سورس C#) خوانده می‌شوند.
enum Detector {
    private static let scoreMargin = 0.12
    private static let strongEnglishThreshold = 0.45
    private static let strongPersianThreshold = 0.35

    private static let domainSuffixes = DetectorData.domainSuffixes
    private static let siteNames = DetectorData.siteNames
    private static let englishWords = DetectorData.englishWords
    private static let persianAttachedSuffixes = DetectorData.persianAttachedSuffixes
    private static let commonTwoLetterPersian = DetectorData.commonTwoLetterPersian
    private static let persianMarkers = DetectorData.persianMarkers
    private static let englishClusters = DetectorData.englishClusters
    private static let persianBigrams: Set<String> = Set(DetectorData.persianBigramList)

    // لغت‌نامهٔ بزرگ فارسی از فایل منبع داخل bundle خوانده می‌شود
    static let persianWords: Set<String> = loadPersianWords()

    private static func loadPersianWords() -> Set<String> {
        var set = Set<String>(minimumCapacity: 400_000)

        if let url = resourceURL("PersianWords", "txt"),
           let text = try? String(contentsOf: url, encoding: .utf8) {
            text.enumerateLines { line, _ in
                if line.count >= 2 { set.insert(line) }
            }
        } else {
            AppLog.write("PersianWords.txt NOT FOUND — dictionary is empty")
        }

        for word in DetectorData.persianColloquial {
            set.insert(word)
        }
        return set
    }

    /// فایل منبع داخل bundle، یا کنار فایل اجرایی (برای اجرای self-test بدون bundle).
    private static func resourceURL(_ name: String, _ ext: String) -> URL? {
        if let url = Bundle.main.url(forResource: name, withExtension: ext) {
            return url
        }
        let exe = URL(fileURLWithPath: CommandLine.arguments[0]).resolvingSymlinksInPath()
        let candidate = exe.deletingLastPathComponent().appendingPathComponent("\(name).\(ext)")
        return FileManager.default.fileExists(atPath: candidate.path) ? candidate : nil
    }

    /// قدرتِ تطبیق یک کلمه با لغت‌نامهٔ فارسی.
    private enum DictMatch {
        case none
        case exact   // خودِ کلمه در لغت‌نامه است — شواهد قوی
        case strong  // ریشهٔ ۴+ حرفی یا پسوند چندحرفی
        case weak    // ریشهٔ کوتاه (۲-۳ حرفی) + پسوند تک‌حرفی — شواهد ضعیف
    }

    /// جستجوی کلمه در لغت‌نامه، مستقیم یا با جداکردن پسوند چسبان (مثل «تشخیصش»، «کتابم»، «خونمون»)
    private static func lookupPersian(_ word: String) -> DictMatch {
        if persianWords.contains(word) {
            return .exact
        }

        let length = word.count
        for suffix in persianAttachedSuffixes {
            let suffixLength = suffix.count
            // برای پسوندهای تک‌حرفی، ریشهٔ حداقل ۳ حرفی لازم است
            let minStem = suffixLength == 1 ? 3 : 2
            if length < suffixLength + minStem { continue }
            if !word.hasSuffix(suffix) { continue }

            let stem = String(word.dropLast(suffixLength))
            if !persianWords.contains(stem) { continue }

            return suffixLength == 1 && stem.count <= 3 ? .weak : .strong
        }

        return .none
    }

    private static func isKnownPersianWord(_ word: String) -> Bool {
        lookupPersian(word) != .none
    }

    private static func isEnglishWord(_ word: String) -> Bool {
        englishWords.contains(word.lowercased())
    }

    static func detectWrongLayout(_ input: String, currentLayoutIsPersian: Bool) -> String? {
        let word = input.trimmingCharacters(in: .whitespacesAndNewlines)
        if word.count < 2 {
            return nil
        }

        // کلمات استثنایی که کاربر خودش اضافه کرده هرگز اصلاح نمی‌شوند
        if UserExceptions.isException(word) {
            return nil
        }

        // لغاتی که کاربر خودش ثبت کرده بر بقیهٔ قاعده‌ها (از جمله محدودیتِ اعداد) اولویت دارند
        switch resolveUserWord(word, currentLayoutIsPersian: currentLayoutIsPersian) {
        case .keep: return nil
        case .replace(let canonical): return canonical
        case .unknown: break
        }

        // اعداد را هرگز اصلاح نمی‌کنیم: رمز عبور، کد، شماره، سال، نسخه و ...
        if word.contains(where: isDigit) {
            return nil
        }

        if currentLayoutIsPersian {
            return detectEnglishIntendedOnPersianLayout(word)
        }
        return detectPersianIntendedOnEnglishLayout(word)
    }

    /// معادل char.IsDigit در .NET: هر رقم دهدهی یونیکد (از جمله ارقام فارسی و عربی).
    private static func isDigit(_ ch: Character) -> Bool {
        ch.unicodeScalars.contains { $0.properties.generalCategory == .decimalNumber }
    }

    private enum UserWordResolution {
        case unknown          // در فهرست کاربر نیست؛ قاعده‌های حدسی اجرا شوند
        case keep             // خودِ کلمه لغت کاربر است، پس درست تایپ شده
        case replace(String)  // شکلِ اشتباهِ یک لغت کاربر است
    }

    /// فهرستِ لغاتِ شخصیِ کاربر: اگر کلمه همان چیزی باشد که کاربر ثبت کرده دست‌نخورده می‌ماند،
    /// و اگر شکلِ تایپ‌شده با چیدمانِ اشتباهِ آن باشد، دقیقاً به شکلِ ثبت‌شده اصلاح می‌شود.
    private static func resolveUserWord(_ word: String, currentLayoutIsPersian: Bool) -> UserWordResolution {
        if UserWords.isUserWord(word) {
            return .keep
        }

        let mapped = currentLayoutIsPersian
            ? Mapper.persianToEnKeys(word)
            : Mapper.enKeysToPersian(word)

        guard let canonical = UserWords.canonical(for: mapped) else {
            return .unknown
        }
        return .replace(canonical)
    }

    private static func detectEnglishIntendedOnPersianLayout(_ word: String) -> String? {
        // کلمهٔ لغت‌نامه‌ای یا کلمه‌ای با پسوند چسبان — فارسیِ عمدی است
        if isKnownPersianWord(word) {
            return nil
        }

        let mappedEn = Mapper.persianToEnKeys(word)

        if UserExceptions.isException(mappedEn) {
            return nil
        }

        // اگر روی حالت فارسی آدرس یا نام سایت تایپ شده باشد، به انگلیسی برمی‌گردد
        if shouldConvertToWebAddress(mappedEn) {
            return mappedEn
        }

        if isEnglishWord(mappedEn) {
            return mappedEn
        }

        let length = Double(word.count)
        if Double(Mapper.countPersian(word)) >= length * 0.75 {
            let faScore = scoreIntentionalPersian(word)
            if faScore >= strongPersianThreshold {
                return nil
            }

            let enScore = scoreEnglishOnPersianKeys(word, candidate: mappedEn)
            if enScore >= 0.28 && enScore > faScore + scoreMargin {
                return mappedEn
            }
            return nil
        }

        if mappableEnRatio(word) >= 0.75 {
            let englishScore = scoreIntentionalEnglish(word)
            let persianOnKeys = scorePersianOnEnglishKeys(word)
            if englishScore >= 0.28 && englishScore > persianOnKeys + scoreMargin {
                return word
            }
        }

        return nil
    }

    private static func detectPersianIntendedOnEnglishLayout(_ word: String) -> String? {
        // کاربر در حال وارد کردن آدرس یا نام سایت است — نباید به فارسی تبدیل شود
        if isProbablyWebInput(word) {
            return nil
        }

        if isEnglishCommaList(word) {
            return nil
        }

        if isEnglishWord(word) {
            return nil
        }

        let mappedFa = Mapper.enKeysToPersian(word)

        if UserExceptions.isException(mappedFa) {
            return nil
        }

        var match = lookupPersian(mappedFa)
        let englishScore = scoreIntentionalEnglish(word)

        // توکن دوحرفی: فقط واژه‌های پرکاربرد از روی لغت‌نامه اصلاح می‌شوند
        if mappedFa.count == 2 && !commonTwoLetterPersian.contains(mappedFa) {
            match = .none
        }

        // تطبیقِ ضعیف نباید بر شواهدِ قویِ انگلیسی غلبه کند
        if match == .weak && englishScore >= strongEnglishThreshold {
            match = .none
        }

        if match != .none {
            return mappedFa
        }

        if englishScore >= strongEnglishThreshold {
            return nil
        }

        let persianScore = scorePersianOnEnglishKeys(word)
        if persianScore >= 0.22 && persianScore > englishScore + scoreMargin {
            return mappedFa
        }

        return nil
    }

    /// آیا کاربر روی حالت انگلیسی مشغول تایپ آدرس/ایمیل/نام سایت است؟
    private static func isProbablyWebInput(_ word: String) -> Bool {
        if word.contains("://") {
            return true
        }
        if word.lowercased().hasPrefix("www.") {
            return true
        }
        if word.contains("@") && looksLikeEmailOrDomain(word) {
            return true
        }
        if hasDomainSuffix(word) {
            return true
        }
        return isKnownSiteName(word)
    }

    /// آیا متن نگاشت‌شده از حالت فارسی، یک آدرس/نام سایت واقعی است؟ (سخت‌گیری زیاد)
    private static func shouldConvertToWebAddress(_ mappedEn: String) -> Bool {
        if isKnownSiteName(mappedEn) {
            return true
        }
        if !looksLikeEmailOrDomain(mappedEn) {
            return false
        }
        if mappedEn.lowercased().hasPrefix("www.") && mappedEn.count >= 8 {
            return true
        }
        return hasDomainSuffix(mappedEn) && Mapper.countAsciiLetters(mappedEn) >= 4
    }

    private static func hasDomainSuffix(_ text: String) -> Bool {
        let lower = text.lowercased()
        return domainSuffixes.contains { lower.count > $0.count && lower.hasSuffix($0) }
    }

    /// نام سایت با یا بدون www. و پسوند دامنه (مثل digikala یا www.digikala.com)
    private static func isKnownSiteName(_ ascii: String) -> Bool {
        var core = ascii.lowercased()

        if core.hasPrefix("www.") {
            core = String(core.dropFirst(4))
        }

        for suffix in domainSuffixes where core.count > suffix.count && core.hasSuffix(suffix) {
            core = String(core.dropLast(suffix.count))
            break
        }

        return core.count >= 3 && siteNames.contains(core)
    }

    private static func isEnglishCommaList(_ word: String) -> Bool {
        if !word.contains(where: { Mapper.persianPunctAscii.contains($0) }) {
            return false
        }
        let parts = word.replacingOccurrences(of: ";", with: ",")
            .split(separator: ",", omittingEmptySubsequences: true)
        return parts.count >= 2 && parts.allSatisfy { isEnglishWord(String($0)) }
    }

    private static func mappableEnRatio(_ text: String) -> Double {
        text.isEmpty ? 0 : Double(Mapper.countEnLayoutKeys(text)) / Double(text.count)
    }

    private static func mappableFaRatio(_ text: String) -> Double {
        if text.isEmpty { return 0 }
        let converted = Mapper.persianToEnKeys(text)
        let changed = zip(text, converted).filter { $0 != $1 }.count
        return Double(changed) / Double(text.count)
    }

    private static func persianBigramScore(_ text: String) -> Double {
        let chars = Array(text)
        if chars.count < 2 { return 0 }
        var hits = 0
        for i in 0..<(chars.count - 1) where persianBigrams.contains(String([chars[i], chars[i + 1]])) {
            hits += 1
        }
        return Double(hits) / Double(chars.count - 1)
    }

    private static func hasPersianMarker(_ text: String) -> Bool {
        persianMarkers.contains { text.contains($0) }
    }

    private static func englishClusterScore(_ word: String) -> Double {
        let lower = word.lowercased()
        return englishClusters.contains { lower.contains($0) } ? 0.55 : 0
    }

    private static func lowerLetters(_ word: String) -> [Character] {
        word.filter(\.isLetter).map { Character($0.lowercased()) }
    }

    private static func isVowel(_ ch: Character) -> Bool {
        "aeiou".contains(ch)
    }

    private static func englishVowelScore(_ word: String) -> Double {
        let letters = lowerLetters(word)
        if letters.count < 2 { return 0 }

        let vowels = letters.filter(isVowel).count
        if vowels == 0 { return 0 }

        let ratio = Double(vowels) / Double(letters.count)
        if letters.count >= 4 && ratio >= 0.38 { return 0.65 }
        if ratio >= 0.30 { return 0.35 }
        return 0.22
    }

    private static func scoreLikelyEnglish(_ candidate: String) -> Double {
        if isEnglishWord(candidate) || isKnownSiteName(candidate) {
            return 1.0
        }

        if candidate.contains(where: { Mapper.persianPunctAscii.contains($0) }) {
            return 0
        }

        let letters = Mapper.countAsciiLetters(candidate)
        if Double(letters) < Double(candidate.count) * 0.85 {
            return 0
        }
        if letters <= 3 {
            return 0
        }
        if !hasReasonableEnglishVowelRatio(candidate) {
            return 0
        }
        if looksLikeGibberishAscii(candidate) {
            return 0
        }
        if looksLikeEmailOrDomain(candidate) {
            return max(englishVowelScore(candidate), 0.35)
        }

        let score = englishClusterScore(candidate) + englishVowelScore(candidate)
        return score >= 0.25 ? min(score, 1.0) : 0
    }

    private static func hasReasonableEnglishVowelRatio(_ word: String) -> Bool {
        let letters = lowerLetters(word)
        if letters.isEmpty { return false }
        let vowels = letters.filter(isVowel).count
        return vowels > 0 && Double(vowels) / Double(letters.count) >= 0.20
    }

    private static func looksLikeGibberishAscii(_ word: String) -> Bool {
        if hasLongConsonantRun(word) {
            return true
        }

        let letters = lowerLetters(word)
        if letters.count >= 5 {
            let vowels = letters.filter(isVowel).count
            if Double(vowels) / Double(letters.count) < 0.18 {
                return true
            }
        }
        return false
    }

    private static func hasLongConsonantRun(_ word: String) -> Bool {
        var run = 0
        for ch in word.lowercased() {
            if ch.isLetter && !isVowel(ch) {
                run += 1
                if run >= 5 { return true }
            } else {
                run = 0
            }
        }
        return false
    }

    private static func scoreIntentionalEnglish(_ word: String) -> Double {
        if isEnglishWord(word) || isKnownSiteName(word) {
            return 1.0
        }
        if word.contains(where: { Mapper.persianPunctAscii.contains($0) }) {
            return 0
        }
        return min(englishClusterScore(word) + englishVowelScore(word), 1.0)
    }

    private static func scorePersianOnEnglishKeys(_ word: String) -> Double {
        if mappableEnRatio(word) < 0.75 { return 0 }
        let converted = Mapper.enKeysToPersian(word)
        if Double(Mapper.countPersian(converted)) < Double(converted.count) * 0.85 { return 0 }

        let bigram = persianBigramScore(converted)
        let marker = hasPersianMarker(converted)
        var score = bigram * 0.55
        if marker { score += 0.42 }

        if word.contains(where: { Mapper.persianPunctAscii.contains($0) }) {
            score += 0.18
        }

        if word.count <= 3 && !marker && bigram < 0.45 {
            return 0
        }

        return min(score, 1.0)
    }

    private static func scoreIntentionalPersian(_ word: String) -> Double {
        if isKnownPersianWord(word) {
            return 1.0
        }
        if Double(Mapper.countPersian(word)) < Double(word.count) * 0.85 { return 0 }

        var score = persianBigramScore(word) * 0.5
        if hasPersianMarker(word) { score += 0.45 }
        if word.count >= 4 { score += 0.12 }
        return min(score, 1.0)
    }

    private static func scoreEnglishOnPersianKeys(_ word: String, candidate: String) -> Double {
        if Double(Mapper.countPersian(word)) < Double(word.count) * 0.75 { return 0 }
        if mappableFaRatio(word) < 0.75 { return 0 }

        if !candidate.allSatisfy(\.isASCII) || Mapper.countPersian(candidate) > 0 { return 0 }
        if !hasEnoughAsciiLetters(candidate) { return 0 }

        return scoreLikelyEnglish(candidate)
    }

    private static func hasEnoughAsciiLetters(_ candidate: String) -> Bool {
        let letterCount = Mapper.countAsciiLetters(candidate)
        if Double(letterCount) >= Double(candidate.count) * 0.85 {
            return true
        }
        return looksLikeEmailOrDomain(candidate)
    }

    private static func looksLikeEmailOrDomain(_ text: String) -> Bool {
        if Mapper.countAsciiLetters(text) < 3 {
            return false
        }
        return text.allSatisfy { ch in
            (ch.isASCII && (ch.isLetter || ch.isNumber)) || ch == "." || ch == "@" || ch == "-" || ch == "_"
        }
    }
}
