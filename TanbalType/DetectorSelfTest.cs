namespace TanbalType;

/// <summary>Sanity checks for bidirectional layout detection at startup.</summary>
internal static class DetectorSelfTest
{
    private static readonly string[] EnglishOnFaLayout =
        // «id» عمداً در لیست نیست: روی حالت فارسی «هی» می‌شود که خودش کلمهٔ فارسی معتبری است
        ["hello", "book", "world", "class", "please", "computer", "good", "test", "name", "google",
         "login", "dashboard", "database", "application", "fararu.com", "app", "api",
         // نام سایت‌ها و کلمات محاوره‌ای که روی حالت فارسی تایپ شده‌اند
         "digikala", "youtube", "instagram", "aparat", "www.digikala.com", "salam", "mamnoon",
         // انگلیسی که قبلاً به‌خاطر تداخل با لغت‌نامه اصلاح نمی‌شد (واژهٔ فارسیِ مسدودکننده حذف شد)
         "just", "load", "can", "did",
         "as","at","co","he","had","how","if","ill","in","ir","is","it","its","idk","jan",
         "gg","gif","git","gn","go","nah","ok","on","or","os","pc","pdf","she","so","tbh",
         "thx","to","ty","ui","us","ux","why","wp"];

    private static readonly string[] PersianOnEnLayout =
        ["sghl", "ldo,hil", "vh", "fhc", ";vnd", "ffdkd", "ldai", "kldai", "dh", "fh", "hc",
         // کلمات محاوره‌ای فارسی که روی حالت انگلیسی تایپ شده‌اند
         "]x,vd" /* چطوری */, "]ofv" /* چخبر */, "pgi" /* حله */, "nd'i" /* دیگه */,
         "jaodwa" /* تشخیصش */, ";jhfa" /* کتابش */,
         // واژه‌های «آ»دار با Shift+H تایپ می‌شوند (باگ گزارش‌شده: «آقا» اصلاح نمی‌شد)
         "Hrh" /* آقا */, "Hf" /* آب */, "Hln" /* آمد */, "Hnvs" /* آدرس */];

    private static readonly string[] PersianShouldNotFix =
        ["برنامه", "بعد", "خیلی", "به", "از", "نمایش", "اطلاعات", "مطالعه", "باید", "سلام", "که", "مدیر",
         "میشه", "نمیشه", "باشه", "دیگه", "حله", "مخم", "چاکرم", "اوکی", "قربونت", "دمتگرم",
         // کلمات لغت‌نامه‌ای با پسوند چسبان — نباید به انگلیسی تبدیل شوند
         "تشخیصش", "تشخیصشون", "برنامشون", "لپتاپم", "موبایلت", "پروژهام",
         // کلمات واقعی فارسی که با انگلیسی تداخل دارند ولی باید حفظ شوند
         "شد", "یخ", "هدف", "خب", "مخزن", "اثاث", "زهد", "مخل",
         // واژه‌های واقعی فارسی که با نام سایت تداخل داشتند و عمداً حفظ شدند
         "فر", "هل", "شخم", "نشمه", "مل", "نهش", "دهنش", "بش",
         "آقا", "آب", "آمد", "آدرس", "آموزش", "آقای",
         // دو واژهٔ فارسی که با نقطه به هم چسبیده‌اند (فاصله جا افتاده): شکلِ دامنه دارند
         // ولی آدرس نیستند. بی‌گاردِ IsDottedPersianText، «سلام.شب» می‌شد sghl.af
         "سلام.شب", "درست.من", "میشه.بعد"];

    // نام سایت‌های متداول که نباید روی حالت انگلیسی به فارسی تبدیل شوند
    private static readonly string[] SiteNamesShouldNotFix =
        ["tv", "ig", "fb", "tg", "lg", "hm", "vk", "cc", "hh", "aol", "dhl", "un",
         "kia", "audi", "dji", "kali", "raja", "avval",
         "digikala.com", "www.aparat.com", "divar.ir", "google.com",
         "fafa", // باگ گزارش‌شده در لاگ: fafa + Enter اشتباهاً «بشبش» می‌شد
         // دستورها و پسوندهایی که با واژه‌های نادر فارسی تداخل داشتند
         "cmd", "pwd", "ssh", "kill", "dll", "xls", "bin", "cfg", "ini", "big", "add", "avg", "off"];

    // اعداد و توکن‌های حاوی عدد (رمز عبور/کد/شماره) در هیچ حالتی نباید اصلاح شوند
    private static readonly string[] DigitTokensShouldNotFix =
        ["1234", "pass123", "admin2024", "user1", "sar12", "P@ss1",
         "۱۲۳۴", "رمز۱۲۳", "شسق۲۰۲۴"];

    // تکه‌هایی که کاربر در ادامهٔ متنِ قبلی می‌نویسد، آن‌جا که آغازِ کلمه معلوم نیست
    // (پنجره عوض شده، ماوس کلیک شده، برنامه تازه بالا آمده). «engineer» از قبل نوشته
    // شده و کاربر «ing» را به آن می‌چسباند: هیچ‌کدامِ این‌ها نباید اصلاح شوند.
    private static readonly string[] EnFragmentsShouldNotFix =
        ["ing", "ings", "ng", "ngs", "nd", "ed", "er", "ment", "tion", "ness", "ther",
         "nal", "tal", "ral", "ial", "ard", "ase", "oad", "kly", "tand", "bility"];

