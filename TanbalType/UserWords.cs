namespace TanbalType;

/// <summary>
/// لغاتی که کاربر خودش به برنامه یاد می‌دهد. هر بار که همان کلمه با چیدمانِ اشتباه
/// تایپ شود، برنامه آن را دقیقاً به شکلی که کاربر ثبت کرده اصلاح می‌کند.
/// این فهرست بر قاعده‌های تشخیص اولویت دارد و بین اجراهای برنامه ذخیره می‌شود.
/// </summary>
internal static class UserWords
{
    private static readonly object Gate = new();

    // کلید = شکلِ نرمال‌شدهٔ کلمه (برای تطبیق با خروجیِ نگاشتِ کیبورد)،
    // مقدار = دقیقاً همان چیزی که کاربر وارد کرده و باید جایگزین شود.
    private static readonly Dictionary<string, string> Words = new(StringComparer.OrdinalIgnoreCase);
    private static bool _loaded;

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TanbalType",
        "userwords.txt");

    public static IReadOnlyList<string> GetAll()
    {
        EnsureLoaded();
        lock (Gate)
            return Words.Values.OrderBy(w => w, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>آیا خودِ این کلمه یکی از لغات کاربر است؟ (یعنی درست تایپ شده و نباید تغییر کند)</summary>
    public static bool IsUserWord(string word) => TryGetCanonical(word, out _);

    /// <summary>اگر کلمه در فهرستِ کاربر باشد، شکلِ دقیقی که کاربر ثبت کرده را برمی‌گرداند.</summary>
    public static bool TryGetCanonical(string word, out string canonical)
    {
        canonical = string.Empty;
        if (string.IsNullOrWhiteSpace(word))
            return false;

        EnsureLoaded();
        lock (Gate)
        {
            // مسیر سریع برای کاربری که هنوز لغتی ثبت نکرده (هر کلمهٔ تایپ‌شده از اینجا رد می‌شود)
            if (Words.Count == 0)
                return false;

            if (!Words.TryGetValue(Normalize(word), out var stored))
                return false;

            canonical = stored;
            return true;
        }
    }

    public static bool Add(string word)
    {
        word = word.Trim();
        var key = Normalize(word);
        if (key.Length == 0)
            return false;

        EnsureLoaded();
        lock (Gate)
        {
            if (!Words.TryAdd(key, word))
                return false;
            Save();
        }

        return true;
    }

    public static bool Remove(string word)
    {
        EnsureLoaded();
        lock (Gate)
        {
            if (!Words.Remove(Normalize(word)))
                return false;
            Save();
        }

        return true;
    }

    /// <summary>
    /// یکسان‌سازی نویسه‌های عربی و حذف نیم‌فاصله/نشانگرهای جهت، تا کلمه‌ای که کاربر ثبت کرده
    /// با خروجیِ نگاشتِ کیبورد (که همیشه «ی» و «ک» فارسی و بدون نیم‌فاصله است) تطبیق پیدا کند.
    /// </summary>
    private static string Normalize(string word)
    {
        var buffer = new System.Text.StringBuilder(word.Length);
        foreach (var ch in word.Trim())
        {
            switch (ch)
            {
                case 'ي':
                case 'ى':
                    buffer.Append('ی');
                    break;
                case 'ك':
                    buffer.Append('ک');
                    break;
                case '\u200C': // نیم‌فاصله
                case '\u200E': // LRM
                case '\u200F': // RLM
                    break;
                default:
                    buffer.Append(ch);
                    break;
            }
        }

        return buffer.ToString();
    }

    private static void EnsureLoaded()
    {
        if (_loaded)
            return;

        lock (Gate)
        {
            if (_loaded)
                return;

            try
            {
                var path = FilePath;
                if (File.Exists(path))
                {
                    foreach (var line in File.ReadAllLines(path, System.Text.Encoding.UTF8))
                    {
                        var trimmed = line.Trim();
                        var key = Normalize(trimmed);
                        if (key.Length > 0)
                            Words[key] = trimmed;
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.Write($"UserWords load failed: {ex.Message}");
            }

            _loaded = true;
        }
    }

    // فراخوانی‌کننده باید Gate را قفل کرده باشد
    private static void Save()
    {
        try
        {
            var path = FilePath;
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllLines(path, Words.Values.OrderBy(w => w, StringComparer.OrdinalIgnoreCase), System.Text.Encoding.UTF8);
        }
        catch (Exception ex)
        {
            AppLog.Write($"UserWords save failed: {ex.Message}");
        }
    }
}
