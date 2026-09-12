namespace TanbalType;

/// <summary>
/// لیستِ لغاتِ استثنا که کاربر خودش اضافه می‌کند تا برای آن‌ها اصلاح خودکار انجام نشود
/// (چه املای فارسی و چه املای انگلیسیِ کلمه). این لیست بین اجراهای برنامه ذخیره می‌شود.
/// </summary>
internal static class UserExceptions
{
    private static readonly object Gate = new();
    private static readonly HashSet<string> Words = new(StringComparer.OrdinalIgnoreCase);
    private static bool _loaded;

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TanbalType",
        "exceptions.txt");

    public static event Action? Changed;

    public static IReadOnlyList<string> GetAll()
    {
        EnsureLoaded();
        lock (Gate)
            return Words.OrderBy(w => w, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static bool IsException(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return false;

        EnsureLoaded();
        lock (Gate)
            return Words.Contains(word);
    }

    public static bool Add(string word)
    {
        word = word.Trim();
        if (word.Length == 0)
            return false;

        EnsureLoaded();
        lock (Gate)
        {
            if (!Words.Add(word))
                return false;
            Save();
        }

        Changed?.Invoke();
        return true;
    }

    public static bool Remove(string word)
    {
        EnsureLoaded();
        lock (Gate)
        {
            if (!Words.Remove(word))
                return false;
            Save();
        }

        Changed?.Invoke();
        return true;
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
                        if (trimmed.Length > 0)
                            Words.Add(trimmed);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.Write($"UserExceptions load failed: {ex.Message}");
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

            File.WriteAllLines(path, Words.OrderBy(w => w, StringComparer.OrdinalIgnoreCase), System.Text.Encoding.UTF8);
        }
        catch (Exception ex)
        {
            AppLog.Write($"UserExceptions save failed: {ex.Message}");
        }
    }
}
