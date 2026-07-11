namespace TanbalType;

public static class Detector
{
    private const double ScoreMargin = 0.12;
    private const double StrongEnglishThreshold = 0.45;
    private const double StrongPersianThreshold = 0.35;

    // پسوندهای دامنه برای شناسایی آدرس سایت
    private static readonly string[] DomainSuffixes =
    [
        ".ir", ".com", ".org", ".net", ".io", ".co", ".edu", ".gov", ".info",
        ".biz", ".me", ".dev", ".app", ".ai", ".tv", ".shop", ".site",
        ".online", ".store", ".xyz", ".pro", ".cc", ".link"
    ];

    // نام سایت‌ها و اپلیکیشن‌هایی که با حروف انگلیسی تایپ می‌شوند ولی کلمهٔ انگلیسی نیستند
    // (نام‌های فارسی مثل digikala در انگلیسی معنایی ندارند و نباید «اصلاح» شوند)
    private static readonly HashSet<string> SiteNames = new(StringComparer.OrdinalIgnoreCase)
    {
        // سرویس‌های ایرانی
        "digikala", "aparat", "divar", "sheypoor", "snapp", "snappfood",
        "snapptrip", "tapsi", "torob", "emalls", "filimo", "namava",
        "telewebion", "rubika", "eitaa", "bale", "soroush", "igap", "shad",
        "zoomit", "digiato", "zoomg", "vigiato", "technolife", "varzesh3",
        "footballi", "tarafdari", "fararu", "tabnak", "asriran",
        "khabaronline", "tasnim", "isna", "irna", "mehrnews", "farsnews",
        "hamshahri", "zarinpal", "idpay", "zibal", "nextpay", "jibit",
        "shaparak", "cafebazaar", "bazaar", "myket", "sibapp", "alibaba",
        "flytoday", "mrbilit", "alopeyk", "tipax", "achareh", "okala",
        "khanoumi", "banimode", "modiseh", "digistyle", "bama", "khodro45",
        "hamrahtel", "iranketab", "taaghche", "fidibo", "navaar", "basalam",
        "esam", "digipay", "azki", "bimito", "irancell", "hamrahaval",
        "rightel", "shatel", "asiatech", "parsonline", "mobinnet",
        "arvancloud", "arvan", "parspack", "iranserver", "quera",
        "maktabkhooneh", "faradars", "roocket", "toplearn", "sabzlearn",
        "virgool", "jobinja", "jobvision", "ponisha", "parscoders",
        "karlancer", "tgju", "tsetmc", "codal", "mofid", "agah", "sahamyab",
        "rahavard", "sanjesh", "adliran", "mellat", "melli", "saderat",
        "tejarat", "pasargad", "parsian", "saman", "resalat", "ayandeh",
        "sepah", "maskan", "keshavarzi",
        // سرویس‌های جهانی
        "google", "gmail", "youtube", "instagram", "telegram", "whatsapp",
        "twitter", "facebook", "tiktok", "linkedin", "pinterest", "reddit",
        "discord", "twitch", "spotify", "soundcloud", "netflix", "amazon",
        "ebay", "aliexpress", "wikipedia", "bing", "yahoo", "outlook",
        "hotmail", "microsoft", "apple", "icloud", "github", "gitlab",
        "stackoverflow", "medium", "quora", "chatgpt", "openai", "claude",
        "anthropic", "gemini", "deepseek", "copilot", "steam", "epicgames",
        "playstation", "xbox", "nvidia", "intel", "amd", "samsung",
        "xiaomi", "huawei", "sony", "canva", "figma", "notion", "trello",
        "slack", "zoom", "skype", "viber", "signal", "snapchat", "threads",
        "booking", "airbnb", "uber", "paypal", "binance", "coinbase",
        "tradingview", "duolingo", "coursera", "udemy", "mozilla",
        "firefox", "chrome", "opera", "brave", "cloudflare", "vercel",
        "netlify", "wordpress", "blogger", "tumblr", "imdb", "goodreads",
        "behance", "dribbble", "duckduckgo", "protonmail", "proton",
        "crunchyroll", "wattpad"
    };