    private static readonly string[] FaFragmentsShouldNotFix =
        ["اث" /* احداث */, "ائره" /* دائره */, "ابخش" /* تسلابخش */, "اشقی" /* عاشقی */,
         "اشسته" /* ناشسته */, "ادشاه" /* پادشاه */, "اعده" /* قاعده */, "امعه" /* جامعه */,
         "تخاب" /* انتخاب */, "ایعه" /* شایعه */, "اصره" /* محاصره */];

    // کلمه‌های کاملی که با نامعلوم‌بودنِ آغازِ کلمه هم باید اصلاح شوند: کاربر روی
    // کادرِ خالی کلیک کرده و همین یک کلمه را نوشته است.
    private static readonly string[] UnanchoredShouldStillFix =
        ["sghl" /* سلام */, "o,f" /* خوب */, "lvsd" /* مرسی */, "]x,vd" /* چطوری */,
         "fvkhli" /* برنامه */, "nvsj" /* درست */, "h,;d" /* اوکی */, "ldo,hil" /* میخواهم */];

    private static readonly string[] EnglishShouldNotFix =
        ["performance", "analysis", "project", "experience", "authentication", "shopping", "slam",
         "book", "id", "app", "api",
         // نام سایت‌های ایرانی که در انگلیسی معنایی ندارند ولی نباید اصلاح شوند
         "digikala", "aparat", "divar", "snapp", "torob", "sheypoor", "varzesh3", "filimo",
         "cafebazaar", "zarinpal", "www.aparat.com", "youtube", "instagram", "telegram",
         // کلمات محاوره‌ای چت و فینگلیش
         "lol", "btw", "thx", "okay", "yeah", "gonna", "salam", "mamnoon", "chetori", "kheili", "azizam",
         // لغات پرکاربردی که تصویرِ فارسی‌شان واژهٔ فارسی نیست و اشتباهاً اصلاح می‌شدند
         // («hand» را کاربر گزارش کرد؛ بقیه با سویپ روی ۱۰٬۰۰۰ واژهٔ پربسامد پیدا شدند)
         "hand", "high", "highly", "child", "military", "kind", "hold", "hill", "fall", "fill",
         "gold", "art", "dvd", "usa", "low", "far", "loss", "tel", "tell", "kids", "band",
         "sand", "milk", "silk", "wild", "mild", "valid", "solid", "civil", "legal", "guard",
         "yard", "tall", "hull", "hell", "held", "told", "toll", "fold", "grid", "lan",
         "possibility", "probability", "stability", "flexibility", "roughly", "thoroughly",
         "dns", "msg", "ghz", "rand", "via", "aug", "fig", "gel", "lid", "cia", "hung",
         // سویپ روی کل فرهنگ املاییِ انگلیسی (hunspell en-US) — هر ۲۵ موردِ
         // ENGLISH_COLLISION_JUNK_2 این‌جا قفل می‌شوند تا دوباره فارسی نشوند
         "aid", "shy", "gut", "hut", "jug", "jot", "lag", "lax", "fad", "foe", "cog", "hid",
         "avid", "fond", "hail", "nail", "raid", "rail", "laid", "slid", "fail", "rival",
         "soda", "skid", "anvil"];

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

        foreach (var word in SiteNamesShouldNotFix)
        {
            if (Detector.DetectWrongLayout(word, currentLayoutIsPersian: false) is { } fixedTo)
            {
                AppLog.Write($"SelfTest site-name interference: {word} -> '{fixedTo}'");
                failed++;
            }
        }

        foreach (var word in DigitTokensShouldNotFix)
        {
            if (Detector.DetectWrongLayout(word, currentLayoutIsPersian: true) is not null
                || Detector.DetectWrongLayout(word, currentLayoutIsPersian: false) is not null)
            {
                AppLog.Write($"SelfTest digit false-positive: {word}");
                failed++;
            }
        }

        // ---- آغازِ کلمه نامعلوم است: فقط شواهدِ قطعی باید اصلاح شوند ----
        foreach (var frag in EnFragmentsShouldNotFix)
        {
            if (Detector.DetectWrongLayout(frag, currentLayoutIsPersian: false, wordStartKnown: false) is { } fixedTo)
            {
                AppLog.Write($"SelfTest unanchored EN fragment: {frag} -> '{fixedTo}'");
                failed++;
            }
        }

        foreach (var frag in FaFragmentsShouldNotFix)
        {
            if (Detector.DetectWrongLayout(frag, currentLayoutIsPersian: true, wordStartKnown: false) is { } fixedTo)
            {
                AppLog.Write($"SelfTest unanchored FA fragment: {frag} -> '{fixedTo}'");
                failed++;
            }
        }

        foreach (var keys in UnanchoredShouldStillFix)
        {
            var expected = Mapper.EnKeysToPersian(keys);
            var got = Detector.DetectWrongLayout(keys, currentLayoutIsPersian: false, wordStartKnown: false);
            if (got != expected)
            {
                AppLog.Write($"SelfTest unanchored word FAIL: {keys} -> '{got}' (expect {expected})");
                failed++;
            }
        }

        if (failed == 0)
            AppLog.Write("SelfTest OK — corrections + no false positives");
        else
            AppLog.Write($"SelfTest FAILED — {failed} case(s)");
    }
}
