namespace TanbalType;

/// <summary>Sanity checks for bidirectional layout detection at startup.</summary>
internal static class DetectorSelfTest
{
    private static readonly string[] EnglishOnFaLayout =
        ["hello", "book", "world", "class", "please", "computer", "good", "test", "name", "google",
         "login", "dashboard", "database", "application", "fararu.com", "app", "id", "api"];

    private static readonly string[] PersianOnEnLayout =
        ["sghl", "ldo,hil", "vh", "fhc", ";vnd", "ffdkd", "ldai", "kldai", "dh", "fh", "hc"];

    private static readonly string[] PersianShouldNotFix =
        ["برنامه", "بعد", "خیلی", "به", "از", "نمایش", "اطلاعات", "مطالعه", "باید", "سلام", "که", "مدیر", "میشه", "نمیشه", "باشه"];

    private static readonly string[] EnglishShouldNotFix =
        ["performance", "analysis", "project", "experience", "authentication", "shopping", "slam", "book", "id", "app", "api"];

    public static void RunAndLog()
    {
        var failed = 0;

        foreach (var word in EnglishOnFaLayout)
        {
            var onScreen = Mapper.EnKeysToPersian(word);
            var got = Detector.DetectWrongLayout(onScreen, currentLayoutIsPersian: true);
            if (got != word)
            {
                AppLog.Write($"SelfTest FA->EN FAIL: {onScreen} -> '{got}' (expect {word})");
                failed++;
            }
        }

        foreach (var keys in PersianOnEnLayout)
        {
            var expected = Mapper.EnKeysToPersian(keys);
            var got = Detector.DetectWrongLayout(keys, currentLayoutIsPersian: false);
            if (got != expected)
            {
                AppLog.Write($"SelfTest EN->FA FAIL: {keys} -> '{got}' (expect {expected})");
                failed++;
            }
        }

        foreach (var word in PersianShouldNotFix)
        {
            if (Detector.DetectWrongLayout(word, currentLayoutIsPersian: true) is not null)
            {
                AppLog.Write($"SelfTest FA false-positive: {word}");
                failed++;
            }
        }

        foreach (var word in EnglishShouldNotFix)
        {
            if (Detector.DetectWrongLayout(word, currentLayoutIsPersian: false) is not null)
            {
                AppLog.Write($"SelfTest EN false-positive: {word}");
                failed++;
            }
        }

        if (failed == 0)
            AppLog.Write("SelfTest OK — corrections + no false positives");
        else
            AppLog.Write($"SelfTest FAILED — {failed} case(s)");
    }
}