    private static readonly HashSet<string> EnglishWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "as", "at", "be", "by", "do", "go", "he", "hi", "if", "in", "is", "it","vs",
        "me", "my", "no", "of", "on", "or", "so", "to", "up", "us", "we", "the", "and", "for",
        "are", "but", "not", "you", "all", "can", "had", "her", "was", "one", "our", "out",
        "day", "get", "has", "him", "his", "how", "its", "may", "new", "now", "old", "see",
        "two", "way", "who", "boy", "did", "let", "put", "say", "she", "too", "use", "hello",
        "world", "test", "code", "file", "data", "name", "type", "text", "mail", "user",
        "pass", "login", "admin", "server", "client", "python", "mac", "apple", "google",
        "github", "def", "class", "import", "return", "print", "async", "await", "true",
        "false", "null", "int", "float", "bool", "list", "dict", "str", "len", "range",
        "while", "break", "continue", "function", "var", "const", "static", "void",
        "public", "private", "string", "char", "byte", "short", "long", "double", "switch",
        "case", "default", "try", "catch", "throw", "finally", "package", "interface",
        "abstract", "extends", "implements", "this", "super", "enum", "struct", "typedef",
        "include", "define", "ifdef", "endif", "main", "stdio", "stdlib", "malloc", "free",
        "sizeof", "printf", "scanf", "cout", "cin", "vector", "map", "set", "queue", "stack",
        "array", "object", "module", "export", "require", "console", "log", "debug", "error",
        "warn", "info", "trace", "document", "window", "script", "style", "html", "head",
        "body", "div", "span", "table", "form", "input", "button", "click", "submit", "http",
        "https", "url", "api", "json", "xml", "css", "sql", "select", "insert", "update",
        "delete", "where", "from", "join", "index", "key", "value", "config", "settings",
        "search", "email", "password", "username", "docker", "react", "node", "java", "rust",
        "swift", "linux", "windows", "spring", "nginx", "redis", "mongo", "mysql", "postgres",
        "git", "push", "pull", "merge", "branch", "commit", "clone", "fetch", "remote",
        "local", "global", "left", "right", "inner", "outer", "full", "group", "order",
        "having", "limit", "offset", "union", "view", "grant", "drop", "alter", "create",
        "column", "row", "schema", "database", "query", "cursor", "buffer", "stream",
        "socket", "thread", "process", "memory", "cache", "mutex", "lock", "sync", "read",
        "write", "open", "close", "save", "load", "start", "stop", "run", "exec", "system",
        "path", "dir", "mkdir", "chmod", "grep", "find", "make", "build", "install", "npm",
        "pip", "yarn", "cargo", "gradle", "maven", "spec", "mock", "stub", "assert", "expect",
        "describe", "before", "after", "each", "with", "without", "about", "above", "below",
        "under", "over", "into", "through", "during", "between", "among", "within", "some",
        "any", "many", "much", "more", "most", "less", "least", "very", "just", "only",
        "also", "even", "still", "already", "always", "never", "often", "sometimes", "here",
        "there", "when", "what", "which", "who", "whom", "whose", "why", "company", "business",
        "market", "product", "service", "customer", "project", "manager", "developer",
        "engineer", "design", "monday", "tuesday", "wednesday", "january", "february", "march",
        "april", "summer", "winter", "autumn", "book", "performance", "analysis", "machine",
        "learning", "shopping", "mobile", "desktop", "application", "authentication", "security",
        "experience", "content", "message", "sample", "release", "feature", "report",
        "generate", "restore", "validate", "dashboard", "profile", "meeting", "english",
        "slam", "box", "cart", "phone", "number", "crash", "team", "log",
        "id", "os", "ui", "ux", "db", "app", "pc", "mac", "web", "net", "io", "ai", "pr", "ok",
        "cup", "shop", "cafe", "coffee", "tea", "bug", "fix", "dev", "sys", "win", "ios", "apk",
        "js", "php", "pdf", "doc", "txt", "zip", "rar", "png", "jpg", "gif", "usb", "ram", "cpu", "ip",
        "ir", "com", "org", "www", "co", "edu", "gov", "info", "biz", "me", "html", "css",

