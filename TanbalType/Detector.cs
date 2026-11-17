namespace TanbalType;

public static class Detector
{
    private const double ScoreMargin = 0.12;
    private const double StrongEnglishThreshold = 0.45;
    private const double StrongPersianThreshold = 0.35;

    /// <summary>
    /// کمینهٔ طولِ توکن برای اصلاح، وقتی نمی‌دانیم آغازِ کلمه کجاست — یعنی کاربر در
    /// ادامهٔ متنی که از قبل آن‌جا بوده تایپ کرده و فاصله‌ای در میان نیست. پایانِ
    /// کوتاهِ کلماتِ انگلیسی («ing» در engineering، «nd»، «ard») و تکه‌های کوتاهِ
    /// فارسی («اث» در «احداث») تصویرِ زبانِ دیگر را دارند و نباید اصلاح شوند.
    /// </summary>
    private const int MinUnanchoredLength = 4;

    /// <summary>
    /// پسوندهای دامنه، برای جهتِ سخت‌گیرانهٔ فارسی←انگلیسی.
    ///
    /// پیش‌تر فقط ۲۳ پسوند در این فهرست بود و تشخیصِ آدرس تنها بر همین فهرست تکیه داشت؛
    /// نتیجه این بود که هر دامنه‌ای با پسوندِ بیرونِ فهرست اصلاح می‌شد و خراب می‌شد
    /// (<c>military.world</c> → «ئهمهفشقغ.صخقمی»، <c>filimo.fr</c> → «بهمهئخ.بق»).
    /// الان در جهتِ انگلیسی←فارسی اصلاً فهرستی در کار نیست و شکلِ آدرس ملاک است
    /// (<see cref="HasHostShape"/>)، پس هر پسوندی — حتی پسوندهای تازه — محافظت می‌شود.
    /// این فهرست فقط جایی به کار می‌آید که می‌خواهیم متنِ کاربر را عوضاً *تغییر* دهیم،
    /// یعنی وقتی آدرس اشتباهاً روی حالت فارسی تایپ شده و باید به انگلیسی برگردد.
    /// </summary>
    private static readonly HashSet<string> KnownTlds = BuildTldSet(
        // دامنه‌های کشوری (ccTLD) — کاملِ فهرست IANA
        "ac ad ae af ag ai al am ao aq ar as at au aw ax az ba bb bd be bf bg bh bi bj bm bn bo br bs bt " +
        "bv bw by bz ca cc cd cf cg ch ci ck cl cm cn co cr cu cv cw cx cy cz de dj dk dm do dz ec ee eg " +
        "er es et eu fi fj fk fm fo fr ga gb gd ge gf gg gh gi gl gm gn gp gq gr gs gt gu gw gy hk hm hn " +
        "hr ht hu id ie il im in io iq ir is it je jm jo jp ke kg kh ki km kn kp kr kw ky kz la lb lc li " +
        "lk lr ls lt lu lv ly ma mc md me mg mh mk ml mm mn mo mp mq mr ms mt mu mv mw mx my mz na nc ne " +
        "nf ng ni nl no np nr nu nz om pa pe pf pg ph pk pl pm pn pr ps pt pw py qa re ro rs ru rw sa sb " +
        "sc sd se sg sh si sj sk sl sm sn so sr ss st su sv sx sy sz tc td tf tg th tj tk tl tm tn to tr " +
        "tt tv tw tz ua ug uk us uy uz va vc ve vg vi vn vu wf ws ye yt za zm zw " +
        // دامنه‌های عمومی و دامنه‌های سطح‌دومِ پرکاربرد
        "ac.ir ac.uk academy aero agency app apps arpa art asia bank bar bio biz blog business cafe " +
        "capital care cat center chat city click clinic cloud club co.in co.jp co.kr co.nz co.th " +
        "co.uk co.za college com com.ar com.au com.bd com.br com.cn com.eg com.hk com.ir com.mx com.my " +
        "com.ph com.pk com.sa com.sg com.tr com.tw com.vn community company consulting coop cyber data " +
        "deal design dev digital dog eco edu edu.au education email enterprises estate events expert farm " +
        "film finance fit flights forum fun fund gallery game games global gov gov.au gov.ir gov.uk green " +
        "group guru health home host hotel house id.ir industries info institute int international " +
        "jobs land law legal life link live market me.uk media mil mobi money movie museum music name " +
        "ne.jp net net.au net.cn net.ir net.uk network news ninja one online or.jp org org.au org.cn " +
        "org.ir org.uk organic page pet photo pizza place plus post press pro radio restaurant rocks sale " +
        "sch.ir school shop shopping show site social software solutions space store studio systems team " +
        "tech tel ticket today tools top tours town travel tube university video vip web website wiki " +
        "world xyz zone");