        // --- کلمات محاوره‌ای و مخفف‌های رایج چت انگلیسی ---
        "lol", "lmao", "rofl", "omg", "btw", "brb", "idk", "idc", "imo", "imho",
        "fyi", "asap", "aka", "etc", "thx", "tnx", "ty", "np", "pls", "plz",
        "okay", "okey", "yeah", "yep", "yup", "nope", "nah", "hey", "yo", "sup",
        "wow", "hmm", "haha", "hehe", "huh", "oops", "bye", "goodbye", "gn", "gm",
        "gg", "wp", "ez", "afk", "dm", "tbh", "smh", "ikr", "rip", "meh",
        "bro", "dude", "sis", "fam", "buddy", "mate", "man", "guys",
        "gonna", "wanna", "gotta", "kinda", "sorta", "lemme", "gimme", "dunno",
        "im", "ive", "ill", "dont", "cant", "wont", "didnt", "isnt", "aint",
        "youre", "thats", "whats", "hows",
        "thanks", "thank", "please", "sorry", "sure", "fine", "cool", "nice",
        "great", "awesome", "perfect", "exactly", "really", "maybe", "welcome",
        "good", "yes", "love", "life", "miss", "soon", "later", "tonight",
        "today", "tomorrow", "yesterday", "morning", "night", "weekend",
        "friend", "family", "home", "send", "sent", "done", "wait", "call",
        "chat", "online", "offline", "busy", "happy", "birthday", "congrats",