    private static HashSet<string> BuildTldSet(string packed) =>
        new(packed.Split(' ', StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);

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
        "crunchyroll", "wattpad",
        // برندها/سایت‌هایی که نگاشتشان با واژهٔ فارسی تداخل داشت (kia→«نهش»، audi→«شعیه»، ...)
        "kia", "audi", "dji", "kali", "raja", "avval"
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
        "chakeram", "dorood", "dorud", "khaste", "nabashi", "nabashid", "mrc",

        // --- مخفف‌ها و نام‌های کوتاه سایت/برند ---
        // این‌ها چون کوتاه‌اند با واژه‌های واقعی فارسی تداخل داشتند و اشتباهاً فارسی می‌شدند
        // (مثلاً tv→«فر»، ig→«هل»، aol→«شخم»). این‌جا محافظت می‌شوند بدون آنکه واژهٔ فارسی حذف شود.
        "tv", "vk", "hh", "cc", "ig", "fb", "tg", "hm", "lg", "aol", "dhl", "un",
        // کدهای زبان/کشور که مستقیم در نوار آدرس تایپ می‌شوند (fa → «بش» می‌شد)
        "fa", "en", "de", "fr", "es", "ru", "ar", "tr", "it", "jp", "kr", "cn", "uk",

        // --- دستورهای ترمینال/ویندوز و پسوند فایل ---
        // این‌ها با واژه‌های نادر فارسی تداخل داشتند (cmd → «زئی»، pwd → «حصی»، ssh → «سسا»)
        "cmd", "pwd", "ssh", "scp", "cls", "dir", "cd", "ls", "del", "ren", "md", "rd",
        "echo", "ping", "curl", "wget", "sudo", "apt", "vim", "nano", "gcc", "top", "ps",
        "kill", "sed", "awk", "tar", "unzip", "df", "du", "reg", "regedit", "msconfig",
        "taskmgr", "explorer", "notepad", "calc", "ipconfig", "tasklist", "netstat",
        "exe", "dll", "iso", "img", "bin", "dat", "cfg", "ini", "bat", "csv", "xls",
        "ppt", "sh", "py", "ts", "cs", "cpp", "yml", "yaml", "toml", "md5", "sha",

        // --- واژه‌های کوتاه پرکاربرد که با فارسیِ نادر تداخل داشتند ---
        "big", "add", "avg", "off", "min", "max", "sum", "tmp", "temp", "demo", "root",
        "home", "work", "edit", "view", "tool", "menu", "back", "next", "prev", "play",
        "live", "free", "half", "auto", "call", "send", "down", "yes", "bye", "end",

        // --- لغات انگلیسیِ پرکاربرد که تصویرِ فارسی‌شان واژهٔ فارسی نیست ---
        // سویپ روی ۱۰٬۰۰۰ واژهٔ پربسامد انگلیسی نشان داد این‌ها اشتباهاً فارسی می‌شدند
        // (مثلاً hand → «اشدی»، hill → «اهمم»، military → «ئهمهفشقغ»). برخلاف دستهٔ
        // قبل، این‌ها با حذف از لغت‌نامه حل نمی‌شدند چون مسدودکننده یا ریشهٔ واقعیِ
        // فارسی بود («اشد»+«ی») یا اصلاً لغت‌نامه دخیل نبود و نمره‌دهی تصمیم می‌گرفت.
        // واژه‌هایی که تصویرِ فارسی‌شان واقعاً معنادار است (fun→«بعد»، leg→«مثل»،
        // col→«زخم»، oct→«خزف»، til→«فهم») عمداً این‌جا نیستند تا اصلاحِ آن واژهٔ
        // فارسی از بین نرود.
        "high", "highly", "highs", "highland", "hand", "legal", "child", "kids", "gold", "aug", "via",
        "held", "hell", "military", "kind", "band", "fall", "fill", "hill", "hull", "hold", "told",
        "toll", "fold", "tall", "valid", "solid", "civil", "facility", "wild", "milk", "mild", "silk",
        "sand", "grid", "fig", "lid", "gel", "lan", "nokia", "andy", "jill", "lucia", "possibility",
        "probability", "stability", "flexibility", "mobility", "roughly", "thoroughly", "rand", "ghz", "msg", "dns", "cia",
        "hung",

        // --- تداخلِ «انگلیسیِ پرکاربرد» با «فارسیِ کم‌کاربرد»: انگلیسی برنده می‌شود ---
        // سویپ روی فهرست ۱۰٬۰۰۰ واژهٔ انگلیسی به‌ترتیبِ بسامد، فقط موارد زیر رتبهٔ ~۳۵۰۰
        // را نگه داشته‌ایم و تنها آن‌جا که تصویرِ فارسی واژهٔ پرکاربردی نیست:
        //   oct→«خزف» china→«زاهدش» sun→«سعد» land→«مشدی» india→«هدیهش» ad→«شی»
        //   aid→«شهی» tag→«فشل» vhs→«راس» ski→«سنه» src→«سقز» hiv→«اهر» kid→«نهی»
        //   vat→«رشف» ali→«شمه»
        // در مقابل، آن‌جا که تصویرِ فارسی واژهٔ پرکاربردی است عمداً دست نزده‌ایم تا اصلاحِ
        // فارسی از بین نرود: fun→«بعد» fund→«بعدی» van→«رشد» il→«هم» fi→«به» cold→«زخمی»
        // iv→«هر» nd→«دی» pdt→«حیف» oak→«خشن» si→«سه» leg→«مثل» col→«زخم» til→«فهم»
        //
        // این فهرست جهتِ انگلیسی→انگلیسی را می‌گیرد (کاربر انگلیسی تایپ می‌کند و نباید
        // خراب شود). برای آن‌که در جهتِ فارسی→انگلیسی هم اصلاح شود، واژهٔ فارسی باید از
        // لغت‌نامه برود؛ آن کار در ENGLISH_WINS داخل add_big_to_detector.py انجام شده و
        // فقط برای واژه‌های کم‌کاربرد (خزف، شهی، فشل، مشدی، سنه، رشف، شمه).
        "oct", "china", "sun", "land", "india", "ad", "aid", "tag", "vhs", "ski", "src", "hiv",
        "kid", "vat", "ali",
        // آلمانی است نه انگلیسی، ولی این فهرست در عمل «توکن‌های لاتینی که دست‌نخورده
        // بمانند» است (مثل فینگلیش و مخفف‌ها)، و «عدی» واژهٔ پرکاربردی نیست
        "und",

        // --- سویپ روی کل فرهنگ املاییِ انگلیسی (hunspell en-US) ---
        // ۱۷ موردِ زیر پیش از این روی حالتِ انگلیسی اشتباهاً فارسی می‌شدند
        // (shy→«ساغ»، avid→«شرهی»، rival→«قهرشم»، anvil→«شدرهم»، ...). بقیه الان هم
        // سالم‌اند ولی این‌جا ثبت شده‌اند تا با تغییرِ بعدیِ لغت‌نامه دوباره نشکنند.
        // («aid» از قبل بالاتر در همین فهرست است.)
        // تصویرِ فارسیِ هیچ‌یک واژهٔ فارسیِ واقعی نیست، پس اصلاحی از دست نمی‌رود.
        "shy", "gut", "hut", "jug", "jot", "lag", "lax", "fad", "foe", "cog", "hid",
        "avid", "fond", "hail", "nail", "raid", "rail", "laid", "slid", "fail",
        "rival", "soda", "skid", "anvil"
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

    /// <summary>قدرتِ تطبیق یک کلمه با لغت‌نامهٔ فارسی.</summary>
    private enum DictMatch
    {
        None,
        Exact,   // خودِ کلمه در لغت‌نامه است — شواهد قوی
        Strong,  // ریشهٔ ۴+ حرفی یا پسوند چندحرفی (مثل «تشخیص»+«ش»، «خون»+«مون»)
        Weak,    // ریشهٔ کوتاه (۲-۳ حرفی) + پسوند تک‌حرفی — شواهد ضعیف و مستعد خطا
    }

    /// <summary>جستجوی کلمه در لغت‌نامه، مستقیم یا با جداکردن پسوند چسبان (مثل «تشخیصش»، «کتابم»، «خونمون»)</summary>
    private static DictMatch LookupPersian(string word)
    {
        if (PersianWords.Contains(word))
            return DictMatch.Exact;

        foreach (var suffix in PersianAttachedSuffixes)
        {
            // برای پسوندهای تک‌حرفی، ریشهٔ حداقل ۳ حرفی لازم است تا با کلمات کوتاه انگلیسی
            // (مثل api که روی حالت فارسی «شحه» می‌شود) تداخل نکند
            var minStem = suffix.Length == 1 ? 3 : 2;
            if (word.Length < suffix.Length + minStem)
                continue;
            if (!word.EndsWith(suffix, StringComparison.Ordinal))
                continue;

            var stem = word[..^suffix.Length];
            if (!PersianWords.Contains(stem))
                continue;

            return suffix.Length == 1 && stem.Length <= 3 ? DictMatch.Weak : DictMatch.Strong;
        }

        return DictMatch.None;
    }

    private static bool IsKnownPersianWord(string word) => LookupPersian(word) != DictMatch.None;

    /// <summary>
    /// لغت‌نامه را از پیش می‌خواند. از <c>Program</c> روی نخِ پس‌زمینه صدا زده می‌شود تا
    /// هزینهٔ خواندنش نه بالاآمدنِ برنامه را عقب بیندازد و نه اولین کلمهٔ کاربر را.
    /// </summary>
    public static void Warmup() => _ = PersianWords.Count;

    /// <summary>
    /// واژه‌های دوحرفیِ پرکاربرد فارسی.
    ///
    /// لغت‌نامه بیش از ۷۰۰ ورودی دوحرفی دارد که اکثرشان ترکیب‌های نادر یا بی‌معنی‌اند
    /// (آء، آآ، آت، بش، ...). اگر همهٔ آن‌ها مبنای اصلاح باشند، مخفف‌ها و نام سایت‌های
    /// دوحرفی (fa، tv، ig، ...) هنگام زدن Enter در نوار آدرس اشتباهاً فارسی می‌شوند.
    /// بنابراین در جهتِ انگلیسی←فارسی فقط این فهرست ملاک است.
    /// (در جهتِ مخالف همچنان کل لغت‌نامه معتبر است تا واژهٔ فارسیِ تایپ‌شده حفظ شود.)
    /// </summary>
    private static readonly HashSet<string> CommonTwoLetterPersian =
    [
        "به", "از", "با", "تا", "را", "که", "در", "هم", "ما", "او", "بی", "یا",
        "نه", "من", "تو", "ای", "آن", "دو", "سه", "بر", "پس", "هر", "چه", "جا",
        "رو", "شد", "کن", "یک", "کم", "بد", "دل", "سر", "پا", "گل", "شب", "نو",
        "بس", "کل", "رد", "بو", "مو", "تن", "کی", "وی", "آب", "نان", "کار",
    ];

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

    /// <summary>
    /// پایان‌های پرکاربردِ کلماتِ انگلیسی.
    ///
    /// وقتی آغازِ کلمه معلوم نیست، آنچه کاربر زده می‌تواند همین باشد: تکه‌ای که به
    /// کلمه‌ای که از قبل نوشته شده چسبیده است. چنین تکه‌ای حتی اگر تصویرش در
    /// لغت‌نامهٔ فارسی پیدا شود هم اصلاح نمی‌شود — «ther» (در «father») تصویرِ
    /// «فاثق» را دارد که در لغت‌نامه هست ولی کسی آن را تایپ نمی‌کند.
    /// هیچ‌یک از این‌ها تصویرِ یک واژهٔ فارسیِ واقعی نیست، پس چیزی از دست نمی‌رود.
    /// </summary>
    private static readonly HashSet<string> EnglishWordEndings = new(StringComparer.OrdinalIgnoreCase)
    {
        "ing", "ings", "ed", "er", "ers", "est", "ly", "ally", "ness", "ment", "ments",
        "tion", "tions", "sion", "sions", "ation", "able", "ible", "ful", "less", "ous",
        "ive", "ity", "ties", "ard", "ards", "ward", "wards", "ther", "ield", "ought",
        "nal", "tal", "ral", "ial", "ical", "ism", "ist", "ence", "ship", "hood",
        "dard", "tand", "ound", "ount", "ance", "ting", "ding", "ning", "king", "ling",
    };

    private static readonly string[] EnglishClusters =
    [
        "str", "spr", "scr", "sch", "ck", "ght", "tch", "tion", "ing", "ment", "ness",
        "able", "ible", "ous", "ful", "less", "est", "ist", "ph", "wh", "qu",
    ];

    /// <summary>
    /// تصمیم دربارهٔ اصلاحِ یک توکن.
    ///
    /// <paramref name="wordStartKnown"/>: آیا مطمئنیم همین‌جا آغازِ کلمه است؟ یعنی
    /// می‌دانیم پیش از این توکن فاصله/اینتر بوده، نه یک حرفِ دیگر. وقتی کاربر پنجره
    /// عوض کرده، با ماوس جای مکان‌نما را برده یا برنامه تازه بالا آمده، آن‌چه پیش از
    /// توکن است نامعلوم می‌مانَد؛ آن‌وقت توکن می‌تواند تکهٔ پایانیِ کلمه‌ای باشد که
    /// از قبل نوشته شده («engineer» + «ing») و اصلاحش متنِ درست را خراب می‌کند.
    /// در آن حالت فقط شواهدِ قطعی پذیرفته می‌شود و حدس‌های نمره‌ای کنار می‌رود.
    /// </summary>
    public static string? DetectWrongLayout(string word, bool currentLayoutIsPersian, bool wordStartKnown = true)
    {
        word = word.Trim();
        if (word.Length < 2)
            return null;

        // کلمات استثنایی که کاربر خودش اضافه کرده هرگز اصلاح نمی‌شوند
        if (UserExceptions.IsException(word))
            return null;

        // لغاتی که کاربر خودش ثبت کرده بر بقیهٔ قاعده‌ها (از جمله محدودیتِ اعداد) اولویت دارند
        if (TryResolveUserWord(word, currentLayoutIsPersian, out var userWord))
            return userWord;

        // اعداد را هرگز اصلاح نمی‌کنیم: رمز عبور، کد، شماره، سال، نسخه و ... نباید تغییر کنند.
        // (char.IsDigit شاملِ ارقام فارسی ۰-۹ و عربی هم می‌شود)
        if (word.Any(char.IsDigit))
            return null;

        if (currentLayoutIsPersian)
            return DetectEnglishIntendedOnPersianLayout(word, wordStartKnown);

        return DetectPersianIntendedOnEnglishLayout(word, wordStartKnown);
    }

    /// <summary>
    /// فهرستِ لغاتِ شخصیِ کاربر: اگر کلمه همان چیزی باشد که کاربر ثبت کرده دست‌نخورده می‌ماند،
    /// و اگر شکلِ تایپ‌شده با چیدمانِ اشتباهِ آن باشد، دقیقاً به شکلِ ثبت‌شده اصلاح می‌شود.
    /// خروجیِ <c>true</c> یعنی تکلیف کلمه روشن شد و قاعده‌های حدسی نباید اجرا شوند.
    /// </summary>
    private static bool TryResolveUserWord(string word, bool currentLayoutIsPersian, out string? result)
    {
        result = null;

        // کاربر همین کلمه را ثبت کرده، پس درست تایپ شده است
        if (UserWords.IsUserWord(word))
            return true;

        var mapped = currentLayoutIsPersian
            ? Mapper.PersianToEnKeys(word)
            : Mapper.EnKeysToPersian(word);

        if (!UserWords.TryGetCanonical(mapped, out var canonical))
            return false;

        result = canonical;
        return true;
    }

    private static string? DetectEnglishIntendedOnPersianLayout(string word, bool wordStartKnown)
    {
        // کلمهٔ لغت‌نامه‌ای یا کلمه‌ای با پسوند چسبان («تشخیصش») — فارسیِ عمدی است
        if (IsKnownPersianWord(word))
            return null;

        var mappedEn = Mapper.PersianToEnKeys(word);

        if (UserExceptions.IsException(mappedEn))
            return null;

        // اگر روی حالت فارسی آدرس یا نام سایت تایپ شده باشد، به انگلیسی برمی‌گردد
        if (ShouldConvertToWebAddress(word, mappedEn))
            return mappedEn;

        // تکهٔ ناتمام: «اث» (در «احداث») تصویرِ «he» را دارد و «اشقی» (در «عاشقی»)
        // تصویرِ «hard» را؛ تکه‌های کوتاه در این حالت دست‌نخورده می‌مانند.
        if (!wordStartKnown && word.Length < MinUnanchoredLength)
            return null;

        if (EnglishWords.Contains(mappedEn))
            return mappedEn;

        // از این‌جا به بعد تصمیم بر پایهٔ نمره است نه فهرست؛ برای تکهٔ ناتمام کافی نیست.
        if (!wordStartKnown)
            return null;

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

    private static string? DetectPersianIntendedOnEnglishLayout(string word, bool wordStartKnown)
    {
        // کاربر در حال وارد کردن آدرس یا نام سایت است — نباید به فارسی تبدیل شود
        if (IsProbablyWebInput(word))
            return null;

        if (IsEnglishCommaList(word))
            return null;

        if (EnglishWords.Contains(word))
            return null;

        var mappedFa = Mapper.EnKeysToPersian(word);

        if (UserExceptions.IsException(mappedFa))
            return null;

        // تکهٔ ناتمام: «ing» (در «engineering») تصویرِ «هدل» را دارد و «nd» تصویرِ «دی».
        if (!wordStartKnown && !IsSafeUnanchored(word))
            return null;

        var match = LookupPersian(mappedFa);
        var englishScore = ScoreIntentionalEnglish(word);

        // توکن دوحرفی: فقط واژه‌های پرکاربرد از روی لغت‌نامه اصلاح می‌شوند
        // (وگرنه fa → «بش»، tv → «فر» و ... آدرس‌ها را خراب می‌کنند)
        if (mappedFa.Length == 2 && !CommonTwoLetterPersian.Contains(mappedFa))
            match = DictMatch.None;

        // تطبیقِ ضعیف (ریشهٔ کوتاه + پسوند تک‌حرفی) نباید بر شواهدِ قویِ انگلیسی غلبه کند.
        // در غیر این صورت نام سایت‌هایی مثل fafa (که به «بشب»+«ش» تجزیه می‌شد) هنگام زدن
        // Enter در نوار آدرس اشتباهاً فارسی می‌شدند.
        if (match == DictMatch.Weak && englishScore >= StrongEnglishThreshold)
            match = DictMatch.None;

        // تطبیقِ ضعیف شاهدِ قطعی نیست، پس برای تکهٔ ناتمام هم کافی نیست.
        if (match == DictMatch.Weak && !wordStartKnown)
            match = DictMatch.None;

        if (match != DictMatch.None)
            return mappedFa;

        if (englishScore >= StrongEnglishThreshold)
            return null;

        // از این‌جا به بعد تصمیم بر پایهٔ نمره است نه لغت‌نامه؛ برای تکهٔ ناتمام کافی نیست.
        if (!wordStartKnown)
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

        // هر چیزی که شکلِ میزبان دارد: با یا بدون زیردامنه، مسیر، پورت، query یا کاربرِ ایمیل.
        // هیچ فهرستی از پسوندها دخیل نیست، پس دامنه‌های تازه هم خودبه‌خود محافظت می‌شوند.
        if (HasHostShape(word))
            return true;

        return IsKnownSiteName(word);
    }

    /// <summary>
    /// بخشِ میزبانِ یک آدرس را جدا می‌کند: scheme، نامِ کاربرِ ایمیل، مسیر، پورت و query حذف می‌شوند.
    /// (<c>https://user@api.site.io:8080/a?b=1</c> → <c>api.site.io</c>)
    /// </summary>
    private static string HostPart(string text)
    {
        var span = text.AsSpan();

        var scheme = span.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0)
            span = span[(scheme + 3)..];

        var at = span.LastIndexOf('@');
        if (at >= 0)
            span = span[(at + 1)..];

        var cut = span.IndexOfAny('/', '?', '#');
        if (cut >= 0)
            span = span[..cut];

        var port = span.IndexOf(':');
        if (port >= 0)
            span = span[..port];

        return span.Trim('.').ToString();
    }

    /// <summary>
    /// آیا توکن شکلِ یک نامِ میزبان را دارد؟ یعنی دو برچسب یا بیشتر، جداشده با نقطه،
    /// که آخری‌شان ۲ تا ۲۴ حرفِ لاتین است و بقیه فقط حرف/رقم/خط تیره دارند.
    /// این ملاکِ ساختاری است نه فهرستی، پس به هیچ پسوندِ خاصی وابسته نیست.
    /// </summary>
    private static bool HasHostShape(string text)
    {
        var host = HostPart(text);
        if (host.Length < 4 || !host.Contains('.'))
            return false;

        var labels = host.Split('.');
        if (labels.Length < 2)
            return false;

        foreach (var label in labels)
        {
            if (label.Length == 0)
                return false;

            foreach (var ch in label)
            {
                if (!char.IsAsciiLetterOrDigit(ch) && ch != '-')
                    return false;
            }
        }

        var tld = labels[^1];
        if (tld.Length is < 2 or > 24 || !tld.All(char.IsAsciiLetter))
            return false;

        // پیش از پسوند باید لااقل یک برچسبِ حرفی باشد، تا «1.2» یا «۱۲.۳۴» آدرس حساب نشود
        for (var i = 0; i < labels.Length - 1; i++)
        {
            if (labels[i].Any(char.IsAsciiLetter))
                return true;
        }

        return false;
    }

    /// <summary>پسوندِ شناخته‌شده در انتهای نامِ میزبان (مثل <c>.io</c>، <c>.com</c>، <c>.ac.ir</c>).</summary>
    private static bool HasKnownTld(string text)
    {
        var host = HostPart(text);
        var lastDot = host.LastIndexOf('.');
        if (lastDot < 0)
            return false;

        // ابتدا پسوندِ دوبخشی (co.uk، ac.ir) و بعد تک‌بخشی
        var prevDot = host.LastIndexOf('.', lastDot - 1 < 0 ? 0 : lastDot - 1);
        if (prevDot >= 0 && prevDot < lastDot && KnownTlds.Contains(host[(prevDot + 1)..]))
            return true;

        return KnownTlds.Contains(host[(lastDot + 1)..]);
    }

    /// <summary>آیا متن نگاشت‌شده از حالت فارسی، یک آدرس/نام سایت واقعی است؟ (سخت‌گیری زیاد؛ چون باعث اصلاح خودکار می‌شود)</summary>
    private static bool ShouldConvertToWebAddress(string word, string mappedEn)
    {
        if (IsKnownSiteName(mappedEn))
            return true;

        if (mappedEn.Contains("://", StringComparison.Ordinal))
            return true;

        if (mappedEn.StartsWith("www.", StringComparison.OrdinalIgnoreCase) && mappedEn.Length >= 8)
            return true;

        // این‌جا متنِ کاربر عوض می‌شود، پس هم شکلِ آدرس لازم است و هم پسوندِ شناخته‌شده؛
        // صرفِ «حرف.حرف» کافی نیست چون دو واژهٔ فارسیِ چسبیده به نقطه هم همان شکل را دارد
        // («سلام.شب» → sghl.af).
        return HasHostShape(mappedEn)
               && HasKnownTld(mappedEn)
               && Mapper.CountAsciiLetters(mappedEn) >= 4
               && !IsDottedPersianText(word);
    }

    /// <summary>
    /// آیا توکن در اصل چند واژهٔ فارسی است که با نقطه به هم چسبیده‌اند؟ («سلام.شب»، «دست.راست»)
    ///
    /// از نمره‌دهیِ عمومی دقیق‌تر است: <see cref="HasPersianMarker"/> زیررشته‌ای کار می‌کند و
    /// Markerهای سه‌حرفی داخلِ آدرسِ اشتباه‌تایپ‌شده هم پیدا می‌شوند
    /// («همه» داخلِ «ئهمهفشقغ» یعنی military)، پس آدرس را اشتباهاً فارسی حساب می‌کرد.
    /// </summary>
    private static bool IsDottedPersianText(string word)
    {
        var parts = word.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return false;

        foreach (var part in parts)
        {
            // بخشِ دوحرفی فقط اگر از واژه‌های پرکاربرد باشد؛ ورودی‌های دوحرفیِ لغت‌نامه
            // بیشترشان نویزند و با پسوندهای دوحرفیِ دامنه تداخل می‌کنند («of.tv» → «خب.فر»)
            var known = part.Length == 2
                ? CommonTwoLetterPersian.Contains(part)
                : IsKnownPersianWord(part);

            if (!known)
                return false;
        }

        return true;
    }

    /// <summary>نام سایت با یا بدون www. و پسوند دامنه (مثل digikala یا www.digikala.com)</summary>
    private static bool IsKnownSiteName(string ascii)
    {
        var core = HostPart(ascii);

        if (core.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            core = core[4..];

        // پسوند را از انتها جدا می‌کنیم تا هستهٔ نام بماند (digikala.com → digikala)
        while (core.LastIndexOf('.') is var dot && dot > 0 && KnownTlds.Contains(core[(dot + 1)..]))
            core = core[..dot];

        return core.Length >= 3 && SiteNames.Contains(core);
    }

    /// <summary>
    /// آیا این توکن، بی‌آنکه بدانیم آغازِ کلمه است، به‌اندازهٔ کافی ایمن است که
    /// اصلاح شود؟ پایانِ کلماتِ انگلیسی («ing»، «ther») و تکه‌های کوتاه نه؛ توکنِ
    /// بلند یا توکنی که شکلِ کلمهٔ انگلیسی ندارد («o,f» → «خوب») بله.
    /// </summary>
    private static bool IsSafeUnanchored(string word) =>
        !EnglishWordEndings.Contains(word)
        && (word.Length >= MinUnanchoredLength || HasNonEnglishWordShape(word));

    /// <summary>
    /// آیا این توکنِ ASCII نشانه‌ای دارد که داخلِ یک کلمهٔ انگلیسی دیده نمی‌شود؟
    ///
    /// حروفِ «و ک گ ج چ» روی کلیدهای «, ; ' [ ]» می‌نشینند، پس «o,f» («خوب») هیچ‌گاه
    /// پایانِ یک کلمهٔ انگلیسی نیست و بی‌خطر اصلاح می‌شود. حرفِ بزرگ در میانهٔ توکن هم
    /// همین معنی را دارد (Shift برای «آ»، «ژ» و ... زده شده است).
    /// </summary>
    private static bool HasNonEnglishWordShape(string word)
    {
        for (var i = 0; i < word.Length; i++)
        {
            var ch = word[i];
            if (!char.IsAsciiLetter(ch))
                return true;

            if (i > 0 && char.IsAsciiLetterUpper(ch))
                return true;
        }

        return false;
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