        // --- فینگلیش رایج (فارسی با حروف انگلیسی) ---
        "salam", "salaam", "mamnoon", "mamnun", "merci", "mersi", "chetori",
        "chetor", "khobi", "khoobi", "khubi", "khob", "khoob", "khube", "khobe",
        "aziz", "azizam", "jan", "joon", "jaan", "joonam", "janam", "jigar",
        "dadash", "abji", "baba", "areh", "chera", "chie", "chiye",
        "koja", "kojaei", "kojaee", "chikar", "alan", "badan", "farda",
        "emrooz", "emruz", "emshab", "dishab", "sobh", "shab", "bekheir",
        "bekhair", "khodafez", "khodahafez", "felan", "ghorbanet", "ghorbunet",
        "fadat", "damet", "garm", "eyval", "eyvallah", "bikhial", "velesh",
        "bezar", "bede", "begoo", "bia", "boro", "berim", "bashe", "chashm",
        "kheili", "kheyli", "aali", "daram", "dooset", "doset", "mokhles",
        "chakeram", "dorood", "dorud", "khaste", "nabashi", "nabashid", "mrc"
    };

    // کلمات محاوره‌ای فارسی که در فایل لغت‌نامه نیستند ولی در تایپ روزمره زیاد استفاده می‌شوند
    // (هم از اصلاحِ اشتباهی محافظت می‌شوند، هم اگر روی حالت انگلیسی تایپ شوند اصلاح می‌شوند)
    private static readonly string[] PersianColloquial =
    [
        "مخم", "چخبر", "اصن", "بخدا", "والا", "ینی", "چیشد", "چیشده",
        "داداچ", "آبجی", "فدات", "قربونت", "دمت", "دمتگرم", "ایول", "باحال",
        "خفن", "بیخیال", "ولش", "بذار", "بزار", "حله", "جانم", "جونم",
        "عزیزم", "گلم", "چاکرم", "مخلصم", "نوکرتم", "سلامتی", "خدافظ",
        "فعلا", "بریم", "پاشو", "بشین", "وایسا", "میدونم", "نمیدونم",
        "میتونم", "نمیتونم", "میخوام", "نمیخوام", "میگم", "میگی", "میگه",
        "میگن", "میام", "میای", "میاد", "نمیام", "بگو", "بگم", "بگی",
        "چطوری", "چطور", "خوبی", "خوبم", "مرسی", "اوکی", "اوکیه", "اره",
        "آره", "نوچ", "چیه", "کیه", "کجایی", "هیچی", "الان", "بعدا",
        "دیگه", "همینه", "امشب", "دیشب", "بخیر", "شبت", "صبحت",
        "تولدت", "مبارک", "ناهار", "شام", "صبونه", "گشنمه", "تشنمه",
        "خستم", "خوابم", "بیدارم", "رسیدم", "رسیدی", "کجاست", "اینجا",
        "اونجا", "اونم", "اینم", "اینو", "اونو", "چقد", "چقدر", "انقد",
        "انقدر", "یکم", "یذره", "بازم", "هنو", "هنوز", "تازه", "زودباش",
        "بدو", "یالا", "بجنب", "داغونم", "عالیه", "قشنگه", "زشته", "گرونه",
        "ارزونه", "شوخی", "جدی", "واقعا", "دروغ", "راستی", "ضایع"
    ];

    // لغت‌نامهٔ بزرگ فارسی از فایل منبع تعبیه‌شده خوانده می‌شود
    // (به‌جای قرار دادن صدها هزار کلمه داخل کد که کامپایل و اجرای برنامه را به‌شدت کند می‌کرد)
    private static readonly HashSet<string> PersianWords = LoadPersianWords();

    private static HashSet<string> LoadPersianWords()
    {
        var set = new HashSet<string>(400_000, StringComparer.Ordinal);

        using var stream = typeof(Detector).Assembly
            .GetManifestResourceStream("TanbalType.PersianWords.txt");
        if (stream is not null)
        {
            using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
            while (reader.ReadLine() is { } line)
            {
                if (line.Length >= 2)
                    set.Add(line);
            }
        }

        foreach (var word in PersianColloquial)
            set.Add(word);

        return set;
    }

    // پسوندهای چسبان فارسی (ضمایر ملکی، جمع، صفت تفضیلی و ...) — از بلند به کوتاه
    private static readonly string[] PersianAttachedSuffixes =
    [
        "هایشان", "هایتان", "هایمان", "هاشون", "هاتون", "هامون",
        "هایش", "هایت", "هایم", "هایی", "هاش", "هات", "هام", "های",
        "شان", "تان", "مان", "شون", "تون", "مون", "ترین", "تری",
        "ها", "تر", "اش", "ات", "ام",
        "ش", "ت", "م", "ی", "و", "ه"
    ];

    /// <summary>کلمهٔ موجود در لغت‌نامهٔ فارسی، یا کلمهٔ لغت‌نامه‌ای با پسوند چسبان (مثل «تشخیصش»، «کتابم»، «خونمون»)</summary>
    private static bool IsKnownPersianWord(string word)
    {
        if (PersianWords.Contains(word))
            return true;

        foreach (var suffix in PersianAttachedSuffixes)
        {
            // برای پسوندهای تک‌حرفی، ریشهٔ حداقل ۳ حرفی لازم است تا با کلمات کوتاه انگلیسی
            // (مثل api که روی حالت فارسی «شحه» می‌شود) تداخل نکند
            var minStem = suffix.Length == 1 ? 3 : 2;
            if (word.Length < suffix.Length + minStem)
                continue;
            if (!word.EndsWith(suffix, StringComparison.Ordinal))
                continue;
            if (PersianWords.Contains(word[..^suffix.Length]))
                return true;
        }

        return false;
    }

    private static readonly HashSet<string> PersianMarkers =
    [
        "سلام", "لام", "های", "خواه", "خواهم", "کتاب", "خوب", "دوست", "برنامه",
        "مند", "باش", "کرد", "گفت", "نیست", "چرا", "مرسی", "ممن", "لطف",
        "خدا", "خیلی", "همین", "امرو", "دیرو", "فردا", "الان", "قبل", "وقت",
        "چیز", "جایی", "همه", "شما", "برای", "اما", "ولی", "چون", "ترین",
        "دانش", "مدرس", "خانه", "باید", "شاید", "حتما", "تشکر", "لطفا", "ممنون",
        "میخوام", "اطلاع", "نمایش", "دانلود", "کاربر", "مدیر", "نتیجه", "مطالعه",
        "میشه", "نمیشه", "باشه",
        "چطور", "دیگه", "بخیر", "داداش", "عزیز", "قربون", "ایول", "خدافظ",
        "بریم", "میدون", "میتون", "میخوا"
    ];

    // فقط جفت‌های دوحرفی؛ برای جستجوی بدون تخصیص حافظه به‌صورت کلید عددی نگهداری می‌شوند
    private static readonly string[] PersianBigramList =
    [
        "ان", "ای", "ست", "نا", "وا", "رد", "ار", "ور", "لا",
        "سل", "ام", "خو", "اه", "هو", "بو", "شی", "تی", "نی", "دی", "کا",
        "هی", "چه", "بر", "هم", "نش", "را", "رو",
        "تو", "مو", "اس", "ات", "ال", "تا", "در", "با", "یا", "پس", "آن",
        "او", "ما", "نه", "یک", "بی", "پر", "سر", "من", "ون", "ید", "شد",
        "می", "شه", "نم", "ها"
    ];

    private static readonly HashSet<int> PersianBigrams =
        PersianBigramList.Select(b => BigramKey(b[0], b[1])).ToHashSet();

    private static int BigramKey(char first, char second) => (first << 16) | second;

    private static readonly string[] EnglishClusters =
    [
        "str", "spr", "scr", "sch", "ck", "ght", "tch", "tion", "ing", "ment", "ness",
        "able", "ible", "ous", "ful", "less", "est", "ist", "ph", "wh", "qu",
    ];

    public static string? DetectWrongLayout(string word, bool currentLayoutIsPersian)
    {
        word = word.Trim();
        if (word.Length < 2)
            return null;

        if (currentLayoutIsPersian)
            return DetectEnglishIntendedOnPersianLayout(word);

        return DetectPersianIntendedOnEnglishLayout(word);
    }

    private static string? DetectEnglishIntendedOnPersianLayout(string word)
    {
        // کلمهٔ لغت‌نامه‌ای یا کلمه‌ای با پسوند چسبان («تشخیصش») — فارسیِ عمدی است
        if (IsKnownPersianWord(word))
            return null;

        var mappedEn = Mapper.PersianToEnKeys(word);

        // اگر روی حالت فارسی آدرس یا نام سایت تایپ شده باشد، به انگلیسی برمی‌گردد
        if (ShouldConvertToWebAddress(mappedEn))
            return mappedEn;

        if (EnglishWords.Contains(mappedEn))
            return mappedEn;

        if (Mapper.CountPersian(word) >= word.Length * 0.75)
        {
            var faScore = ScoreIntentionalPersian(word);
            if (faScore >= StrongPersianThreshold)
                return null;

            var enScore = ScoreEnglishOnPersianKeys(word, mappedEn);
            if (enScore >= 0.28 && enScore > faScore + ScoreMargin)
                return mappedEn;
            return null;
        }

        if (MappableEnRatio(word) >= 0.75)
        {
            var englishScore = ScoreIntentionalEnglish(word);
            var persianOnKeys = ScorePersianOnEnglishKeys(word);
            if (englishScore >= 0.28 && englishScore > persianOnKeys + ScoreMargin)
                return word;
        }

        return null;
    }

    private static string? DetectPersianIntendedOnEnglishLayout(string word)
    {
        // کاربر در حال وارد کردن آدرس یا نام سایت است — نباید به فارسی تبدیل شود
        if (IsProbablyWebInput(word))
            return null;

        if (IsEnglishCommaList(word))
            return null;

        if (EnglishWords.Contains(word))
            return null;

        var mappedFa = Mapper.EnKeysToPersian(word);

        if (IsKnownPersianWord(mappedFa))
            return mappedFa;

        var englishScore = ScoreIntentionalEnglish(word);
        if (englishScore >= StrongEnglishThreshold)
            return null;

        var persianScore = ScorePersianOnEnglishKeys(word);
        if (persianScore >= 0.22 && persianScore > englishScore + ScoreMargin)
            return mappedFa;

        return null;
    }

    /// <summary>آیا کاربر روی حالت انگلیسی مشغول تایپ آدرس/ایمیل/نام سایت است؟ (سخت‌گیری کم؛ فقط اصلاح را متوقف می‌کند)</summary>
    private static bool IsProbablyWebInput(string word)
    {
        if (word.Contains("://"))
            return true;

        if (word.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            return true;

        if (word.Contains('@') && LooksLikeEmailOrDomain(word))
            return true;

        if (HasDomainSuffix(word))
            return true;

        return IsKnownSiteName(word);
    }

    /// <summary>آیا متن نگاشت‌شده از حالت فارسی، یک آدرس/نام سایت واقعی است؟ (سخت‌گیری زیاد؛ چون باعث اصلاح خودکار می‌شود)</summary>
    private static bool ShouldConvertToWebAddress(string mappedEn)
    {
        if (IsKnownSiteName(mappedEn))
            return true;

        if (!LooksLikeEmailOrDomain(mappedEn))
            return false;

        if (mappedEn.StartsWith("www.", StringComparison.OrdinalIgnoreCase) && mappedEn.Length >= 8)
            return true;

        return HasDomainSuffix(mappedEn) && Mapper.CountAsciiLetters(mappedEn) >= 4;
    }

    private static bool HasDomainSuffix(string text)
    {
        foreach (var suffix in DomainSuffixes)
        {
            if (text.Length > suffix.Length &&
                text.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>نام سایت با یا بدون www. و پسوند دامنه (مثل digikala یا www.digikala.com)</summary>
    private static bool IsKnownSiteName(string ascii)
    {
        var core = ascii;

        if (core.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            core = core[4..];

        foreach (var suffix in DomainSuffixes)
        {
            if (core.Length > suffix.Length &&
                core.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                core = core[..^suffix.Length];
                break;
            }
        }

        return core.Length >= 3 && SiteNames.Contains(core);
    }

    private static bool IsEnglishCommaList(string word)
    {
        if (!word.Any(ch => Mapper.PersianPunctAscii.Contains(ch)))
            return false;

        var parts = word.Replace(';', ',').Split(',', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && parts.All(p => EnglishWords.Contains(p));
    }

    private static double MappableEnRatio(string text) =>
        text.Length == 0 ? 0 : (double)Mapper.CountEnLayoutKeys(text) / text.Length;

    private static double MappableFaRatio(string text)
    {
        if (text.Length == 0) return 0;
        var converted = Mapper.PersianToEnKeys(text);
        var changed = text.Zip(converted).Count(pair => pair.First != pair.Second);
        return (double)changed / text.Length;
    }

    private static double PersianBigramScore(string text)
    {
        if (text.Length < 2) return 0;
        var hits = 0;
        for (var i = 0; i < text.Length - 1; i++)
        {
            if (PersianBigrams.Contains(BigramKey(text[i], text[i + 1])))
                hits++;
        }
        return (double)hits / (text.Length - 1);
    }

    private static bool HasPersianMarker(string text) =>
        PersianMarkers.Any(marker => text.Contains(marker, StringComparison.Ordinal));

    private static double EnglishClusterScore(string word)
    {
        var lower = word.ToLowerInvariant();
        return EnglishClusters.Any(lower.Contains) ? 0.55 : 0;
    }

    private static double EnglishVowelScore(string word)
    {
        var letters = word.Where(char.IsLetter).Select(char.ToLowerInvariant).ToArray();
        if (letters.Length < 2) return 0;

        var vowels = letters.Count(c => "aeiou".Contains(c));
        if (vowels == 0) return 0;

        var ratio = (double)vowels / letters.Length;
        if (letters.Length >= 4 && ratio >= 0.38) return 0.65;
        if (ratio >= 0.30) return 0.35;
        return 0.22;
    }

    private static double ScoreLikelyEnglish(string candidate)
    {
        if (EnglishWords.Contains(candidate) || IsKnownSiteName(candidate))
            return 1.0;

        if (candidate.Any(ch => Mapper.PersianPunctAscii.Contains(ch)))
            return 0;

        var letters = candidate.Where(char.IsAsciiLetter).ToArray();
        if (letters.Length < candidate.Length * 0.85)
            return 0;

        if (letters.Length <= 3)
            return 0;

        if (!HasReasonableEnglishVowelRatio(candidate))
            return 0;

        if (LooksLikeGibberishAscii(candidate))
            return 0;

        if (LooksLikeEmailOrDomain(candidate))
            return Math.Max(EnglishVowelScore(candidate), 0.35);

        var score = EnglishClusterScore(candidate) + EnglishVowelScore(candidate);
        return score >= 0.25 ? Math.Min(score, 1.0) : 0;
    }

    private static bool HasReasonableEnglishVowelRatio(string word)
    {
        var letters = word.Where(char.IsLetter).Select(char.ToLowerInvariant).ToArray();
        if (letters.Length == 0) return false;
        var vowels = letters.Count(c => "aeiou".Contains(c));
        return vowels > 0 && (double)vowels / letters.Length >= 0.20;
    }

    private static bool LooksLikeGibberishAscii(string word)
    {
        if (HasLongConsonantRun(word))
            return true;

        var letters = word.Where(char.IsLetter).Select(char.ToLowerInvariant).ToArray();
        if (letters.Length >= 5)
        {
            var vowels = letters.Count(c => "aeiou".Contains(c));
            if ((double)vowels / letters.Length < 0.18)
                return true;
        }

        return false;
    }

    private static bool HasLongConsonantRun(string word)
    {
        var run = 0;
        foreach (var ch in word.ToLowerInvariant())
        {
            if (char.IsLetter(ch) && !"aeiou".Contains(ch))
            {
                run++;
                if (run >= 5) return true;
            }
            else
            {
                run = 0;
            }
        }

        return false;
    }

    private static double ScoreIntentionalEnglish(string word)
    {
        if (EnglishWords.Contains(word) || IsKnownSiteName(word))
            return 1.0;
        if (word.Any(ch => Mapper.PersianPunctAscii.Contains(ch)))
            return 0;
        return Math.Min(EnglishClusterScore(word) + EnglishVowelScore(word), 1.0);
    }

    private static double ScorePersianOnEnglishKeys(string word)
    {
        if (MappableEnRatio(word) < 0.75) return 0;
        var converted = Mapper.EnKeysToPersian(word);
        if (Mapper.CountPersian(converted) < converted.Length * 0.85) return 0;

        var bigram = PersianBigramScore(converted);
        var score = bigram * 0.55;
        if (HasPersianMarker(converted)) score += 0.42;

        if (word.Any(ch => Mapper.PersianPunctAscii.Contains(ch)))
            score += 0.18;

        if (word.Length <= 3)
        {
            if (!HasPersianMarker(converted) && bigram < 0.45)
                return 0;
        }

        return Math.Min(score, 1.0);
    }

    private static double ScoreIntentionalPersian(string word)
    {
        if (IsKnownPersianWord(word))
            return 1.0;

        if (Mapper.CountPersian(word) < word.Length * 0.85) return 0;

        var score = PersianBigramScore(word) * 0.5;
        if (HasPersianMarker(word)) score += 0.45;
        if (word.Length >= 4) score += 0.12;
        return Math.Min(score, 1.0);
    }

    private static double ScoreEnglishOnPersianKeys(string word, string candidate)
    {
        if (Mapper.CountPersian(word) < word.Length * 0.75) return 0;
        if (MappableFaRatio(word) < 0.75) return 0;

        if (!candidate.All(ch => ch <= 127) || Mapper.CountPersian(candidate) > 0) return 0;
        if (!HasEnoughAsciiLetters(candidate)) return 0;

        return ScoreLikelyEnglish(candidate);
    }

    private static bool HasEnoughAsciiLetters(string candidate)
    {
        var letterCount = Mapper.CountAsciiLetters(candidate);
        if (letterCount >= candidate.Length * 0.85)
            return true;

        return LooksLikeEmailOrDomain(candidate);
    }

    private static bool LooksLikeEmailOrDomain(string text)
    {
        if (Mapper.CountAsciiLetters(text) < 3)
            return false;

        return text.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '.' or '@' or '-' or '_');
    }
}
